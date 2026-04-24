using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using Xunit;
using Memoria.API;
using Memoria.API.Abstractions.Services;
using Memoria.API.Client.Configuration;
using Memoria.API.Extensions;

namespace Memoria.Tests.API.Extensions
{
    /// <summary>
    /// Tests for the ServiceCollectionExtensions to ensure proper dependency injection
    /// registration and service resolution for Memoria API services.
    /// </summary>
    public class ServiceCollectionExtensionsTests
    {
        /// <summary>
        /// Tests that AddMemoriaApi extension method registers all required services
        /// and they can be resolved properly from the service provider.
        /// </summary>
        [Fact]
        public void AddMemoriaApi_RegistersAllServices_SuccessfullyResolvesServices()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            
            var config = new Configuration
            {
                BaseUrl = "https://test.api.com/v1/",
                Key = "test-api-key"
            };

            // Act
            services.AddMemoriaApi(config, options =>
            {
                options.TimeoutSeconds = 60;
                options.MaxRetryAttempts = 5;
                options.EnableLogging = true;
            });

            using var serviceProvider = services.BuildServiceProvider();

            // Assert - Verify all services can be resolved
            var restClient = serviceProvider.GetRequiredService<IRestClient>();
            Assert.NotNull(restClient);

            var playerService = serviceProvider.GetRequiredService<IPlayerDataService>();
            Assert.NotNull(playerService);

            var serverService = serviceProvider.GetRequiredService<IServerStatusService>();
            Assert.NotNull(serverService);

            var userService = serviceProvider.GetRequiredService<IUserAuthService>();
            Assert.NotNull(userService);

            var apiClient = serviceProvider.GetRequiredService<ApiClient>();
            Assert.NotNull(apiClient);

            var options = serviceProvider.GetRequiredService<IOptions<ApiClientOptions>>();
            Assert.NotNull(options);
            Assert.NotNull(options.Value);

            var resolvedConfig = serviceProvider.GetRequiredService<Configuration>();
            Assert.NotNull(resolvedConfig);
            Assert.Equal(config.BaseUrl, resolvedConfig.BaseUrl);
            Assert.Equal(config.Key, resolvedConfig.Key);
        }

        /// <summary>
        /// Tests that AddMemoriaApi without explicit configuration works with defaults.
        /// </summary>
        [Fact]
        public void AddMemoriaApi_WithoutConfiguration_UsesDefaults()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddMemoriaApi(options =>
            {
                options.BaseUrl = "https://custom.api.com/v1/";
                options.TimeoutSeconds = 45;
            });

            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<ApiClientOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal("https://custom.api.com/v1/", options.Value.BaseUrl);
            Assert.Equal(45, options.Value.TimeoutSeconds);
            Assert.Equal(3, options.Value.MaxRetryAttempts); // Default value
        }

        /// <summary>
        /// Tests that ValidateMemoriaApiServices extension method correctly validates
        /// that all required services are registered and can be resolved.
        /// </summary>
        [Fact]
        public void ValidateMemoriaApiServices_WithProperRegistration_ReturnsTrue()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            
            var config = new Configuration
            {
                BaseUrl = "https://test.api.com/v1/"
            };

            services.AddMemoriaApi(config);

            using var serviceProvider = services.BuildServiceProvider();

            // Act
            var isValid = serviceProvider.ValidateMemoriaApiServices();

            // Assert
            Assert.True(isValid);
        }

        /// <summary>
        /// Tests that ValidateMemoriaApiServices returns false when required services
        /// are not registered properly.
        /// </summary>
        [Fact]
        public void ValidateMemoriaApiServices_WithMissingServices_ReturnsFalse()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            // Not registering Memoria services

            using var serviceProvider = services.BuildServiceProvider();

            // Act
            var isValid = serviceProvider.ValidateMemoriaApiServices();

            // Assert
            Assert.False(isValid);
        }

        /// <summary>
        /// Tests that multiple configurations can be applied and the last one wins.
        /// </summary>
        [Fact]
        public void AddMemoriaApi_MultipleConfigurations_LastConfigurationWins()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Act - Apply multiple configurations
            services.Configure<ApiClientOptions>(options =>
            {
                options.BaseUrl = "https://first.api.com/v1/";
                options.TimeoutSeconds = 30;
            });

            services.Configure<ApiClientOptions>(options =>
            {
                options.BaseUrl = "https://second.api.com/v1/";
                options.MaxRetryAttempts = 5;
            });

            services.AddMemoriaApi(); // This will apply defaults but should preserve existing config

            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<ApiClientOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal("https://second.api.com/v1/", options.Value.BaseUrl);
            Assert.Equal(5, options.Value.MaxRetryAttempts);
        }

        /// <summary>
        /// Tests that service registration preserves singleton lifetime for all API services.
        /// </summary>
        [Fact]
        public void AddMemoriaApi_ServiceLifetimes_AreSingleton()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            
            var config = new Configuration();
            services.AddMemoriaApi(config);

            using var serviceProvider = services.BuildServiceProvider();

            // Act - Resolve services multiple times
            var apiClient1 = serviceProvider.GetRequiredService<ApiClient>();
            var apiClient2 = serviceProvider.GetRequiredService<ApiClient>();

            var playerService1 = serviceProvider.GetRequiredService<IPlayerDataService>();
            var playerService2 = serviceProvider.GetRequiredService<IPlayerDataService>();

            var restClient1 = serviceProvider.GetRequiredService<IRestClient>();
            var restClient2 = serviceProvider.GetRequiredService<IRestClient>();

            // Assert - Same instances should be returned (singleton behavior)
            Assert.Same(apiClient1, apiClient2);
            Assert.Same(playerService1, playerService2);
            Assert.Same(restClient1, restClient2);
        }

        /// <summary>
        /// Tests that custom RestClient factory works correctly.
        /// </summary>
        [Fact]
        public void AddMemoriaApiWithCustomClient_CustomFactory_UsesCustomClient()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            var customRestClient = new RestClient("https://custom.api.com/");

            // Act
            services.AddMemoriaApiWithCustomClient(provider => customRestClient);

            using var serviceProvider = services.BuildServiceProvider();

            // Assert
            var resolvedClient = serviceProvider.GetRequiredService<IRestClient>();
            Assert.Same(customRestClient, resolvedClient);

            // Verify other services are still registered
            var playerService = serviceProvider.GetRequiredService<IPlayerDataService>();
            Assert.NotNull(playerService);
        }
    }
}