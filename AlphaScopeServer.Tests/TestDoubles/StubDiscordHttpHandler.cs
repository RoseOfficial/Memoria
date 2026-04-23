using System.Net;
using System.Net.Http;
using System.Text;

namespace AlphaScopeServer.Tests.TestDoubles;

/// <summary>
/// HTTP message handler that fakes the three Discord API endpoints the OAuth callback hits:
/// POST /oauth2/token, GET /users/@me, GET /users/@me/guilds. Configurable per-test.
/// </summary>
public sealed class StubDiscordHttpHandler : HttpMessageHandler
{
    public string? TokenResponse { get; set; } = """{"access_token":"fake-at","token_type":"Bearer","expires_in":604800,"refresh_token":"fake-rt","scope":"identify guilds"}""";
    public string? UserResponse { get; set; } = """{"id":"111222333444555666","username":"testuser","discriminator":"0001"}""";
    public string? GuildsResponse { get; set; } = """[{"id":"999888777","name":"AlphaScope"}]""";
    public HttpStatusCode TokenStatus { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode UserStatus { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode GuildsStatus { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/oauth2/token"))
            return Respond(TokenStatus, TokenResponse);
        if (path.EndsWith("/users/@me/guilds"))
            return Respond(GuildsStatus, GuildsResponse);
        if (path.EndsWith("/users/@me"))
            return Respond(UserStatus, UserResponse);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static Task<HttpResponseMessage> Respond(HttpStatusCode status, string? body)
    {
        var resp = new HttpResponseMessage(status);
        if (body is not null)
            resp.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return Task.FromResult(resp);
    }
}
