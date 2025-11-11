using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace QuadroAIPilot.Dialogs
{
    /// <summary>
    /// Güncelleme kurulum onay dialog'u
    /// İndirme tamamlandıktan sonra kullanıcıya kurulum onayı sorar
    /// </summary>
    public sealed partial class UpdateInstallConfirmationDialog : ContentDialog
    {
        public UpdateInstallConfirmationDialog()
        {
            this.InitializeComponent();

            Log.Information("[UpdateInstallConfirmationDialog] Kurulum onay dialog'u açıldı");
        }
    }
}
