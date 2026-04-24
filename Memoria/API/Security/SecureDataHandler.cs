using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Memoria.API.Security
{
    /// <summary>
    /// Secure utility class for handling sensitive data including API keys, 
    /// with proper encryption, obfuscation, and logging safety measures.
    /// 
    /// Security Features:
    /// - AES-256-GCM encryption for data at rest
    /// - Memory-safe string handling
    /// - Automatic key derivation from machine-specific entropy
    /// - Logging exclusions for sensitive data
    /// - Secure disposal of sensitive data
    /// </summary>
    public static class SecureDataHandler
    {
        private static readonly byte[] _entropy = GetMachineEntropy();
        private const int KeySize = 32; // 256-bit key
        private const int NonceSize = 12; // 96-bit nonce for GCM
        private const int TagSize = 16; // 128-bit authentication tag

        /// <summary>
        /// Encrypts sensitive data using AES-256-GCM with machine-specific key derivation.
        /// </summary>
        /// <param name="plaintext">The sensitive data to encrypt</param>
        /// <returns>Base64-encoded encrypted data with nonce and authentication tag</returns>
        public static string EncryptSensitiveData(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;

            try
            {
                using var aes = new AesGcm(DeriveKey(), TagSize);
                
                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var nonce = new byte[NonceSize];
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[TagSize];

                RandomNumberGenerator.Fill(nonce);
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

                // Combine nonce + ciphertext + tag
                var result = new byte[NonceSize + ciphertext.Length + TagSize];
                Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
                Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
                Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

                // Clear sensitive data from memory
                Array.Clear(plaintextBytes, 0, plaintextBytes.Length);
                Array.Clear(ciphertext, 0, ciphertext.Length);

                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                // Log error without exposing sensitive data
                Plugin.Log?.Error($"Failed to encrypt sensitive data: {ex.GetType().Name}");
                throw new InvalidOperationException("Encryption operation failed", ex);
            }
        }

        /// <summary>
        /// Decrypts sensitive data that was encrypted with EncryptSensitiveData.
        /// </summary>
        /// <param name="encryptedData">Base64-encoded encrypted data</param>
        /// <returns>Decrypted plaintext data</returns>
        public static string DecryptSensitiveData(string encryptedData)
        {
            if (string.IsNullOrEmpty(encryptedData))
                return string.Empty;

            try
            {
                var data = Convert.FromBase64String(encryptedData);
                
                if (data.Length < NonceSize + TagSize)
                    throw new ArgumentException("Invalid encrypted data format");

                using var aes = new AesGcm(DeriveKey(), TagSize);
                
                var nonce = new byte[NonceSize];
                var ciphertext = new byte[data.Length - NonceSize - TagSize];
                var tag = new byte[TagSize];
                var plaintext = new byte[ciphertext.Length];

                Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);
                Buffer.BlockCopy(data, NonceSize, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(data, NonceSize + ciphertext.Length, tag, 0, TagSize);

                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                var result = Encoding.UTF8.GetString(plaintext);

                // Clear sensitive data from memory
                Array.Clear(plaintext, 0, plaintext.Length);
                Array.Clear(ciphertext, 0, ciphertext.Length);
                Array.Clear(data, 0, data.Length);

                return result;
            }
            catch (Exception ex)
            {
                // Log error without exposing sensitive data
                Plugin.Log?.Error($"Failed to decrypt sensitive data: {ex.GetType().Name}");
                throw new InvalidOperationException("Decryption operation failed", ex);
            }
        }

        /// <summary>
        /// Creates an obfuscated representation of an API key for safe logging and display.
        /// Shows only the first 4 and last 4 characters, replacing the middle with asterisks.
        /// </summary>
        /// <param name="apiKey">The API key to obfuscate</param>
        /// <returns>Obfuscated API key safe for logging</returns>
        public static string ObfuscateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "[EMPTY_KEY]";

            if (apiKey.Length <= 8)
                return new string('*', apiKey.Length);

            return $"{apiKey[..4]}{"*".PadLeft(Math.Max(4, apiKey.Length - 8), '*')}{apiKey[^4..]}";
        }

        /// <summary>
        /// Securely clears sensitive string data from memory by overwriting with random data.
        /// Note: This provides best-effort security; .NET string immutability limits effectiveness.
        /// </summary>
        /// <param name="sensitiveData">The string containing sensitive data to clear</param>
        public static void SecureClearString(string sensitiveData)
        {
            if (string.IsNullOrEmpty(sensitiveData))
                return;

            try
            {
                // In .NET, strings are immutable, so we can't truly clear them from memory.
                // This is a best-effort approach that may help in some scenarios.
                // For truly sensitive operations, use SecureString or byte arrays.
                unsafe
                {
                    fixed (char* ptr = sensitiveData)
                    {
                        for (int i = 0; i < sensitiveData.Length; i++)
                        {
                            ptr[i] = '\0';
                        }
                    }
                }
            }
            catch
            {
                // Silently fail - clearing is best effort
            }
        }

        /// <summary>
        /// Validates that an API key meets security requirements.
        /// </summary>
        /// <param name="apiKey">The API key to validate</param>
        /// <returns>True if the API key meets security standards</returns>
        public static bool IsValidApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            // Check minimum length (should be at least 16 characters)
            if (apiKey.Length < 16)
                return false;

            // Check for common weak patterns
            if (apiKey.All(c => c == apiKey[0])) // All same character
                return false;

            if (apiKey.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                apiKey.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                apiKey.Contains("password", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Creates a logging-safe representation of sensitive data for debug purposes.
        /// </summary>
        /// <param name="sensitiveData">The sensitive data to make logging-safe</param>
        /// <param name="contextInfo">Additional context information (non-sensitive)</param>
        /// <returns>Safe string for logging that doesn't expose sensitive data</returns>
        public static string CreateLoggingSafeRepresentation(string sensitiveData, string contextInfo = "")
        {
            if (string.IsNullOrEmpty(sensitiveData))
                return $"[EMPTY_DATA]{(string.IsNullOrEmpty(contextInfo) ? "" : $" ({contextInfo})")}";

            var hash = ComputeSafeHash(sensitiveData);
            var length = sensitiveData.Length;
            
            return $"[DATA_HASH:{hash[..8]}][LENGTH:{length}]{(string.IsNullOrEmpty(contextInfo) ? "" : $" ({contextInfo})")}";
        }

        /// <summary>
        /// Computes a non-cryptographic hash for logging and identification purposes.
        /// </summary>
        private static string ComputeSafeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input + "Memoria_Salt"));
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Derives an encryption key from machine-specific entropy.
        /// </summary>
        private static byte[] DeriveKey()
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(_entropy, _entropy, 10000, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(KeySize);
        }

        /// <summary>
        /// Generates machine-specific entropy for key derivation.
        /// </summary>
        private static byte[] GetMachineEntropy()
        {
            try
            {
                // Combine multiple sources of machine-specific data
                var machineId = Environment.MachineName;
                var osVersion = Environment.OSVersion.ToString();
                var processorCount = Environment.ProcessorCount.ToString();
                var userDomain = Environment.UserDomainName;
                
                var combined = $"{machineId}|{osVersion}|{processorCount}|{userDomain}|Memoria";
                
                using var sha256 = SHA256.Create();
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            }
            catch
            {
                // Fallback to a static salt if machine entropy fails
                return SHA256.HashData(Encoding.UTF8.GetBytes("Memoria_Fallback_Entropy_2024"));
            }
        }

        /// <summary>
        /// Extension method to check if a string contains sensitive data patterns.
        /// </summary>
        public static bool ContainsSensitiveData(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var sensitivePatterns = new[]
            {
                "key", "token", "password", "secret", "auth", "credential",
                "api_key", "apikey", "bearer", "authorization"
            };

            return sensitivePatterns.Any(pattern => 
                input.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Secure wrapper for API key operations with automatic encryption/decryption.
    /// </summary>
    public class SecureApiKey : IDisposable
    {
        private string? _encryptedKey;
        private bool _disposed;

        public SecureApiKey(string apiKey)
        {
            if (!SecureDataHandler.IsValidApiKey(apiKey))
                throw new ArgumentException("Invalid API key format");

            _encryptedKey = SecureDataHandler.EncryptSensitiveData(apiKey);
            
            // Clear the input parameter from memory (best effort)
            SecureDataHandler.SecureClearString(apiKey);
        }

        /// <summary>
        /// Gets the API key in plaintext form. Use sparingly and clear immediately after use.
        /// </summary>
        public string GetPlaintextKey()
        {
            if (_disposed || string.IsNullOrEmpty(_encryptedKey))
                throw new ObjectDisposedException(nameof(SecureApiKey));

            return SecureDataHandler.DecryptSensitiveData(_encryptedKey);
        }

        /// <summary>
        /// Gets an obfuscated version of the API key safe for logging.
        /// </summary>
        public string GetObfuscatedKey()
        {
            if (_disposed || string.IsNullOrEmpty(_encryptedKey))
                return "[DISPOSED]";

            try
            {
                var plaintext = GetPlaintextKey();
                var obfuscated = SecureDataHandler.ObfuscateApiKey(plaintext);
                SecureDataHandler.SecureClearString(plaintext);
                return obfuscated;
            }
            catch
            {
                return "[ERROR]";
            }
        }

        /// <summary>
        /// Validates the stored API key without exposing it.
        /// </summary>
        public bool IsValid()
        {
            if (_disposed || string.IsNullOrEmpty(_encryptedKey))
                return false;

            try
            {
                var plaintext = GetPlaintextKey();
                var isValid = SecureDataHandler.IsValidApiKey(plaintext);
                SecureDataHandler.SecureClearString(plaintext);
                return isValid;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed && !string.IsNullOrEmpty(_encryptedKey))
            {
                SecureDataHandler.SecureClearString(_encryptedKey);
                _encryptedKey = null;
                _disposed = true;
            }
        }
    }
}