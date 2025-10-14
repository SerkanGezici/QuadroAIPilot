using System.Collections.Generic;
using System.Threading.Tasks;
using QuadroAIPilot.Models.Web;

namespace QuadroAIPilot.Services.WebServices.Interfaces
{
    /// <summary>
    /// Main orchestrator for web content retrieval
    /// </summary>
    public interface IWebContentService
    {
        /// <summary>
        /// Get content based on request, using appropriate provider
        /// </summary>
        Task<WebContent> GetContentAsync(ContentRequest request);

        /// <summary>
        /// Get summarized content
        /// </summary>
        Task<WebContent> GetSummarizedContentAsync(ContentRequest request, SummaryOptions options = null);

        /// <summary>
        /// Search for content across multiple providers
        /// </summary>
        Task<WebContent> SearchAsync(string query, ContentType? preferredType = null);

        /// <summary>
        /// Get trending topics
        /// </summary>
        Task<List<TrendingTopic>> GetTrendsAsync(string location = "turkey");

        /// <summary>
        /// Analyze a specific trend
        /// </summary>
        Task<TrendAnalysis> AnalyzeTrendAsync(string topic);
    }
}