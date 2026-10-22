using System.Text.RegularExpressions;
using FaraPortfolio.Web.Components;
using FaraPortfolio.Web.Pages;
using FaraPortfolio.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace FaraPortfolio.Tests;

/// <summary>4 サイト共通の形のフッター（サイト名 | 問い合わせ | プライバシーポリシー | © 公開年 Fara）と、問い合わせ・プライバシーポリシーのページ。</summary>
public class FooterAndReportTests
{
    private sealed class TestNav : NavigationManager
    {
        public TestNav(string uri) => Initialize("https://fara-labs.com/", uri);
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    /// <summary>描画だけを確かめるので、JavaScript は呼ばれない（呼ばれたら失敗する）。</summary>
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException($"JS は呼ばれないはず: {identifier}");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new InvalidOperationException($"JS は呼ばれないはず: {identifier}");
    }

    private static async Task<string> RenderAsync<T>(string uri = "https://fara-labs.com/works") where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NavigationManager>(new TestNav(uri));
        services.AddSingleton(new SiteData());
        services.AddSingleton<IJSRuntime>(new NoJs());
        services.AddSingleton(new HttpClient());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<T>()).ToHtmlString());
    }

    /// <summary>HTML から、指定した id（または class）の要素の中の文字だけを取り出す（タグは消し、空白は 1 つにする）。</summary>
    private static string TextOf(string html, string openTag)
    {
        var start = html.IndexOf(openTag, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{openTag} が見つかりません");
        var inner = html[(start + openTag.Length)..];
        inner = inner[..inner.IndexOf("</div>", StringComparison.Ordinal)];
        return Regex.Replace(System.Net.WebUtility.HtmlDecode(Regex.Replace(inner, "<[^>]+>", " ")), @"\s+", " ").Trim();
    }

    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public async Task Footer_line_has_the_agreed_parts_in_order()
    {
        var html = await RenderAsync<SiteFooter>();
        Assert.Equal("Fara Portfolio | 問い合わせ | プライバシーポリシー | © 2026 Fara", TextOf(html, "<div class=\"site-footer-line\">"));
    }

    [Fact]
    public async Task Footer_links_go_to_the_report_page_the_privacy_page_and_the_author_homepage()
    {
        var html = await RenderAsync<SiteFooter>("https://fara-labs.com/works");
        Assert.Contains("<span class=\"site-footer-name\">Fara Portfolio</span>", html);
        Assert.DoesNotContain("href=\"/\"", html);
        Assert.Contains("href=\"/report?from=%2Fworks\"", html);
        Assert.Contains("href=\"/privacy\"", html);
        Assert.Contains("href=\"https://fara-labs.com/\">Fara</a>", html);
    }

    [Fact]
    public async Task Separators_are_hidden_from_screen_readers()
    {
        var html = await RenderAsync<SiteFooter>();
        Assert.Equal(3, Regex.Matches(html, "<span class=\"site-footer-sep\" aria-hidden=\"true\">\\|</span>").Count);
    }

    [Fact]
    public async Task Report_link_on_the_report_page_has_no_from()
    {
        var html = await RenderAsync<SiteFooter>("https://fara-labs.com/report");
        Assert.Contains("href=\"/report\"", html);
        Assert.DoesNotContain("from=", html);
    }

    [Theory]
    [InlineData("/works", "/report?from=%2Fworks")]
    [InlineData("/", "/report?from=%2F")]
    [InlineData("/a/b_c-d.e", "/report?from=%2Fa%2Fb_c-d.e")]
    [InlineData("/report", "/report")]
    [InlineData("/privacy", "/report")]
    [InlineData("/x y", "/report")]
    [InlineData("//evil.example", "/report")]
    [InlineData("/<script>", "/report")]
    [InlineData("", "/report")]
    public void Report_link_carries_the_current_page_only_when_it_is_safe(string path, string expected) =>
        Assert.Equal(expected, SiteFooterLinks.ReportHref(path));

    [Fact]
    public void A_too_long_path_is_not_carried() => Assert.Equal("/report", SiteFooterLinks.ReportHref("/" + new string('a', 300)));

    [Fact]
    public void Site_info_has_the_agreed_name_year_and_author()
    {
        Assert.Equal("Fara Portfolio", SiteInfo.Name);
        Assert.Equal(2026, SiteInfo.PublishedYear);
        Assert.Equal("© 2026 Fara", SiteInfo.Copyright);
        Assert.Equal("https://fara-labs.com/", SiteInfo.PortfolioUrl);
    }

    [Fact]
    public void The_worker_names_the_same_site_as_the_footer()
    {
        // 報告の通知のサイト名（worker/index.js）と、画面に出すサイト名が食い違わないように
        var worker = File.ReadAllText(RepoFile("worker/index.js"));
        Assert.Contains($"handleReport(request, env, \"{SiteInfo.Name}\")", worker);
    }

    [Fact]
    public async Task Report_page_has_the_form_the_script_expects()
    {
        var html = await RenderAsync<Report>();
        foreach (var id in new[] { "report-category", "report-message", "report-contact", "report-turnstile" })
            Assert.Contains($"id=\"{id}\"", html);
        // 種類の ID は、サーバー（worker/report.js の CATEGORIES）と同じ
        Assert.Contains("value=\"bug\"", html);
        Assert.Contains("value=\"request\"", html);
        Assert.Contains("value=\"other\"", html);
        Assert.Contains("maxlength=\"2000\"", html);
        // 罠の欄は、人には見えない（画面の外）。入力の候補にも出さない
        Assert.Contains("report-trap", html);
        Assert.Contains("tabindex=\"-1\"", html);
        Assert.Contains("autocomplete=\"off\"", html);
        Assert.Contains("href=\"/privacy\"", html);
    }

    [Fact]
    public async Task Report_page_shows_the_form_before_sending()
    {
        var html = await RenderAsync<Report>();
        Assert.Contains("<form", html);
        Assert.DoesNotContain("report-done", html);
    }

    [Fact]
    public void Index_includes_report_js_before_the_blazor_script()
    {
        var index = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/index.html"));
        var report = index.IndexOf("js/report.js", StringComparison.Ordinal);
        Assert.True(report > 0, "js/report.js を読み込んでいません");
        Assert.True(report < index.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Privacy_page_covers_the_topics()
    {
        var html = await RenderAsync<Privacy>();
        var text = Regex.Replace(System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ");
        foreach (var topic in new[] { "運営者", "ブラウザに保存する情報", "アクセス解析", "問い合わせで送る情報", "Turnstile", "Discord", "外部のサービス", "広告", "第三者への提供", "Google Fonts", "Cloudflare" })
            Assert.Contains(topic, text);
    }

    [Fact]
    public async Task Privacy_page_states_cookies_only_as_a_limited_claim()
    {
        var text = Regex.Replace(System.Net.WebUtility.HtmlDecode(Regex.Replace(await RenderAsync<Privacy>(), "<[^>]+>", " ")), @"\s+", " ");
        // このサイトのコードは保存しない、と限定している（断定の「保存しません」だけにしない）
        Assert.Contains("このサイトのコードは", text);
        Assert.Contains("localStorage・sessionStorage・Cookie などに、お客さまの情報を保存しません", text);
        // Cookie は Cloudflare・Turnstile の仕組みで使われることがある、と条件つきで書く
        Assert.Contains("Cloudflare・Turnstile の仕組みで Cookie が使われることがあります", text);
        // アクセス解析は、使う場合がある、と条件つきで書く（リポジトリだけでは有効かどうか確認できないため）
        Assert.Contains("Web Analytics）を使っています", text);
        Assert.Contains("EU 圏内からのアクセスは、計測の対象外", text);
        Assert.DoesNotContain("Web Analytics を使って", text);
    }

    [Fact]
    public async Task Header_has_report_and_policy_links_in_english()
    {
        var html = await RenderAsync<SiteHeader>("https://fara-labs.com/works");
        // 属性の順に依存しない: href と class を、どちらが先でも見つける
        Assert.Matches(@"<a\b(?=[^>]*\bhref=""report"")(?=[^>]*\bclass=""nav-link"")[^>]*>Report</a>", html);
        Assert.Matches(@"<a\b(?=[^>]*\bhref=""privacy"")(?=[^>]*\bclass=""nav-link"")[^>]*>Policy</a>", html);
    }

    [Fact]
    public void Footer_social_services_have_icons_in_the_agreed_order()
    {
        // フッターの SNS アイコンは、プロフィールの SNS（URL が入っているもの）を、その順に並べる。GitHub・Twitter・Twitch・Discord
        var profile = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/data/profile.json"));
        var order = new[] { "github", "twitter", "twitch", "discord" };
        var positions = order.Select(s => profile.IndexOf($"\"service\": \"{s}\"", StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p > 0));
        Assert.Equal(positions.OrderBy(p => p), positions);
        foreach (var service in order)
            Assert.Equal(service, Icon.ForService(service));
    }

    [Fact]
    public void Discord_entry_links_to_the_invite_in_a_new_tab_with_a_safe_rel()
    {
        var profile = System.Text.Json.JsonSerializer.Deserialize<FaraPortfolio.Web.Models.Profile>(File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/data/profile.json")), FaraPortfolio.Web.Models.SiteJson.Options)!;
        var discord = profile.Socials.Single(s => s.Service == "discord");
        Assert.Equal("Discord", discord.Label);
        Assert.Equal("https://discord.com/invite/B38eCSYyk5", discord.Url);
        // 外部のリンクなので、フッターと SNS のページは別のタブで開き、rel を付ける（Links.razor・SiteFooter.razor の並び）
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", File.ReadAllText(RepoFile("src/FaraPortfolio.Web/Components/SiteFooter.razor")));
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", File.ReadAllText(RepoFile("src/FaraPortfolio.Web/Pages/Links.razor")));
    }

    [Fact]
    public void Discord_icon_and_brand_color_exist()
    {
        Assert.Equal("discord", Icon.ForService("Discord"));
        Assert.Contains(".service-discord { --brand: #5865f2; }", File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/css/app.css")));
        Assert.Contains("[\"discord\"] = ", File.ReadAllText(RepoFile("src/FaraPortfolio.Web/Components/Icon.razor")));
    }

    [Fact]
    public void Narrow_screens_hide_the_separators_and_keep_each_link_on_one_line()
    {
        var css = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/css/app.css")).Replace("\r\n", "\n");
        // 狭い画面（720px 以下）だけ、区切りを隠して項目の間を gap で空ける
        Assert.Contains("@media (max-width: 720px) {\n    .site-footer-line { gap: 6px 16px; }\n    .site-footer-sep { display: none; }\n}", css);
        // リンクと名前・コピーライトは、途中で改行しない
        Assert.Contains(".site-footer-line a { color: inherit; text-decoration: underline; text-underline-offset: 3px; white-space: nowrap; }", css);
        Assert.Contains(".site-footer-copy { white-space: nowrap; }", css);
        // PC では区切りを残す（区切りを消す規則は、画面幅の条件の中にだけある）
        Assert.DoesNotMatch(@"(?m)^\.site-footer-sep \{[^}]*display:\s*none", css);
    }
}
