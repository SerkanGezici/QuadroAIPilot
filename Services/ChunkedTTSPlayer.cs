using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuadroAIPilot.Interfaces;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Chunked TTS Player - Edge TTS'i küçük parçalarda oynatarak anında kesme sağlar
    /// ChatGPT benzeri interrupt capability için optimize edilmiş
    /// </summary>
    public class ChunkedTTSPlayer : IDisposable
    {
        #region Private Fields
        
        private readonly IWebViewManager _webViewManager;
        private CancellationTokenSource _playbackCts;
        private volatile bool _isPlaying = false;
        private volatile bool _shouldStop = false;
        private readonly object _lockObject = new object();
        private int _currentChunkIndex = 0;
        private List<byte[]> _audioChunks;
        
        // Chunk boyutu (milisaniye cinsinden)
        private const int CHUNK_DURATION_MS = 50;
        // 44100 Hz, 16-bit, mono için chunk boyutu
        private const int CHUNK_SIZE_BYTES = 4410; // 50ms için yaklaşık boyut
        
        #endregion
        
        #region Events
        
        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackCompleted;
        public event EventHandler PlaybackInterrupted;
        public event EventHandler<ChunkPlaybackEventArgs> ChunkPlayed;
        
        #endregion
        
        #region Constructor
        
        public ChunkedTTSPlayer(IWebViewManager webViewManager)
        {
            _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
            _audioChunks = new List<byte[]>();
        }
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// TTS şu anda oynatılıyor mu?
        /// </summary>
        public bool IsPlaying 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    return _isPlaying; 
                } 
            } 
        }
        
        /// <summary>
        /// Mevcut chunk index'i
        /// </summary>
        public int CurrentChunkIndex 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    return _currentChunkIndex; 
                } 
            } 
        }
        
        /// <summary>
        /// Toplam chunk sayısı
        /// </summary>
        public int TotalChunks 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    return _audioChunks?.Count ?? 0; 
                } 
            } 
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Audio verisi ile chunked oynatma başlat
        /// </summary>
        /// <param name="audioData">Oynatılacak audio data</param>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Oynatma başarılı mı?</returns>
        public async Task<bool> PlayChunkedAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            if (audioData == null || audioData.Length == 0)
            {
                LogService.LogDebug("[ChunkedTTSPlayer] Boş audio data");
                return false;
            }
            
            lock (_lockObject)
            {
                if (_isPlaying)
                {
                    LogService.LogDebug("[ChunkedTTSPlayer] Zaten oynatılıyor, yeni istek iptal edildi");
                    return false;
                }
                
                _isPlaying = true;
                _shouldStop = false;
                _currentChunkIndex = 0;
            }
            
            try
            {
                LogService.LogDebug($"[ChunkedTTSPlayer] Chunked oynatma başlatılıyor: {audioData.Length} bytes");
                
                // Audio'yu chunk'lara böl
                _audioChunks = SplitAudioIntoChunks(audioData);
                
                LogService.LogDebug($"[ChunkedTTSPlayer] {_audioChunks.Count} chunk oluşturuldu");
                
                // Playback token oluştur
                _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                // Event tetikle
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
                
                // Chunk'ları sırayla oynat
                bool completed = await PlayChunksSequentially(_playbackCts.Token);
                
                if (completed && !_shouldStop)
                {
                    LogService.LogDebug("[ChunkedTTSPlayer] Oynatma tamamlandı");
                    PlaybackCompleted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    LogService.LogDebug("[ChunkedTTSPlayer] Oynatma kesildi");
                    PlaybackInterrupted?.Invoke(this, EventArgs.Empty);
                }
                
                return completed;
            }
            catch (OperationCanceledException)
            {
                LogService.LogDebug("[ChunkedTTSPlayer] Oynatma iptal edildi");
                PlaybackInterrupted?.Invoke(this, EventArgs.Empty);
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[ChunkedTTSPlayer] Oynatma hatası: {ex.Message}");
                return false;
            }
            finally
            {
                lock (_lockObject)
                {
                    _isPlaying = false;
                    _shouldStop = false;
                }
                
                _playbackCts?.Dispose();
                _playbackCts = null;
            }
        }
        
        /// <summary>
        /// Oynatmayı anında kes
        /// </summary>
        public void InterruptPlayback()
        {
            lock (_lockObject)
            {
                if (!_isPlaying)
                {
                    LogService.LogDebug("[ChunkedTTSPlayer] Zaten oynatılmıyor, interrupt gerekmez");
                    return;
                }
                
                _shouldStop = true;
                LogService.LogDebug("[ChunkedTTSPlayer] Interrupt signal gönderildi");
            }
            
            // Playback token'ı iptal et
            _playbackCts?.Cancel();
            
            // WebView'daki audio'yu da durdur
            _ = Task.Run(async () =>
            {
                try
                {
                    await _webViewManager.ExecuteScriptAsync(@"
                        if (window.currentChunkedAudio) {
                            window.currentChunkedAudio.pause();
                            window.currentChunkedAudio.currentTime = 0;
                            window.currentChunkedAudio = null;
                        }
                    ");
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"[ChunkedTTSPlayer] WebView stop error: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Oynatma ilerlemesi (0.0 - 1.0)
        /// </summary>
        /// <returns>İlerleme yüzdesi</returns>
        public float GetPlaybackProgress()
        {
            lock (_lockObject)
            {
                if (_audioChunks == null || _audioChunks.Count == 0)
                    return 0.0f;
                
                return (float)_currentChunkIndex / _audioChunks.Count;
            }
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Audio data'yı chunk'lara böl
        /// </summary>
        /// <param name="audioData">Bölünecek audio data</param>
        /// <returns>Chunk listesi</returns>
        private List<byte[]> SplitAudioIntoChunks(byte[] audioData)
        {
            var chunks = new List<byte[]>();
            
            for (int i = 0; i < audioData.Length; i += CHUNK_SIZE_BYTES)
            {
                int chunkSize = Math.Min(CHUNK_SIZE_BYTES, audioData.Length - i);
                byte[] chunk = new byte[chunkSize];
                Array.Copy(audioData, i, chunk, 0, chunkSize);
                chunks.Add(chunk);
            }
            
            return chunks;
        }
        
        /// <summary>
        /// Chunk'ları sırayla oynat
        /// </summary>
        /// <param name="cancellationToken">İptal token'ı</param>
        /// <returns>Tamamlandı mı?</returns>
        private async Task<bool> PlayChunksSequentially(CancellationToken cancellationToken)
        {
            for (_currentChunkIndex = 0; _currentChunkIndex < _audioChunks.Count; _currentChunkIndex++)
            {
                // İptal kontrolü
                if (_shouldStop || cancellationToken.IsCancellationRequested)
                {
                    LogService.LogDebug($"[ChunkedTTSPlayer] Chunk {_currentChunkIndex} interrupted");
                    return false;
                }
                
                try
                {
                    // Chunk'ı oynat
                    await PlaySingleChunk(_audioChunks[_currentChunkIndex], _currentChunkIndex);
                    
                    // Chunk event'i tetikle
                    ChunkPlayed?.Invoke(this, new ChunkPlaybackEventArgs
                    {
                        ChunkIndex = _currentChunkIndex,
                        TotalChunks = _audioChunks.Count,
                        Progress = GetPlaybackProgress()
                    });
                    
                    // Sonraki chunk için kısa bekleme (overlap için)
                    await Task.Delay(45, cancellationToken); // 50ms chunk, 45ms bekleme = 5ms overlap
                }
                catch (OperationCanceledException)
                {
                    LogService.LogDebug($"[ChunkedTTSPlayer] Chunk {_currentChunkIndex} cancelled");
                    return false;
                }
            }
            
            return true; // Tüm chunk'lar tamamlandı
        }
        
        /// <summary>
        /// Tek bir chunk'ı WebView'da oynat
        /// </summary>
        /// <param name="chunkData">Chunk data</param>
        /// <param name="chunkIndex">Chunk index'i</param>
        private async Task PlaySingleChunk(byte[] chunkData, int chunkIndex)
        {
            try
            {
                string base64Audio = Convert.ToBase64String(chunkData);
                
                string script = $@"
                    (function() {{
                        // Önceki chunk'ı temizle
                        if (window.currentChunkedAudio) {{
                            window.currentChunkedAudio.pause();
                        }}
                        
                        // Yeni audio element oluştur
                        window.currentChunkedAudio = new Audio();
                        window.currentChunkedAudio.src = 'data:audio/wav;base64,{base64Audio}';
                        window.currentChunkedAudio.volume = 1.0;
                        
                        // Hemen oynat
                        window.currentChunkedAudio.play().catch(function(error) {{
                            console.log('Chunk {chunkIndex} play error:', error);
                        }});
                    }})();
                ";
                
                await _webViewManager.ExecuteScriptAsync(script);
                
                LogService.LogVerbose($"[ChunkedTTSPlayer] Chunk {chunkIndex} played: {chunkData.Length} bytes");
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[ChunkedTTSPlayer] Chunk {chunkIndex} play error: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            InterruptPlayback();
            _playbackCts?.Dispose();
            _audioChunks?.Clear();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Chunk oynatma event argümanları
    /// </summary>
    public class ChunkPlaybackEventArgs : EventArgs
    {
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public float Progress { get; set; }
    }
}