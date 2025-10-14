using System.Threading.Tasks;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Tüm komutlar için temel arayüz
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Komutun çalıştırılması
        /// </summary>
        /// <returns>İşlem sonucunu belirten Task</returns>
        Task<bool> ExecuteAsync();

        /// <summary>
        /// Komut metni
        /// </summary>
        string CommandText { get; }
    }
}