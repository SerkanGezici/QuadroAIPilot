using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using QuadroAIPilot.State;
using QuadroAIPilot.Services;
using QuadroAIPilot.Interfaces;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Intent sonucuna göre komutları yürüten sınıf
    /// </summary>
    public class CommandExecutor : ICommandExecutor
    {
        private readonly CommandFactory _commandFactory;
        private readonly WindowsApiService _windowsApiService;

        // Son işlenen komut
        private ICommand _lastCommand;
        
        // Ana pencere tanıtıcısı
        private IntPtr _mainWindowHandle;
        
        // Olay tanımlamaları - komut tamamlandıktan sonra tetiklenir
        public event EventHandler CommandCompleted;
        
        // ICommandExecutor interface events
        public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;

        // Çeşitli cevaplar
        private static readonly List<string> SuccessResponses = new List<string>
        {
            "Tamam, hemen hallediyorum.",
            "Tamamlandı.",
            "İşlem başarıyla tamamlandı.",
            "Hazır.",
            "İstediğiniz işlem yapıldı."
        };

        private static readonly List<string> FailureResponses = new List<string>
        {
            "Maalesef bu işlemi yapamadım.",
            "İşlem başarısız oldu.",
            "Bu komutu yerine getiremedim.",
            "Üzgünüm, istediğiniz işlemi gerçekleştiremedim.",
            "Bu işlem için yetkim yok."
        };

        private static readonly List<string> UnknownCommandResponses = new List<string>
        {
            "Bu komutu anlamadım.",
            "Ne yapmak istediğinizi anlayamadım.",
            "Bu komutu tanıyamadım.",
            "Böyle bir komutum yok.",
            "Bu görev için programlanmadım."
        };

        /// <summary>
        /// CommandExecutor kurucu metodu
        /// </summary>
        /// <param name="commandFactory">Komut fabrikası</param>
        /// <param name="windowsApiService">Windows API servisi</param>
        public CommandExecutor(CommandFactory commandFactory, WindowsApiService windowsApiService)
        {
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            _windowsApiService = windowsApiService ?? throw new ArgumentNullException(nameof(windowsApiService));
        }
        
        /// <summary>
        /// Ana pencere handle'ını ayarlar
        /// </summary>
        public void SetMainWindowHandle(IntPtr handle)
        {
            _mainWindowHandle = handle;
            Debug.WriteLine($"[CommandExecutor] Ana pencere handle ayarlandı: {handle}");
        }

        /// <summary>
        /// Intent sonucuna göre komutu yürütür
        /// </summary>
        /// <param name="intentResult">Belirlenen intent</param>
        /// <returns>İşlem sonucu</returns>
        public async Task<bool> ExecuteIntentAsync(CommandIntentResult intentResult)
        {            if (intentResult == null || intentResult.Type == CommandIntentType.Unknown)
            {
                Debug.WriteLine("[CommandExecutor] Bilinmeyen veya null intent.");

                // Sesli yanıt ver - MainWindow feedback sistemi devredeyken devre dışı
                // await RespondAsync(GetRandomResponse(UnknownCommandResponses));

                return false;
            }

            try
            {
                // Uygulama durumunu güncelle
                AppState.CurrentState = AppState.ApplicationState.Executing;

                // Kapatma komutları için özel işlem
                if (intentResult.Type == CommandIntentType.CloseApplication)
                {
                    bool isApplication = intentResult.Command.ToLowerInvariant().Contains("uygulama");
                    bool result;

                    if (isApplication)
                    {
                        result = _windowsApiService.CloseActiveApplication();
                    }
                    else
                    {
                        result = _windowsApiService.CloseActiveWindow();
                    }

                    // Sonuca göre yanıt ver
                    if (result)
                    {
                        // await RespondAsync(GetRandomResponse(SuccessResponses));
                        // Sistem komutlarının diğer komutlarla aynı bildirim davranışını sağlamak için devre dışı bırakıldı
                    }
                    else
                    {
                        // await RespondAsync(GetRandomResponse(FailureResponses));
                        // Sistem komutlarının diğer komutlarla aynı bildirim davranışını sağlamak için devre dışı bırakıldı
                    }
                    
                    // Komut tamamlandı olayını tetikle
                    OnCommandCompleted();

                    return result;
                }                // Diğer komutlar için fabrika kullanarak uygun komutu oluştur
                // Önce yeni CommandRegistry sistemi ile dene
                _lastCommand = _commandFactory.CreateCommandFromText(intentResult.Command);
                  // Eğer yeni sistemde bulunamazsa eski yöntemi kullan
                if (_lastCommand == null)
                {
                    _lastCommand = _commandFactory.CreateCommand(intentResult);
                }                if (_lastCommand == null)
                {
                    Debug.WriteLine("[CommandExecutor] Komut oluşturulamadı.");

                    // Sesli yanıt ver - MainWindow feedback sistemi devredeyken devre dışı
                    // await RespondAsync(GetRandomResponse(UnknownCommandResponses));

                    return false;
                }

                // Komutu çalıştır
                Debug.WriteLine($"[CommandExecutor] Komut çalıştırılıyor: {_lastCommand.CommandText}");
                bool commandResult = await _lastCommand.ExecuteAsync();

                // Sonuca göre yanıt ver
                if (commandResult)
                {
                    Debug.WriteLine("[CommandExecutor] Komut başarıyla tamamlandı.");
                    
                    // Eğer uygulama açma komutu ise ve başarılıysa, gecikme ekle
                    if (intentResult.Type == CommandIntentType.OpenApplication)
                    {
                        // Uygulamanın açılması için bekle
                        await Task.Delay(1500);
                        
                        // Ana pencereyi öne getir
                        BringMainWindowToFront();
                    }

                    // Sesli yanıt ver
                    // await RespondAsync(GetRandomResponse(SuccessResponses));
                    // Sistem komutlarının diğer komutlarla aynı bildirim davranışını sağlamak için devre dışı bırakıldı
                    
                    // Komut tamamlandı olayını tetikle
                    OnCommandCompleted();

                    return true;
                }
                else
                {
                    Debug.WriteLine("[CommandExecutor] Komut başarısız oldu.");

                    // Sesli yanıt ver
                    // await RespondAsync(GetRandomResponse(FailureResponses));
                    // Sistem komutlarının diğer komutlarla aynı bildirim davranışını sağlamak için devre dışı bırakıldı
                    
                    // Komut tamamlandı olayını tetikle
                    OnCommandCompleted();

                    return false;
                }
            }            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandExecutor] Hata: {ex.Message}");
                AppState.SetError();

                // Sesli yanıt ver - MainWindow feedback sistemi devredeyken devre dışı
                // await RespondAsync(GetRandomResponse(FailureResponses));
                
                // Komut tamamlandı olayını tetikle
                OnCommandCompleted();

                return false;
            }
            finally
            {
                // İşlem bittiyse durumu güncelle
                if (AppState.CurrentState == AppState.ApplicationState.Executing)
                {
                    AppState.CurrentState = AppState.ApplicationState.Idle;
                }
            }
        }
        
        /// <summary>
        /// Ana pencereyi öne getirir
        /// </summary>
        private void BringMainWindowToFront()
        {
            try
            {
                // Eğer ana pencere handle'ı varsa
                if (_mainWindowHandle != IntPtr.Zero)
                {
                    Debug.WriteLine("[CommandExecutor] Ana pencereyi öne getirme deneniyor...");
                    
                    // Pencereyi öne getir
                    bool result = _windowsApiService.ForceForegroundWindow(_mainWindowHandle);
                    
                    if (result)
                    {
                        Debug.WriteLine("[CommandExecutor] Ana pencere öne getirildi.");
                    }
                    else
                    {
                        Debug.WriteLine("[CommandExecutor] Ana pencere öne getirilemedi.");
                    }
                }
                else
                {
                    Debug.WriteLine("[CommandExecutor] Ana pencere handle'ı tanımlanmamış, öne getirme atlanıyor.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandExecutor] Ana pencereyi öne getirme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Kullanıcıya sesli yanıt verir
        /// </summary>
        private async Task RespondAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                // Uygulama durumunu güncelle
                AppState.CurrentState = AppState.ApplicationState.Speaking;

                // Sesli yanıt ver
                await TextToSpeechService.SpeakTextAsync(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandExecutor] Sesli yanıt hatası: {ex.Message}");
            }
            finally
            {
                // İşlem bittiyse durumu güncelle
                if (AppState.CurrentState == AppState.ApplicationState.Speaking)
                {
                    AppState.CurrentState = AppState.ApplicationState.Idle;
                }
            }
        }

        /// <summary>
        /// Verilen yanıt dizisinden rastgele bir yanıt seçer
        /// </summary>
        private string GetRandomResponse(List<string> responses)
        {
            if (responses == null || responses.Count == 0)
                return string.Empty;

            Random random = new Random();
            int index = random.Next(0, responses.Count);
            return responses[index];
        }
        
        /// <summary>
        /// Komut tamamlandı olayını tetikler
        /// </summary>
        private void OnCommandCompleted()
        {
            try
            {
                CommandCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandExecutor] CommandCompleted olayı hatası: {ex.Message}");
            }
        }

        // ICommandExecutor interface implementation
        public async Task<bool> ExecuteAsync(ICommand command)
        {
            if (command == null) return false;
            
            var stopwatch = Stopwatch.StartNew();
            bool success = false;
            string? errorMessage = null;
            
            try
            {
                success = await command.ExecuteAsync();
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                success = false;
            }
            finally
            {
                stopwatch.Stop();
                
                // Fire the CommandExecuted event
                CommandExecuted?.Invoke(this, new CommandExecutedEventArgs
                {
                    Command = command,
                    Success = success,
                    ErrorMessage = errorMessage,
                    ExecutionTime = stopwatch.Elapsed
                });
            }
            
            return success;
        }

        public async Task<bool> ExecuteCommandTextAsync(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText)) return false;
            
            var command = _commandFactory.CreateCommandFromText(commandText);
            if (command == null) return false;
            
            return await ExecuteAsync(command);
        }
    }
}