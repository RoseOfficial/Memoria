using System;
using System.ComponentModel.DataAnnotations;

namespace AlphaScope.API.Client.Configuration
{
    /// <summary>
    /// Strongly-typed configuration options for the ApiClient.
    /// Provides validation and default values for API client settings.
    /// </summary>
    public class ApiClientOptions
    {
        /// <summary>
        /// Configuration section name for dependency injection
        /// </summary>
        public const string SectionName = "ApiClient";

        /// <summary>
        /// Base URL for the AlphaScopeServer API endpoint
        /// </summary>
        [Required]
        [Url]
        public string BaseUrl { get; set; } = "https://localhost:5001/v1/";

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum number of retry attempts for failed requests
        /// </summary>
        [Range(0, 10)]
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        [Range(100, 10000)]
        public int RetryDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Whether to enable request/response logging
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// Whether to enable detailed error logging
        /// </summary>
        public bool EnableDetailedErrorLogging { get; set; } = true;

        /// <summary>
        /// User agent string for HTTP requests
        /// </summary>
        public string UserAgent { get; set; } = "AlphaScope/1.0";

        /// <summary>
        /// Maximum size for response content in bytes (default: 10MB)
        /// </summary>
        [Range(1024, 104857600)] // 1KB to 100MB
        public long MaxResponseContentBufferSize { get; set; } = 10485760; // 10MB

        /// <summary>
        /// Whether to validate SSL certificates (SECURITY: Should only be false for localhost development)
        /// Production deployments should ALWAYS use true for security
        /// </summary>
        public bool ValidateSslCertificate { get; set; } = true;

        /// <summary>
        /// Whether to compress requests
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Whether to follow redirects automatically
        /// </summary>
        public bool FollowRedirects { get; set; } = true;

        /// <summary>
        /// Maximum number of redirects to follow
        /// </summary>
        [Range(0, 20)]
        public int MaxRedirects { get; set; } = 5;

        /// <summary>
        /// Validates the configuration options
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(BaseUrl) &&
                   Uri.IsWellFormedUriString(BaseUrl, UriKind.Absolute) &&
                   TimeoutSeconds > 0 &&
                   MaxRetryAttempts >= 0 &&
                   RetryDelayMilliseconds > 0 &&
                   MaxResponseContentBufferSize > 0 &&
                   MaxRedirects >= 0;
        }

        /// <summary>
        /// Gets the timeout as a TimeSpan
        /// </summary>
        public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

        /// <summary>
        /// Gets the retry delay as a TimeSpan
        /// </summary>
        public TimeSpan RetryDelay => TimeSpan.FromMilliseconds(RetryDelayMilliseconds);
    }
}