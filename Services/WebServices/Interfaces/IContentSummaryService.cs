using System.Collections.Generic;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;

namespace QuadroAIPilot.Services.WebServices.Interfaces
{
    /// <summary>
    /// Interface for content summarization service
    /// </summary>
    public interface IContentSummaryService
    {
        /// <summary>
        /// Summarize content based on provided options
        /// </summary>
        Task<string> SummarizeAsync(string content, SummaryOptions options = null);

        /// <summary>
        /// Extract keywords from content
        /// </summary>
        Task<Dictionary<string, int>> ExtractKeywordsAsync(string content, int maxKeywords = 10);

        /// <summary>
        /// Check if service is available
        /// </summary>
        Task<bool> IsAvailableAsync();
    }
}