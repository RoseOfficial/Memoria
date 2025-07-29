using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using System;
using AlphaScope.API.Abstractions.Services;
using AlphaScope.API.Abstractions.Cache;
using AlphaScope.API.Client;
using AlphaScope.API.Client.Configuration;
using AlphaScope.API.Services;
using AlphaScope.API.Services.Cache;

namespace AlphaScope.API.Extensions
{
    /// <summary>
    /// Extension methods for IServiceCollection to register AlphaScope API services
    /// with dependency injection container. Provides clean, fluent API for configuring
    /// all API-related services with proper lifetime management and configuration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all AlphaScope API services with the dependency injection container.
        /// Configures HTTP client, API services, caching, and all required dependencies
        /// with appropriate service lifetimes for optimal performance and resource management.
        /// </summary>
        /// <param name="services">The service collection to register services with</param>
        /// <param name="configureOptions">Optional action to configure API client options</param>
        /// <returns>The service collection for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when services parameter is null</exception>
        /// <example>
        /// <code>
        /// services.AddAlphaScopeApi(options =>
        /// {
        ///     options.BaseUrl = "https://api.alphascope.example.com/v1/";
        ///     options.TimeoutSeconds = 30;
        ///     options.MaxRetryAttempts = 3;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddAlphaScopeApi(
            this IServiceCollection services,
            Action<ApiClientOptions>? configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Configure API client options with defaults if not already configured
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                // Ensure default options are available even without explicit configuration
                services.PostConfigure<ApiClientOptions>(options =>
                {
                    if (string.IsNullOrWhiteSpace(options.BaseUrl))
                        options.BaseUrl = "https://localhost:5001/v1/";
                    if (options.TimeoutSeconds <= 0)
                        options.TimeoutSeconds = 30;
                    if (options.MaxRetryAttempts <= 0)
                        options.MaxRetryAttempts = 3;
                    if (options.RetryDelayMilliseconds <= 0)
                        options.RetryDelayMilliseconds = 1000;
                    if (string.IsNullOrWhiteSpace(options.UserAgent))
                        options.UserAgent = "AlphaScope/1.0";
                });
            }

            // Register memory cache for caching service
            services.AddMemoryCache();
            
            // Register caching service
            services.AddSingleton<IApiCacheService, ApiCacheService>();

            // Register HTTP client factory for RestSharp
            services.AddSingleton<IRestClient>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ApiClientOptions>>().Value;
                return HttpClientConfiguration.CreateRestClient(options);
            });

            // Register API service interfaces and implementations with appropriate lifetimes
            services.AddSingleton<IPlayerDataService>(serviceProvider =>
            {
                var restClient = serviceProvider.GetRequiredService<IRestClient>();
                var config = serviceProvider.GetRequiredService<Configuration>();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<PlayerDataService>();
                var cacheService = serviceProvider.GetService<IApiCacheService>();
                return new PlayerDataService(restClient, config, logger, cacheService);
            });

            services.AddSingleton<IServerStatusService>(serviceProvider =>
            {
                var restClient = serviceProvider.GetRequiredService<IRestClient>();
                var config = serviceProvider.GetRequiredService<Configuration>();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<ServerStatusService>();
                var cacheService = serviceProvider.GetService<IApiCacheService>();
                return new ServerStatusService(restClient, config, logger, cacheService);
            });

            services.AddSingleton<IUserAuthService>(serviceProvider =>
            {
                var restClient = serviceProvider.GetRequiredService<IRestClient>();
                var config = serviceProvider.GetRequiredService<Configuration>();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<UserAuthService>();
                return new UserAuthService(restClient, config, logger);
            });

            // Register the main API client as scoped service to follow proper DI patterns
            services.AddScoped<ApiClient>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ApiClient>>();
                var options = serviceProvider.GetService<IOptions<ApiClientOptions>>();
                var config = serviceProvider.GetRequiredService<Configuration>();
                
                return new ApiClient(logger, options, config);
            });

            // Note: ApiClient doesn't implement IApiClientBase interface yet
            // This can be added later if needed for abstraction

            return services;
        }

        /// <summary>
        /// Registers AlphaScope API services with explicit configuration instance.
        /// Useful when configuration is managed externally or loaded from specific sources.
        /// </summary>
        /// <param name="services">The service collection to register services with</param>
        /// <param name="configuration">Pre-configured plugin configuration instance</param>
        /// <param name="configureOptions">Optional action to configure additional API client options</param>
        /// <returns>The service collection for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or configuration parameter is null</exception>
        /// <example>
        /// <code>
        /// var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        /// services.AddAlphaScopeApi(config, options =>
        /// {
        ///     options.EnableDetailedErrorLogging = true;
        ///     options.EnableLogging = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddAlphaScopeApi(
            this IServiceCollection services,
            Configuration configuration,
            Action<ApiClientOptions>? configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Register the configuration instance as a singleton
            services.AddSingleton(configuration);

            // Configure API client options based on configuration
            services.Configure<ApiClientOptions>(options =>
            {
                options.BaseUrl = string.IsNullOrWhiteSpace(configuration.BaseUrl) 
                    ? "https://localhost:5001/v1/" 
                    : configuration.BaseUrl;
                options.TimeoutSeconds = 30;
                options.MaxRetryAttempts = 3;
                options.RetryDelayMilliseconds = 1000;
                options.EnableLogging = true;
                options.EnableDetailedErrorLogging = true;
                options.UserAgent = "AlphaScope/1.0";

                // Apply additional configuration if provided
                configureOptions?.Invoke(options);
            });

            // Register the rest of the API services
            return services.AddAlphaScopeApi();
        }

        /// <summary>
        /// Validates that all required API services can be resolved from the service provider.
        /// Useful for integration testing and startup validation to ensure proper DI configuration.
        /// </summary>
        /// <param name="serviceProvider">The service provider to validate</param>
        /// <returns>True if all services can be resolved successfully, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when serviceProvider parameter is null</exception>
        /// <example>
        /// <code>
        /// using var serviceProvider = services.BuildServiceProvider();
        /// if (!serviceProvider.ValidateAlphaScopeApiServices())
        /// {
        ///     throw new InvalidOperationException("Failed to register AlphaScope API services properly");
        /// }
        /// </code>
        /// </example>
        public static bool ValidateAlphaScopeApiServices(this IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            try
            {
                // Attempt to resolve all critical services
                var restClient = serviceProvider.GetRequiredService<IRestClient>();
                var playerService = serviceProvider.GetRequiredService<IPlayerDataService>();
                var serverService = serviceProvider.GetRequiredService<IServerStatusService>();
                var userService = serviceProvider.GetRequiredService<IUserAuthService>();
                var apiClient = serviceProvider.GetRequiredService<ApiClient>();
                var options = serviceProvider.GetRequiredService<IOptions<ApiClientOptions>>();
                var cacheService = serviceProvider.GetService<IApiCacheService>();
                var memoryCache = serviceProvider.GetService<IMemoryCache>();

                // Verify all services are not null
                return restClient != null && 
                       playerService != null && 
                       serverService != null && 
                       userService != null && 
                       apiClient != null && 
                       options?.Value != null &&
                       cacheService != null &&
                       memoryCache != null;
            }
            catch (Exception)
            {
                // If any service cannot be resolved, validation fails
                return false;
            }
        }

        /// <summary>
        /// Registers a custom RestSharp client configuration for advanced scenarios.
        /// Allows fine-grained control over HTTP client behavior, middleware, and serialization.
        /// </summary>
        /// <param name="services">The service collection to register services with</param>
        /// <param name="clientFactory">Factory function to create custom RestSharp client</param>
        /// <returns>The service collection for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or clientFactory parameter is null</exception>
        /// <example>
        /// <code>
        /// services.AddAlphaScopeApiWithCustomClient(provider =>
        /// {
        ///     var options = provider.GetRequiredService&lt;IOptions&lt;ApiClientOptions&gt;&gt;().Value;
        ///     var client = new RestClient(options.BaseUrl);
        ///     // Apply custom configuration
        ///     client.AddDefaultHeader("Custom-Header", "Value");
        ///     return client;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddAlphaScopeApiWithCustomClient(
            this IServiceCollection services,
            Func<IServiceProvider, IRestClient> clientFactory)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (clientFactory == null)
                throw new ArgumentNullException(nameof(clientFactory));

            // Register custom RestSharp client
            services.AddSingleton(clientFactory);

            // Register memory cache and caching service for custom client setup
            services.AddMemoryCache();
            services.AddSingleton<IApiCacheService, ApiCacheService>();

            // Register API services without the default HTTP client registration
            services.AddSingleton<IPlayerDataService>(serviceProvider =>
            {
                var restClient = serviceProvider.GetRequiredService<IRestClient>();
                var config = serviceProvider.GetService<Configuration>() ?? new Configuration();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<PlayerDataService>();
                var cacheService = serviceProvider.GetService<IApiCacheService>();
                return new PlayerDataService(restClient, config, logger, cacheService);
            });

            services.AddSingleton<IServerStatusService>(serviceProvider =>
            {
                var restClient = serviceProvider.GetRequiredService<IRestClient>();
                var config = serviceProvider.GetService<Configuration>() ?? new Configuration();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<ServerStatusService>();
                var cacheService = serviceProvider.GetService<IApiCacheService>();
                return new ServerStatusService(restClient, config, logger, cacheService);
            });

            services.AddSingleton<IUserAuthService>(serviceProvider =>
            {
                var restClient = serviceProvider.GetRequiredService<IRestClient>();
                var config = serviceProvider.GetService<Configuration>() ?? new Configuration();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<UserAuthService>();
                return new UserAuthService(restClient, config, logger);
            });

            // Register the main API client as scoped service
            services.AddScoped<ApiClient>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ApiClient>>();
                var options = serviceProvider.GetService<IOptions<ApiClientOptions>>();
                var config = serviceProvider.GetRequiredService<Configuration>();
                
                return new ApiClient(logger, options, config);
            });

            // Note: ApiClient doesn't implement IApiClientBase interface yet
            // This can be added later if needed for abstraction

            return services;
        }
    }
}