using FaraPortfolio.Web.Models;
using FaraPortfolio.Web.Services;
using FaraBlazorComponent.Core;

namespace FaraPortfolio.Tests;

/// <summary>
/// 確認用ホストでのリンクの置き換え（kit の SiteLinks）・フッターの SNS・「今日」から NEW の判定・ページの題名。
/// kit の SiteLinks は静的な値（DevelopHost・CurrentHost）を持つので、触るテストは同じコレクションにして、並列に動かさない。
/// </summary>
[Collection("SiteLinks")]
public class SiteLinksTests
{
    public SiteLinksTests() => SiteLinks.DevelopHost = SiteInfo.DevelopHost;

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
    public void Localize_follows_the_current_host()
    {
        SiteLinks.CurrentHost = "dev.fara-labs.com";
        Assert.Equal("https://dev.fara-labs.com/", SiteLinks.Localize("https://fara-labs.com/"));
        SiteLinks.CurrentHost = "fara-labs.com";
        Assert.Equal("https://fara-labs.com/", SiteLinks.Localize("https://fara-labs.com/"));
    }

    [Fact]
    public void Footer_socials_come_from_the_profile_and_skip_empty_urls_and_unknown_services()
    {
        var profile = new Profile
        {
            Name = "Fara",
            Socials =
            [
                new SocialLink { Service = "github", Label = "GitHub", Url = "https://github.com/fara1991" },
                new SocialLink { Service = "youtube", Label = "YouTube", Url = "https://youtube.com/@x" },
                new SocialLink { Service = "twitch", Label = "Twitch", Url = "" },
                new SocialLink { Service = "Discord", Label = "Discord", Url = "https://discord.com/invite/x" },
            ],
        };
        var options = new SiteOptions();
        SiteInfo.Configure(options, profile);

        Assert.Equal(["github", "discord"], options.Social.Select(s => s.Service));
        Assert.Equal("Fara Labs", options.Name);
        Assert.Equal(SiteInfo.DevelopHost, options.DevelopHost);
    }

    [Fact]
    public void Profile_json_socials_are_all_supported_by_the_footer()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "profile.json"));
        var profile = System.Text.Json.JsonSerializer.Deserialize<Profile>(json, SiteJson.Options)!;
        var options = new SiteOptions();
        SiteInfo.Configure(options, profile);

        // フッターは kit が絵柄を持つサービスだけ。profile.json の SNS（URL が入っているもの）が消えていたら、kit に絵柄を足す
        Assert.Equal(profile.VisibleSocials.Select(s => s.Service), options.Social.Select(s => s.Service));
    }

    [Theory]
    [InlineData("2026-10-23", "2026-10-23", true)]
    [InlineData("2026-10-23", "2026-10-09", true)]
    [InlineData("2026-10-23", "2026-10-08", false)]
    [InlineData("2026-10-23", "2026-10-24", false)]
    public void New_chip_is_for_updates_within_14_days_of_the_browsers_today(string today, string date, bool expected)
    {
        var clock = new BrowserClock { Today = DateOnly.Parse(today) };
        Assert.Equal(expected, clock.IsNew(DateOnly.Parse(date)));
    }

    [Fact]
    public void New_chip_is_never_shown_without_a_browser_date() =>
        Assert.False(new BrowserClock().IsNew(new DateOnly(2026, 10, 23)));

    [Fact]
    public void Page_titles_follow_the_profile_name()
    {
        var profile = new Profile { Name = "Fara", Tagline = "t", Bio = "b" };
        Assert.Equal("Fara Labs", PageCatalog.Home(profile).Title);
        Assert.Equal("プロフィール | Fara Labs", PageCatalog.ProfilePage(profile).Title);
        Assert.Equal("作ったもの | Fara Labs", PageCatalog.Works(profile).Title);
        Assert.Equal("更新履歴 | Fara Labs", PageCatalog.Updates(profile).Title);
        Assert.Equal("リンク | Fara Labs", PageCatalog.Links(profile).Title);
        Assert.Equal("問い合わせ | Fara Labs", PageCatalog.Report().Title);
        Assert.Equal("プライバシーポリシー | Fara Labs", PageCatalog.Privacy().Title);
        Assert.All(PageCatalog.All(profile), p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));
    }
}
