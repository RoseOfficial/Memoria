using System.Security.Cryptography;
using System.Text;

namespace MemoriaServer.Services.Auth
{
    /// <summary>
    /// HMAC-SHA256 signs and verifies the `state` param for Discord OAuth redirect flow.
    /// Payload format: "{returnTo}|{nonce}|{base64url-sig}"
    /// </summary>
    public sealed class OAuthStateSigner
    {
        private readonly byte[] _key;

        public OAuthStateSigner(string base64Key)
        {
            _key = Convert.FromBase64String(base64Key);
            if (_key.Length < 16)
                throw new ArgumentException("State signing key must be at least 16 bytes (base64-decoded).", nameof(base64Key));
        }

        public string Sign(string returnTo, string nonce)
        {
            var payload = $"{returnTo}|{nonce}";
            using var hmac = new HMACSHA256(_key);
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return $"{payload}|{Base64UrlEncode(sig)}";
        }

        public bool Verify(string state, out string returnTo, out string nonce)
        {
            returnTo = string.Empty;
            nonce = string.Empty;

            var parts = state.Split('|');
            if (parts.Length != 3) return false;

            var payload = $"{parts[0]}|{parts[1]}";
            using var hmac = new HMACSHA256(_key);
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            byte[] provided;
            try { provided = Base64UrlDecode(parts[2]); }
            catch { return false; }

            if (!CryptographicOperations.FixedTimeEquals(expected, provided))
                return false;

            returnTo = parts[0];
            nonce = parts[1];
            return true;
        }

        private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] Base64UrlDecode(string s)
        {
            var padded = s.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight((padded.Length + 3) & ~3, '=');
            return Convert.FromBase64String(padded);
        }
    }
}
