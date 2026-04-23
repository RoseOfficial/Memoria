using FluentAssertions;
using AlphaScopeServer.Services.Auth;

namespace AlphaScopeServer.Tests.Services;

public class OAuthStateSignerTests
{
    private const string Key = "ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0";

    [Fact]
    public void SignAndVerify_RoundTrip_Succeeds()
    {
        var signer = new OAuthStateSigner(Key);
        var state = signer.Sign("https://app.example.com/me", "noncevalue");

        var ok = signer.Verify(state, out var returnTo, out var nonce);

        ok.Should().BeTrue();
        returnTo.Should().Be("https://app.example.com/me");
        nonce.Should().Be("noncevalue");
    }

    [Fact]
    public void Verify_TamperedSignature_Fails()
    {
        var signer = new OAuthStateSigner(Key);
        var state = signer.Sign("https://app.example.com/me", "n1");
        var tampered = state.Substring(0, state.Length - 3) + "AAA";

        var ok = signer.Verify(tampered, out _, out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void Verify_DifferentKey_Fails()
    {
        var a = new OAuthStateSigner(Key);
        var b = new OAuthStateSigner("ZGlmZmVyZW50LWtleS1oZXJlLXRvdGFsLW9mLTMyLWJ5dGU=");

        var state = a.Sign("https://app.example.com/me", "n");
        var ok = b.Verify(state, out _, out _);

        ok.Should().BeFalse();
    }
}
