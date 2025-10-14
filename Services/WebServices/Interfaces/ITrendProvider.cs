using System.Collections.Generic;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;

namespace QuadroAIPilot.Services.WebServices.Interfaces
{
    /// <summary>
    /// Interface for trend providers (Twitter, social media, etc.)
    /// </summary>
    public interface ITrendProvider
    {
        /// <summary>
        /// Get trending topics for a specific location
        /// </summary>
        Task<List<TrendingTopic>> GetTrendsAsync(string location = "turkey", int count = 10);

        /// <summary>
        /// Get trend analysis with sentiment and details
        /// </summary>
        Task<TrendAnalysis> AnalyzeTrendAsync(TrendingTopic trend);

        /// <summary>
        /// Search for specific trend or hashtag
        /// </summary>
        Task<TrendSearchResult> SearchTrendAsync(string query);
    }
}