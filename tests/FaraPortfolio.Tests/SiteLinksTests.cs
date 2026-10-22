using FaraPortfolio.Web.Publishing;
using FaraPortfolio.Web.Services;
using Xunit;

namespace FaraPortfolio.Tests;

/// <summary>リンクの置き換え（確認用の書き出しだけ）・フッターの問い合わせのリンク・robots.txt / sitemap.xml の中身。</summary>
public class SiteLinksTests
{
    [Theory]
    [InlineData("https://pokemon.fara-labs.com/", "https://pokemon-dev.fara-labs.com/")]
    [InlineData("https://avatar.fara-labs.com/guide", "https://avatar-dev.fara-labs.com/guide")]
    [InlineData("https://twitch-bot.fara-labs.com/", "https://twitch-bot-dev.fara-labs.com/")]
    [InlineData("https://fara-labs.com/", "https://dev.fara-labs.com/")]
    [InlineData("https://fara-labs.com/works?x=1#a", "https://dev.fara-labs.com/works?x=1#a")]
    [InlineData("https://pokemon-dev.fara-labs.com/", "https://pokemon-dev.fara-labs.com/")]
    [InlineData("https://dev.fara-labs.com/", "https://dev.fara-labs.com/")]
    [InlineData("https://github.com/fara1991", "https://github.com/fara1991")]
    [InlineData("https://example.org/", "https://example.org/")]
    [InlineData("/works", "/works")]
    [InlineData("mailto:someone@example.com", "mailto:someone@example.com")]
    public void Develop_host_rewrites_only_fara_labs_links(string url, string expected) =>
        Assert.Equal(expected, SiteLinks.Localize(url, "dev.fara-labs.com"));

    [Theory]
    [InlineData("https://pokemon.fara-labs.com/")]
    [InlineData("https://fara-labs.com/")]
    public void Production_host_keeps_links_as_they_are(string url) =>
        Assert.Equal(url, SiteLinks.Localize(url, "fara-labs.com"));

    [Fact]
    public void Site_options_localize_follows_the_host() =>
        Assert.Equal("https://dev.fara-labs.com/", new SiteOptions("dev.fara-labs.com").Localize("https://fara-labs.com/"));

    [Theory]
    [InlineData("/works", "/report?from=%2Fworks")]
    [InlineData("/", "/report?from=%2F")]
    [InlineData("/a/b_c-d.e", "/report?from=%2Fa%2Fb_c-d.e")]
    [InlineData("/report", "/report")]
    [InlineData("/privacy", "/report")]
    [InlineData("/not-found", "/report")]
    [InlineData("/x y", "/report")]
    [InlineData("//evil.example", "/report")]
    [InlineData("/<script>", "/report")]
    [InlineData("", "/report")]
    public void Report_link_carries_the_current_page_only_when_it_is_safe(string path, string expected) =>
        Assert.Equal(expected, SiteFooterLinks.ReportHref(path));

    [Fact]
    public void A_too_long_path_is_not_carried() =>
        Assert.Equal("/report", SiteFooterLinks.ReportHref("/" + new string('a', 300)));

    [Fact]
    public void Production_robots_points_to_the_sitemap()
    {
        var robots = StaticSiteExporter.BuildRobots(new SiteOptions(SiteOptions.ProductionHost));
        Assert.Contains("Sitemap: https://fara-labs.com/sitemap.xml", robots);
    }

    [Fact]
    public void Develop_robots_has_no_sitemap()
    {
        var robots = StaticSiteExporter.BuildRobots(new SiteOptions("dev.fara-labs.com"));
        Assert.DoesNotContain("Sitemap:", robots);
    }

    [Fact]
    public void Sitemap_lists_each_page_on_the_production_origin()
    {
        var sitemap = StaticSiteExporter.BuildSitemap(new SiteOptions(SiteOptions.ProductionHost), ["/", "/works"]);
        Assert.Contains("<loc>https://fara-labs.com/</loc>", sitemap);
        Assert.Contains("<loc>https://fara-labs.com/works</loc>", sitemap);
    }
}
