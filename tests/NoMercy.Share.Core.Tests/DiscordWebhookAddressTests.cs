using NoMercy.Share.Core.Discord;

namespace NoMercy.Share.Core.Tests;

public sealed class DiscordWebhookAddressTests
{
    [Theory]
    [InlineData("https://discord.com/api/webhooks/000/made-up-token")]
    [InlineData("https://discordapp.com/api/webhooks/000/made-up-token")]
    [InlineData("https://canary.discord.com/api/webhooks/000/made-up-token")]
    [InlineData("https://ptb.discord.com/api/webhooks/000/made-up-token")]
    [InlineData("  https://discord.com/api/webhooks/000/made-up-token  ")]
    public void ADiscordWebhookAddressIsAccepted(string text)
    {
        Assert.True(DiscordWebhookAddress.TryParse(text, out Uri? address));
        Assert.Equal("/api/webhooks/000/made-up-token", address.AbsolutePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an address")]
    [InlineData("http://discord.com/api/webhooks/000/made-up-token")]
    [InlineData("https://discord.com.example.test/api/webhooks/000/made-up-token")]
    [InlineData("https://notdiscord.com/api/webhooks/000/made-up-token")]
    [InlineData("https://example.test/api/webhooks/000/made-up-token")]
    [InlineData("https://discord.com/api/v10/channels/000")]
    [InlineData("https://discord.com/api/webhooksx/000/made-up-token")]
    [InlineData("https://discord.com/")]
    [InlineData("https://user:pass@discord.com/api/webhooks/000/made-up-token")]
    [InlineData("https://discord.com:8443/api/webhooks/000/made-up-token")]
    [InlineData("https://127.0.0.1/api/webhooks/000/made-up-token")]
    public void AnythingElseIsRefused(string text)
    {
        Assert.False(DiscordWebhookAddress.TryParse(text, out Uri? address));
        Assert.Null(address);
    }
}
