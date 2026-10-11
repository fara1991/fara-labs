using FaraPortfolio.Web.Services;

namespace FaraPortfolio.Tests;

public class SiteLinksTests
{
    [Theory]
    [InlineData("https://pokemon.fara-labs.com/", "https://pokemon-dev.fara-labs.com/")]
    [InlineData("https://avatar.fara-labs.com/learn/basics", "https://avatar-dev.fara-labs.com/learn/basics")]
    [InlineData("https://fara-labs.com/works", "https://dev.fara-labs.com/works")]
    [InlineData("https://avatar.fara-labs.com/learn/%E5%9F%BA%E6%9C%AC?q=a%20b", "https://avatar-dev.fara-labs.com/learn/%E5%9F%BA%E6%9C%AC?q=a%20b")]
    [InlineData("https://github.com/fara1991/fara-avatar-guide", "https://github.com/fara1991/fara-avatar-guide")]
    [InlineData("/works", "/works")]
    [InlineData("https://pokemon-dev.fara-labs.com/", "https://pokemon-dev.fara-labs.com/")]
    public void Develop_RewritesFaraLabsLinks(string url, string expected) =>
        Assert.Equal(expected, SiteLinks.Localize(url, "dev.fara-labs.com"));

    [Theory]
    [InlineData("fara-labs.com")]
    [InlineData("localhost")]
    public void Production_KeepsLinks(string host) =>
        Assert.Equal("https://pokemon.fara-labs.com/", SiteLinks.Localize("https://pokemon.fara-labs.com/", host));
}
