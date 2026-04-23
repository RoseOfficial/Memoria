using System.Security.Cryptography;

namespace AlphaScopeServer.Services.Auth
{
    /// <summary>
    /// Generates Crockford base32 codes for claim verification and account linking.
    /// Format "AS-XXXX-XXXX" for claims, "AL-XXXX-XXXX" for account link codes. Crockford
    /// alphabet omits I/L/O/U to prevent misreads when the user types the code from bio.
    /// </summary>
    public static class ClaimCodeGenerator
    {
        // Crockford base32 alphabet — no I/L/O/U to avoid visual ambiguity.
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string GenerateClaimCode() => Generate("AS");
        public static string GenerateLinkCode() => Generate("AL");

        private static string Generate(string prefix)
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);

            Span<char> chars = stackalloc char[8];
            for (int i = 0; i < 8; i++)
                chars[i] = Alphabet[bytes[i] & 0x1F]; // low 5 bits

            return $"{prefix}-{chars.Slice(0, 4)}-{chars.Slice(4, 4)}";
        }
    }
}
