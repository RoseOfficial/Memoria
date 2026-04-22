using System.Text;
using System.Web;

namespace AlphaScopeServer;

/// <summary>
/// Converts PostgreSQL URI-style connection strings (as handed out by Neon, Supabase,
/// Railway, etc.) to the key-value form that Npgsql's parser expects.
/// </summary>
internal static class ConnectionStringHelper
{
    public static string NormalizeForNpgsql(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        if (!raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var host = uri.Host;
        var port = uri.IsDefaultPort ? 5432 : uri.Port;

        var sb = new StringBuilder();
        sb.Append("Host=").Append(host).Append(';');
        sb.Append("Port=").Append(port).Append(';');
        sb.Append("Database=").Append(database).Append(';');
        sb.Append("Username=").Append(username).Append(';');
        sb.Append("Password=").Append(password).Append(';');

        if (!string.IsNullOrEmpty(uri.Query))
        {
            var qs = HttpUtility.ParseQueryString(uri.Query);
            foreach (var key in qs.AllKeys)
            {
                if (key is null) continue;
                var value = qs[key];
                if (string.IsNullOrEmpty(value)) continue;

                switch (key.ToLowerInvariant())
                {
                    case "sslmode":
                        sb.Append("SSL Mode=").Append(NormalizeSslMode(value)).Append(';');
                        break;
                    case "channel_binding":
                        sb.Append("Channel Binding=").Append(NormalizeChannelBinding(value)).Append(';');
                        break;
                    case "application_name":
                        sb.Append("Application Name=").Append(value).Append(';');
                        break;
                    case "connect_timeout":
                        sb.Append("Timeout=").Append(value).Append(';');
                        break;
                    default:
                        sb.Append(key).Append('=').Append(value).Append(';');
                        break;
                }
            }
        }

        return sb.ToString();
    }

    private static string NormalizeSslMode(string value) => value.ToLowerInvariant() switch
    {
        "disable" => "Disable",
        "allow" => "Allow",
        "prefer" => "Prefer",
        "require" => "Require",
        "verify-ca" => "VerifyCA",
        "verify-full" => "VerifyFull",
        _ => value,
    };

    private static string NormalizeChannelBinding(string value) => value.ToLowerInvariant() switch
    {
        "disable" => "Disable",
        "prefer" => "Prefer",
        "require" => "Require",
        _ => value,
    };
}
