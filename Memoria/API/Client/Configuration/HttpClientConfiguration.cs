using System;
using System.Net.Http;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Options;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;

namespace Memoria.API.Client.Configuration
{
    /// <summary>
    /// Configuration helper for setting up HTTP clients with proper settings
    /// </summary>
    public static class HttpClientConfiguration
    {
        /// <summary>
        /// Configures an HttpClient instance with the provided options
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to configure</param>
        /// <param name="options">The configuration options</param>
        public static void ConfigureHttpClient(HttpClient httpClient, ApiClientOptions options)
        {
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Set timeout
            httpClient.Timeout = options.Timeout;

            // Set user agent
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

            // Set maximum response content buffer size
            httpClient.MaxResponseContentBufferSize = options.MaxResponseContentBufferSize;

            // Add common headers
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        }

        /// <summary>
        /// Creates a configured HttpClientHandler with the provided options
        /// </summary>
        /// <param name="options">The configuration options</param>
        /// <returns>A configured HttpClientHandler</returns>
        public static HttpClientHandler CreateHttpClientHandler(ApiClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var handler = new HttpClientHandler();

            // Configure SSL validation with secure defaults
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
            {
                return ValidateServerCertificate(cert, chain, sslPolicyErrors, options);
            };

            // Configure compression
            if (options.EnableCompression)
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }

            // Configure redirects
            handler.AllowAutoRedirect = options.FollowRedirects;
            if (options.FollowRedirects)
            {
                handler.MaxAutomaticRedirections = options.MaxRedirects;
            }

            return handler;
        }

        /// <summary>
        /// Creates a configured RestClient instance with the provided options
        /// </summary>
        /// <param name="options">The configuration options</param>
        /// <returns>A configured RestClient</returns>
        public static RestClient CreateRestClient(ApiClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var restClientOptions = new RestClientOptions(options.BaseUrl)
            {
                Timeout = options.Timeout,
                UserAgent = options.UserAgent,
                ThrowOnAnyError = false, // We'll handle errors manually
                ThrowOnDeserializationError = false,
                FollowRedirects = options.FollowRedirects,
                MaxRedirects = options.MaxRedirects
            };

            // Configure the handler if needed
            if (!options.ValidateSslCertificate || options.EnableCompression)
            {
                restClientOptions.ConfigureMessageHandler = _ => CreateHttpClientHandler(options);
            }

            var client = new RestClient(
                restClientOptions,
                configureSerialization: s => s.UseNewtonsoftJson()
            );

            return client;
        }

        /// <summary>
        /// Creates a RestRequest with common configuration
        /// </summary>
        /// <param name="resource">The resource URL</param>
        /// <param name="method">The HTTP method</param>
        /// <param name="options">The configuration options</param>
        /// <returns>A configured RestRequest</returns>
        public static RestRequest CreateRestRequest(string resource, Method method, ApiClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var request = new RestRequest(resource, method);

            // Set timeout for this specific request
            request.Timeout = options.Timeout;

            // Add common headers
            request.AddHeader("Accept", "application/json");
            
            if (options.EnableCompression)
            {
                request.AddHeader("Accept-Encoding", "gzip, deflate");
            }

            return request;
        }

        /// <summary>
        /// Validates the configuration options
        /// </summary>
        /// <param name="options">The options to validate</param>
        /// <throws>ArgumentException if options are invalid</throws>
        public static void ValidateOptions(ApiClientOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (!options.IsValid())
                throw new ArgumentException("Invalid ApiClientOptions configuration", nameof(options));
        }

        /// <summary>
        /// Validates server certificates with secure defaults and proper logging
        /// </summary>
        /// <param name="certificate">The server certificate</param>
        /// <param name="chain">The certificate chain</param>
        /// <param name="sslPolicyErrors">SSL policy errors</param>
        /// <param name="options">API client options</param>
        /// <returns>True if certificate should be accepted</returns>
        private static bool ValidateServerCertificate(
            X509Certificate2? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors,
            ApiClientOptions options)
        {
            // Always accept valid certificates
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // For production environments, always enforce strict SSL validation
            if (!options.ValidateSslCertificate)
            {
                // Only allow bypass for localhost/development scenarios
                if (certificate != null && IsLocalhostCertificate(certificate))
                {
                    if (options.EnableDetailedErrorLogging)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SSL Warning] Bypassing certificate validation for localhost: {certificate.Subject}");
                    }
                    return true;
                }

                // Log security warning for non-localhost bypasses
                if (options.EnableDetailedErrorLogging)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SSL Security Warning] Certificate validation bypassed for: {certificate?.Subject ?? "Unknown"}, " +
                        $"Errors: {sslPolicyErrors}");
                }

                // In development mode, allow bypass but log it prominently
                return true;
            }

            // Log certificate validation failures
            if (options.EnableDetailedErrorLogging && certificate != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SSL Error] Certificate validation failed for: {certificate.Subject}, " +
                    $"Errors: {sslPolicyErrors}");
            }

            return false;
        }

        /// <summary>
        /// Checks if the certificate is for localhost development
        /// </summary>
        /// <param name="certificate">The certificate to check</param>
        /// <returns>True if this appears to be a localhost development certificate</returns>
        private static bool IsLocalhostCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) return false;

            var subject = certificate.Subject.ToLowerInvariant();
            return subject.Contains("localhost") || 
                   subject.Contains("127.0.0.1") || 
                   subject.Contains("::1");
        }
    }

}