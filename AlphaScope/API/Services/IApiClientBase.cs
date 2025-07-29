using Microsoft.Extensions.Logging;
using RestSharp;

namespace AlphaScope.API.Services
{
    /// <summary>
    /// Base interface for API service classes providing shared functionality
    /// </summary>
    public interface IApiClientBase
    {
        /// <summary>
        /// RestSharp client for HTTP requests
        /// </summary>
        IRestClient RestClient { get; }
        
        /// <summary>
        /// Plugin configuration
        /// </summary>
        Configuration Config { get; }
        
        /// <summary>
        /// Logger instance
        /// </summary>
        ILogger Logger { get; }
    }
}