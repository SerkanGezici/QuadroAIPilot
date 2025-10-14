using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Managers
{
    /// <summary>
    /// ContentDialog'ları yöneten singleton sınıf
    /// "Only a single ContentDialog can be open at any time" hatasını önler
    /// </summary>
    public sealed class DialogManager
    {
        private static readonly Lazy<DialogManager> _instance = new Lazy<DialogManager>(() => new DialogManager());
        private readonly SemaphoreSlim _dialogSemaphore = new SemaphoreSlim(1, 1);
        private readonly Queue<DialogRequest> _dialogQueue = new Queue<DialogRequest>();
        private ContentDialog _currentDialog;
        private bool _isProcessing = false;
        private readonly object _lockObject = new object();

        private DialogManager()
        {
            LogService.LogDebug("[DialogManager] Singleton instance created");
        }

        public static DialogManager Instance => _instance.Value;

        /// <summary>
        /// Güvenli bir şekilde dialog gösterir
        /// </summary>
        public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
        {
            if (dialog == null)
            {
                LogService.LogWarning("[DialogManager] Null dialog provided");
                return ContentDialogResult.None;
            }

            var request = new DialogRequest
            {
                Dialog = dialog,
                CompletionSource = new TaskCompletionSource<ContentDialogResult>()
            };

            lock (_lockObject)
            {
                _dialogQueue.Enqueue(request);
            }

            await ProcessDialogQueue();
            return await request.CompletionSource.Task;
        }

        /// <summary>
        /// Basit bir mesaj dialogu gösterir
        /// </summary>
        public async Task<ContentDialogResult> ShowMessageAsync(string title, string content, string primaryButton = "Tamam", string secondaryButton = null)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    PrimaryButtonText = primaryButton,
                    XamlRoot = GetActiveWindowRoot()
                };

                if (!string.IsNullOrEmpty(secondaryButton))
                {
                    dialog.SecondaryButtonText = secondaryButton;
                }

                return await ShowDialogAsync(dialog);
            }
            catch (Exception ex)
            {
                LogService.LogError($"[DialogManager] ShowMessageAsync hatası: {ex.Message}");
                return ContentDialogResult.None;
            }
        }

        /// <summary>
        /// Hata mesajı gösterir
        /// </summary>
        public async Task ShowErrorAsync(string title, string message)
        {
            await ShowMessageAsync(title ?? "Hata", message, "Tamam");
        }

        /// <summary>
        /// Dialog kuyruğunu işler
        /// </summary>
        private async Task ProcessDialogQueue()
        {
            if (_isProcessing)
                return;

            _isProcessing = true;

            try
            {
                while (true)
                {
                    DialogRequest request = null;

                    lock (_lockObject)
                    {
                        if (_dialogQueue.Count == 0)
                        {
                            _isProcessing = false;
                            break;
                        }
                        request = _dialogQueue.Dequeue();
                    }

                    if (request != null)
                    {
                        await _dialogSemaphore.WaitAsync();
                        try
                        {
                            // Mevcut dialog'u kapat
                            if (_currentDialog != null)
                            {
                                try
                                {
                                    _currentDialog.Hide();
                                }
                                catch (Exception ex)
                                {
                                    LogService.LogDebug($"[DialogManager] Dialog kapatma hatası: {ex.Message}");
                                }
                                _currentDialog = null;
                            }

                            // Kısa bir gecikme ekle
                            await Task.Delay(100);

                            // XamlRoot kontrolü
                            if (request.Dialog.XamlRoot == null)
                            {
                                request.Dialog.XamlRoot = GetActiveWindowRoot();
                            }

                            if (request.Dialog.XamlRoot != null)
                            {
                                _currentDialog = request.Dialog;
                                var result = await request.Dialog.ShowAsync();
                                request.CompletionSource.SetResult(result);
                            }
                            else
                            {
                                LogService.LogWarning("[DialogManager] XamlRoot bulunamadı");
                                request.CompletionSource.SetResult(ContentDialogResult.None);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.LogError($"[DialogManager] Dialog gösterme hatası: {ex.Message}");
                            request.CompletionSource.SetResult(ContentDialogResult.None);
                        }
                        finally
                        {
                            _currentDialog = null;
                            _dialogSemaphore.Release();
                        }
                    }
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Aktif pencerenin XamlRoot'unu alır
        /// </summary>
        private XamlRoot GetActiveWindowRoot()
        {
            try
            {
                // Application'dan aktif pencereyi al
                if (Application.Current is App app && app.MainWindow?.Content != null)
                {
                    return app.MainWindow.Content.XamlRoot;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"[DialogManager] XamlRoot alma hatası: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Tüm bekleyen dialogları temizler
        /// </summary>
        public void ClearQueue()
        {
            lock (_lockObject)
            {
                while (_dialogQueue.Count > 0)
                {
                    var request = _dialogQueue.Dequeue();
                    request.CompletionSource.SetResult(ContentDialogResult.None);
                }
            }

            if (_currentDialog != null)
            {
                try
                {
                    _currentDialog.Hide();
                }
                catch { }
                _currentDialog = null;
            }
        }

        /// <summary>
        /// Mevcut dialog'u kapatır
        /// </summary>
        public void CloseCurrentDialog()
        {
            if (_currentDialog != null)
            {
                try
                {
                    _currentDialog.Hide();
                    LogService.LogDebug("[DialogManager] Mevcut dialog kapatıldı");
                }
                catch (Exception ex)
                {
                    LogService.LogError($"[DialogManager] Dialog kapatma hatası: {ex.Message}");
                }
                finally
                {
                    _currentDialog = null;
                }
            }
        }

        /// <summary>
        /// Dialog açık mı kontrolü
        /// </summary>
        public bool IsDialogOpen => _currentDialog != null;

        /// <summary>
        /// Dialog isteği sınıfı
        /// </summary>
        private class DialogRequest
        {
            public ContentDialog Dialog { get; set; }
            public TaskCompletionSource<ContentDialogResult> CompletionSource { get; set; }
        }
    }
}