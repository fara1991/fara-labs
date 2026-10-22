using System.Net;
using FaraPortfolio.Web.Models;
using FaraPortfolio.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FaraPortfolio.Tests;

/// <summary>書き出すページ（HTML）の中身。JS が無くても読める本文・ページごとの title・canonical・フッター・報告フォームの形を確かめる。</summary>
public sealed class StaticPageTests(SiteFactory factory) : IClassFixture<SiteFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        AllowAutoRedirect = false,
    });

    private async Task<string> GetAsync(string path, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(expected, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static readonly (string Path, string Title, string Canonical)[] Pages =
    [
        ("/", "Fara | Portfolio", "https://fara-labs.com/"),
        ("/profile", "Profile | Fara", "https://fara-labs.com/profile"),
        ("/works", "Works | Fara", "https://fara-labs.com/works"),
        ("/updates", "Updates | Fara", "https://fara-labs.com/updates"),
        ("/links", "Links | Fara", "https://fara-labs.com/links"),
        ("/report", "問い合わせ | Fara Portfolio", "https://fara-labs.com/report"),
        ("/privacy", "プライバシーポリシー | Fara Portfolio", "https://fara-labs.com/privacy"),
    ];

    public static IEnumerable<object[]> PageCases() => Pages.Select(p => new object[] { p.Path, p.Title, p.Canonical });

    [Theory]
    [MemberData(nameof(PageCases))]
    public async Task Each_page_has_its_own_title_description_and_canonical(string path, string title, string canonical)
    {
        var html = await GetAsync(path);
        Assert.Contains($"<title>{title}</title>", html);
        Assert.Matches("<meta name=\"description\" content=\"[^\"]+\" />", html);
        Assert.Contains($"<link rel=\"canonical\" href=\"{canonical}\" />", html);
        Assert.Contains($"<meta property=\"og:url\" content=\"{canonical}\" />", html);
        Assert.Contains($"<meta property=\"og:title\" content=\"{title}\" />", html);
    }

    [Theory]
    [MemberData(nameof(PageCases))]
    public async Task Pages_are_static_html_without_the_blazor_runtime(string path, string title, string canonical)
    {
        var html = await GetAsync(path);
        Assert.DoesNotContain("_framework", html);
        Assert.DoesNotContain("blazor.", html);
        Assert.DoesNotContain("<!--!-->", html);
        Assert.Contains("<script src=\"/js/site.js\" defer></script>", html);
    }

    [Theory]
    [MemberData(nameof(PageCases))]
    public async Task Layout_keeps_the_header_fixed_and_the_notice_slot_before_the_scrolling_area(string path, string title, string canonical)
    {
        var html = await GetAsync(path);
        var header = html.IndexOf("<header class=\"site-header\"", StringComparison.Ordinal);
        var slot = html.IndexOf("<div class=\"maintenance-slot\"></div>", StringComparison.Ordinal);
        var scroll = html.IndexOf("<div class=\"site-scroll\"", StringComparison.Ordinal);
        var main = html.IndexOf("<main class=\"site-main\">", StringComparison.Ordinal);
        var footer = html.IndexOf("<footer class=\"site-footer\"", StringComparison.Ordinal);
        Assert.True(header >= 0 && slot > header && scroll > slot && main > scroll && footer > main,
            "ヘッダー → お知らせの帯の場所 → スクロールする領域（本文 → フッター）の順になっていません");
    }

    [Theory]
    [MemberData(nameof(PageCases))]
    public async Task Footer_has_the_agreed_parts(string path, string title, string canonical)
    {
        var html = await GetAsync(path);
        Assert.Contains("<span class=\"site-footer-name\">Fara Portfolio</span>", html);
        Assert.Contains("<a href=\"/privacy\">プライバシーポリシー</a>", html);
        Assert.Contains("© 2026 <a href=\"https://fara-labs.com/\">Fara</a>", html);
    }

    [Fact]
    public async Task The_footer_report_link_carries_the_page_it_is_on()
    {
        var html = await GetAsync("/works");
        Assert.Contains("<a href=\"/report?from=%2Fworks\">問い合わせ</a>", html);
    }

    [Fact]
    public async Task The_active_menu_item_is_marked_on_its_own_page()
    {
        var html = await GetAsync("/works");
        Assert.Contains("<a class=\"nav-link active\" href=\"/works\" aria-current=\"page\">Works</a>", html);
        Assert.DoesNotContain("<a class=\"nav-link active\" href=\"/profile\"", html);
    }

    [Fact]
    public async Task Works_page_has_an_anchor_for_every_work()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var html = await GetAsync("/works");
        foreach (var work in data.Works)
        {
            Assert.Contains($"<article id=\"{work.Id}\"", html);
        }
    }

    [Fact]
    public async Task Home_page_links_each_work_orbit_icon_to_its_anchor()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var html = await GetAsync("/");
        foreach (var work in data.Works)
        {
            Assert.Contains($"href=\"/works#{work.Id}\"", html);
        }
        Assert.Equal(data.Works.Count, CountOccurrences(html, "class=\"satellite "));
    }

    [Fact]
    public async Task Home_page_shows_the_newest_updates_first()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var html = await GetAsync("/");
        Assert.Equal(Math.Min(3, data.Updates.Count), CountOccurrences(html, "<li class=\"update-item\""));
    }

    [Fact]
    public async Task Updates_page_lists_every_update_with_its_category_and_date_for_the_script()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var html = await GetAsync("/updates");
        Assert.Equal(data.Updates.Count, CountOccurrences(html, "<li class=\"update-item\""));
        // 絞り込みの欄と NEW は、script が付ける（書き出し時には隠しておく）
        if (data.Updates.Select(u => u.Category).Distinct().Count() >= 2)
        {
            Assert.Contains("data-filter-bar hidden", html);
        }
        Assert.Contains("<span class=\"chip chip-new\" data-new hidden>NEW</span>", html);
        Assert.Contains("<p class=\"muted\" data-filter-empty hidden>", html);
    }

    [Fact]
    public async Task Report_page_has_the_form_with_the_limits_and_is_disabled_until_the_script_enables_it()
    {
        var html = await GetAsync("/report");
        Assert.Contains("data-min=\"5\" data-max=\"2000\" data-max-contact=\"200\"", html);
        Assert.Contains("<select id=\"report-category\" name=\"category\">", html);
        Assert.Contains("<option value=\"bug\">不具合・記事の間違い</option>", html);
        Assert.Contains("<option value=\"request\">要望</option>", html);
        Assert.Contains("<option value=\"other\">その他</option>", html);
        Assert.Contains("maxlength=\"2000\"", html);
        Assert.Contains("id=\"report-submit\" disabled", html);
        Assert.Contains("id=\"report-disabled\" role=\"status\" hidden", html);
        Assert.Contains("<input type=\"text\" name=\"website\"", html);
        // Blazor の静的 SSR が付ける action は、送信できない（submit は JS が有効にするまで無効）ので害はない。トークンは付かない
        Assert.DoesNotContain("__RequestVerificationToken", html);
        Assert.Contains("/js/report.js", html);
    }

    [Fact]
    public async Task The_404_page_is_served_with_404_and_noindex()
    {
        var html = await GetAsync("/no-such-page", HttpStatusCode.NotFound);
        Assert.Contains("ページが見つかりません", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html);
        Assert.DoesNotContain("<link rel=\"canonical\"", html);
        // 404 のページは、どの URL でも同じ HTML なので、報告のリンクに書き出し時のパスを入れない
        Assert.Contains("<a href=\"/report\">問い合わせ</a>", html);
    }

    [Fact]
    public async Task Works_links_on_the_production_site_are_not_rewritten()
    {
        var html = await GetAsync("/works");
        Assert.Contains("https://pokemon.fara-labs.com/", html);
        Assert.DoesNotContain("-dev.fara-labs.com", html);
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}

/// <summary>確認用（dev.fara-labs.com）の書き出し: 配下のリンクを確認用の URL に置き換える。</summary>
public sealed class DevelopSiteTests(DevelopSiteFactory factory) : IClassFixture<DevelopSiteFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        AllowAutoRedirect = false,
    });

    [Fact]
    public async Task Links_under_fara_labs_com_point_to_the_develop_site()
    {
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/works"));
        Assert.Contains("https://pokemon-dev.fara-labs.com/", html);
        Assert.Contains("href=\"https://dev.fara-labs.com/\"", html);
        Assert.DoesNotContain("https://pokemon.fara-labs.com/", html);
    }

    [Fact]
    public async Task Other_sites_links_stay_as_they_are()
    {
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/links"));
        Assert.Contains("https://github.com/fara1991", html);
    }
}
