using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memoria.API.Security
{
    /// <summary>
    /// Secure logging wrapper that automatically sanitizes sensitive data from log messages.
    /// Prevents accidental exposure of API keys, tokens, passwords, and other sensitive information.
    /// </summary>
    public class SecureLogger
    {
        private readonly ILogger _logger;
        private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "key", "apikey", "api_key", "token", "password", "secret", "credential", "auth",
            "authorization", "bearer", "x-api-key", "authentication", "clientsecret", 
            "client_secret", "accesstoken", "access_token", "refreshtoken", "refresh_token",
            "sessiontoken", "session_token", "privatekey", "private_key"
        };

        private static readonly Regex[] SensitivePatterns = 
        {
            new(@"(?i)(api[_-]?key|token|password|secret|credential|auth|bearer)\s*[:=]\s*[""']?([^""'\s,}]+)", RegexOptions.Compiled),
            new(@"(?i)(authorization|x-api-key)\s*:\s*[""']?([^""'\s,}]+)", RegexOptions.Compiled),
            new(@"(?i)\b[a-f0-9]{32,}\b", RegexOptions.Compiled), // Potential hash/key patterns
            new(@"(?i)\b[a-z0-9+/]{20,}={0,2}\b", RegexOptions.Compiled) // Base64 patterns
        };

        public SecureLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Logs an information message after sanitizing sensitive data.
        /// </summary>
        public void LogInformation(string message, params object[] args)
        {
            var sanitizedMessage = SanitizeMessage(message);
            var sanitizedArgs = SanitizeArgs(args);
            _logger.LogInformation(sanitizedMessage, sanitizedArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Logs a warning message after sanitizing sensitive data.
        /// </summary>
        public void LogWarning(string message, params object[] args)
        {
            var sanitizedMessage = SanitizeMessage(message);
            var sanitizedArgs = SanitizeArgs(args);
            _logger.LogWarning(sanitizedMessage, sanitizedArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Logs an error message after sanitizing sensitive data.
        /// </summary>
        public void LogError(Exception? exception, string message, params object[] args)
        {
            var sanitizedMessage = SanitizeMessage(message);
            var sanitizedArgs = SanitizeArgs(args);
            var sanitizedException = SanitizeException(exception);
            _logger.LogError(sanitizedException, sanitizedMessage, sanitizedArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Logs an error message after sanitizing sensitive data.
        /// </summary>
        public void LogError(string message, params object[] args)
        {
            var sanitizedMessage = SanitizeMessage(message);
            var sanitizedArgs = SanitizeArgs(args);
            _logger.LogError(sanitizedMessage, sanitizedArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Logs a debug message after sanitizing sensitive data.
        /// </summary>
        public void LogDebug(string message, params object[] args)
        {
            var sanitizedMessage = SanitizeMessage(message);
            var sanitizedArgs = SanitizeArgs(args);
            _logger.LogDebug(sanitizedMessage, sanitizedArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Logs a trace message after sanitizing sensitive data.
        /// </summary>
        public void LogTrace(string message, params object[] args)
        {
            var sanitizedMessage = SanitizeMessage(message);
            var sanitizedArgs = SanitizeArgs(args);
            _logger.LogTrace(sanitizedMessage, sanitizedArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Logs a critical message after sanitizing sensitive data.
        /// </summary>
        public void LogCritical(Exception? exception, string message, params object[] args)
        {
            var sanitizedMessage = SanitizeMessage(message);
            var sanitizedArgs = SanitizeArgs(args);
            var sanitizedException = SanitizeException(exception);
            _logger.LogCritical(sanitizedException, sanitizedMessage, sanitizedArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Sanitizes a log message by replacing sensitive data with safe placeholders.
        /// </summary>
        private static string SanitizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var sanitized = message;

            // Replace known sensitive patterns
            foreach (var pattern in SensitivePatterns)
            {
                sanitized = pattern.Replace(sanitized, match => 
                {
                    var fieldName = match.Groups[1].Value;
                    var value = match.Groups[2].Value;
                    var obfuscated = ObfuscateValue(value);
                    return $"{fieldName}: {obfuscated}";
                });
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes arguments by replacing sensitive values.
        /// </summary>
        private static object[]? SanitizeArgs(object[]? args)
        {
            if (args == null || args.Length == 0)
                return args;

            var sanitizedArgs = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                sanitizedArgs[i] = SanitizeObject(args[i]) ?? "[NULL]";
            }

            return sanitizedArgs;
        }

        /// <summary>
        /// Sanitizes an object by checking for sensitive data patterns.
        /// </summary>
        private static object? SanitizeObject(object? obj)
        {
            if (obj == null)
                return obj;

            switch (obj)
            {
                case string str:
                    return SanitizeMessage(str);
                
                case IDictionary<string, object> dict:
                    return SanitizeDictionary(dict);
                
                default:
                    // For complex objects, try to serialize and sanitize
                    try
                    {
                        var json = JsonSerializer.Serialize(obj);
                        var sanitizedJson = SanitizeMessage(json);
                        return sanitizedJson;
                    }
                    catch
                    {
                        // If serialization fails, return the object as-is
                        return obj;
                    }
            }
        }

        /// <summary>
        /// Sanitizes a dictionary by checking keys for sensitive field names.
        /// </summary>
        private static Dictionary<string, object> SanitizeDictionary(IDictionary<string, object> dict)
        {
            var sanitized = new Dictionary<string, object>();
            
            foreach (var kvp in dict)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                if (IsSensitiveField(key))
                {
                    sanitized[key] = ObfuscateValue(value?.ToString() ?? "");
                }
                else
                {
                    sanitized[key] = SanitizeObject(value) ?? "[NULL]";
                }
            }

            return sanitized;
        }

        /// <summary>
        /// Checks if a field name indicates sensitive data.
        /// </summary>
        private static bool IsSensitiveField(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return false;

            return SensitiveFields.Contains(fieldName) || 
                   fieldName.ContainsSensitiveData();
        }

        /// <summary>
        /// Obfuscates a sensitive value for safe logging.
        /// </summary>
        private static string ObfuscateValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "[EMPTY]";

            if (value.Length <= 4)
                return "[REDACTED]";

            if (value.Length <= 8)
                return $"{value[0]}***{value[^1]}";

            return SecureDataHandler.ObfuscateApiKey(value);
        }

        /// <summary>
        /// Sanitizes exception data to remove sensitive information.
        /// </summary>
        private static Exception? SanitizeException(Exception? exception)
        {
            if (exception == null)
                return null;

            // For now, return the exception as-is since exception sanitization
            // is complex and could break debugging. In production, you might
            // want to create new exceptions with sanitized messages.
            
            // TODO: Consider implementing exception message sanitization for production
            return exception;
        }

        /// <summary>
        /// Creates a secure logger from an existing ILogger instance.
        /// </summary>
        public static SecureLogger CreateSecureLogger(ILogger logger)
        {
            return new SecureLogger(logger);
        }

        /// <summary>
        /// Logs authentication events with special handling for sensitive data.
        /// </summary>
        public void LogAuthenticationEvent(string eventType, string userId, bool success, string? details = null)
        {
            var obfuscatedUserId = ObfuscateValue(userId);
            var sanitizedDetails = details != null ? SanitizeMessage(details) : "N/A";
            
            LogInformation("Authentication Event: {EventType} for user {UserId} - Success: {Success} - Details: {Details}",
                eventType, obfuscatedUserId, success, sanitizedDetails);
        }

        /// <summary>
        /// Logs API request events with automatic sanitization of sensitive headers and data.
        /// </summary>
        public void LogApiRequest(string method, string endpoint, bool success, int statusCode, string? error = null)
        {
            var sanitizedEndpoint = SanitizeMessage(endpoint);
            var sanitizedError = error != null ? SanitizeMessage(error) : null;

            if (success)
            {
                LogInformation("API Request: {Method} {Endpoint} - Status: {StatusCode}",
                    method, sanitizedEndpoint, statusCode);
            }
            else
            {
                LogWarning("API Request Failed: {Method} {Endpoint} - Status: {StatusCode} - Error: {Error}",
                    method, sanitizedEndpoint, statusCode, sanitizedError ?? "Unknown error");
            }
        }

        /// <summary>
        /// Logs configuration changes with sensitive data protection.
        /// </summary>
        public void LogConfigurationChange(string configKey, string? oldValue, string? newValue, string userId)
        {
            var obfuscatedUserId = ObfuscateValue(userId);
            
            if (IsSensitiveField(configKey))
            {
                LogInformation("Configuration Changed: {ConfigKey} - User: {UserId} - Values: [REDACTED]",
                    configKey, obfuscatedUserId);
            }
            else
            {
                LogInformation("Configuration Changed: {ConfigKey} - Old: {OldValue} - New: {NewValue} - User: {UserId}",
                    configKey, oldValue ?? "[NULL]", newValue ?? "[NULL]", obfuscatedUserId);
            }
        }
    }

    /// <summary>
    /// Extension methods for ILogger to add secure logging capabilities.
    /// </summary>
    public static class SecureLoggerExtensions
    {
        /// <summary>
        /// Creates a secure wrapper around the logger.
        /// </summary>
        public static SecureLogger AsSecure(this ILogger logger)
        {
            return new SecureLogger(logger);
        }

        /// <summary>
        /// Logs information with automatic sensitive data sanitization.
        /// </summary>
        public static void LogInformationSecure(this ILogger logger, string message, params object[] args)
        {
            var secureLogger = new SecureLogger(logger);
            secureLogger.LogInformation(message, args);
        }

        /// <summary>
        /// Logs warnings with automatic sensitive data sanitization.
        /// </summary>
        public static void LogWarningSecure(this ILogger logger, string message, params object[] args)
        {
            var secureLogger = new SecureLogger(logger);
            secureLogger.LogWarning(message, args);
        }

        /// <summary>
        /// Logs errors with automatic sensitive data sanitization.
        /// </summary>
        public static void LogErrorSecure(this ILogger logger, Exception? exception, string message, params object[] args)
        {
            var secureLogger = new SecureLogger(logger);
            secureLogger.LogError(exception, message, args);
        }
    }
}