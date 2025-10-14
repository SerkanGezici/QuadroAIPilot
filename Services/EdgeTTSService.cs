using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    public class EdgeTTSService : IDisposable
    {
        private const string EDGE_TTS_URL = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
        private const string TRUSTED_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        
        private ClientWebSocket _webSocket;
        private readonly List<byte[]> _audioChunks;
        private const int MaxRetryCount = 3;
        private const int RetryDelayMs = 1000;
        
        public EdgeTTSService()
        {
            _audioChunks = new List<byte[]>();
        }
        
        public async Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "tr-TR-EmelNeural", CancellationToken cancellationToken = default)
        {
            Exception lastException = null;
            
            for (int attempt = 0; attempt < MaxRetryCount; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        LoggingService.LogWarning($"Yeniden deneme #{attempt + 1}");
                        await Task.Delay(RetryDelayMs * attempt, cancellationToken); // Progressive delay
                    }
                    
                    _audioChunks.Clear();
                    
                    // WebSocket bağlantısı kur
                    _webSocket = new ClientWebSocket();
                    
                    // Gerekli başlıkları ekle
                    _webSocket.Options.SetRequestHeader("Pragma", "no-cache");
                    _webSocket.Options.SetRequestHeader("Cache-Control", "no-cache");
                    _webSocket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
                    _webSocket.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
                    _webSocket.Options.SetRequestHeader("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
                    _webSocket.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36 Edg/91.0.864.41");
                    
                    var uri = new Uri($"{EDGE_TTS_URL}?TrustedClientToken={Uri.EscapeDataString(TRUSTED_TOKEN)}&ConnectionId={Guid.NewGuid():N}");
                    
                    LoggingService.LogWarning($"WebSocket bağlantısı kuruluyor: {uri}");
                    await _webSocket.ConnectAsync(uri, cancellationToken);
                
                if (_webSocket.State != WebSocketState.Open)
                {
                    throw new Exception("WebSocket bağlantısı kurulamadı");
                }
                
                LoggingService.LogWarning("WebSocket bağlantısı kuruldu");
                
                // Config mesajı gönder
                var timestamp = DateTimeOffset.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " GMT+0000 (Coordinated Universal Time)";
                var requestId = Guid.NewGuid().ToString();
                
                var configMessage = new
                {
                    context = new
                    {
                        synthesis = new
                        {
                            audio = new
                            {
                                metadataoptions = new
                                {
                                    sentenceBoundaryEnabled = "true",
                                    wordBoundaryEnabled = "true"
                                },
                                outputFormat = "webm-24khz-16bit-mono-opus"
                            }
                        }
                    }
                };
                
                var configJson = JsonSerializer.Serialize(configMessage);
                var configHeader = $"X-Timestamp:{timestamp}\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n";
                var configData = configHeader + configJson;
                await SendTextAsync(configData, cancellationToken);
                
                LoggingService.LogVerbose("[EdgeTTSService] Config mesajı gönderildi");
                
                // SSML mesajı gönder
                // Apostrofu koruyarak SSML escape işlemi yap
                var escapedText = EscapeForSSML(text);
                var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='tr-TR'>
                    <voice name='{voice}'>
                        <prosody pitch='+0Hz' rate='-5%' volume='+0%'>
                            {escapedText}
                        </prosody>
                    </voice>
                </speak>";
                
                var ssmlHeader = $"X-RequestId:{requestId}\r\nContent-Type:application/ssml+xml\r\nX-Timestamp:{timestamp}\r\nPath:ssml\r\n\r\n";
                var ssmlMessage = ssmlHeader + ssml;
                await SendTextAsync(ssmlMessage, cancellationToken);
                
                LoggingService.LogVerbose($"[EdgeTTSService] SSML mesajı gönderildi - Voice: {voice}");
                
                // Audio data'yı al
                await ReceiveAudioAsync(cancellationToken);
                
                // Tüm chunk'ları birleştir
                if (_audioChunks.Count == 0)
                {
                    throw new Exception("Ses verisi alınamadı");
                }
                
                var totalLength = _audioChunks.Sum(chunk => chunk.Length);
                var audioData = new byte[totalLength];
                var offset = 0;
                
                foreach (var chunk in _audioChunks)
                {
                    Buffer.BlockCopy(chunk, 0, audioData, offset, chunk.Length);
                    offset += chunk.Length;
                }
                
                    LoggingService.LogVerbose($"[EdgeTTSService] Toplam ses verisi: {audioData.Length} bytes");
                    
                    return audioData; // Başarılı, döndür
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LoggingService.LogWarning($"Hata (deneme {attempt + 1}/{MaxRetryCount}): {ex.Message}");
                    
                    // WebSocket'i temizle
                    if (_webSocket?.State == WebSocketState.Open)
                    {
                        try
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Error", CancellationToken.None);
                        }
                        catch { }
                    }
                    _webSocket?.Dispose();
                    _webSocket = null;
                    
                    // Son deneme değilse devam et
                    if (attempt < MaxRetryCount - 1)
                    {
                        continue;
                    }
                }
            }
            
            // Tüm denemeler başarısız
            throw new Exception($"Edge TTS {MaxRetryCount} deneme sonrası başarısız oldu", lastException);
        }
        
        private async Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }
        
        private async Task ReceiveAudioAsync(CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[65536]); // Daha büyük buffer
            var messageBuffer = new List<byte>();
            
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Text mesajları işle
                    messageBuffer.AddRange(buffer.Array.Take(result.Count));
                    
                    if (result.EndOfMessage)
                    {
                        var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        LoggingService.LogVerbose($"Text mesaj alındı (boyut: {message.Length})");
                        
                        if (message.Contains("turn.end"))
                        {
                            LoggingService.LogVerbose("[EdgeTTSService] Stream tamamlandı");
                            break;
                        }
                        
                        messageBuffer.Clear();
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Binary mesajları işle - header'ı ayır
                    var data = buffer.Array.Take(result.Count).ToArray();
                    
                    // İlk 2 byte header size'ı içerir
                    if (data.Length > 2)
                    {
                        var headerSize = BitConverter.ToUInt16(new byte[] { data[1], data[0] }, 0);
                        
                        // Audio data header'dan sonra başlar
                        if (data.Length > headerSize + 2)
                        {
                            var audioData = data.Skip(headerSize + 2).ToArray();
                            _audioChunks.Add(audioData);
                            LoggingService.LogVerbose($"Audio chunk alındı: {audioData.Length} bytes (header: {headerSize} bytes)");
                        }
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    LoggingService.LogVerbose("[EdgeTTSService] WebSocket kapatıldı");
                    break;
                }
            }
        }
        
        // Desteklenen sesler
        public static class Voices
        {
            public const string TurkishFemale = "tr-TR-EmelNeural";
            public const string TurkishMale = "tr-TR-AhmetNeural";
            
            public static readonly Dictionary<string, string> All = new()
            {
                { "emel", TurkishFemale },
                { "ahmet", TurkishMale },
                { "female", TurkishFemale },
                { "male", TurkishMale },
                { "kadın", TurkishFemale },
                { "erkek", TurkishMale }
            };
        }
        
        /// <summary>
        /// SSML için metni escape eder, apostrof karakterini korur
        /// </summary>
        private string EscapeForSSML(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // SSML'de özel karakterleri escape et
            // Apostrof karakterini escape etmiyoruz, Edge TTS bunu doğru işliyor
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
            // Apostrof (') karakterini escape etmiyoruz
        }
        
        public void Dispose()
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposed", CancellationToken.None).Wait();
            }
            _webSocket?.Dispose();
        }
    }
}