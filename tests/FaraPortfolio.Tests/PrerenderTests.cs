using System.Net;
using FaraPortfolio.Prerender;
using FaraPortfolio.Web.Services;

namespace FaraPortfolio.Tests;

/// <summary>
/// 静的な本文（tools/PrerenderPages。kit の build-pages が入口ページに入れる）。JavaScript が無くても読める本文・フッター・リンクの形を確かめる。
/// 描画は kit の SiteLinks.CurrentHost（静的な値）を書き換えるので、同じコレクション（SiteLinks）で直列に動かす。
/// </summary>
[Collection("SiteLinks")]
public sealed class PrerenderTests : IAsyncLifetime
{
    private IReadOnlyList<PrerenderedPage> production = [];
    private IReadOnlyList<PrerenderedPage> develop = [];
    private PrerenderedPage notFound = default!;

    public async Task InitializeAsync()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var prod = await PagePrerenderer.RenderAllAsync(data, SiteInfo.ProductionHost);
        production = prod.Pages;
        notFound = prod.NotFound;
        develop = (await PagePrerenderer.RenderAllAsync(data, SiteInfo.DevelopHost)).Pages;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // プライバシーポリシーの制定日・最終改定日（改定したらここも更新する）
    private const string PolicyEnacted = "2026 年 10 月 22 日";
    private const string PolicyRevised = "2026 年 10 月 29 日";

    private static string Text(PrerenderedPage page) => WebUtility.HtmlDecode(page.BodyHtml);

    [Fact]
    public void Every_page_is_rendered_in_the_agreed_order_with_title_and_description()
    {
        Assert.Equal(["/", "/works", "/updates", "/report", "/privacy"], production.Select(p => p.Path));
        Assert.Equal("Fara Labs", production[0].Title);
        Assert.Equal("作ったもの | Fara Labs", production[1].Title);
        Assert.Equal("更新履歴 | Fara Labs", production[2].Title);
        Assert.Equal("問い合わせ | Fara Labs", production[3].Title);
        Assert.All(production, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));
    }

    [Fact]
    public void Body_starts_with_the_entry_static_root_so_the_boot_indicator_can_find_it()
    {
        // kit の build-pages（bodyRootClass）と読み込み表示の JS が、この class に依存する
        Assert.All(production, p => Assert.StartsWith("<div class=\"entry-static\">", p.BodyHtml));
    }

    [Fact]
    public void Layout_keeps_the_header_and_footer_fixed_around_the_scrolling_area()
    {
        Assert.All(production, p =>
        {
            var html = p.BodyHtml;
            var header = html.IndexOf("<header class=\"site-header\"", StringComparison.Ordinal);
            var scroll = html.IndexOf("<div class=\"site-scroll\"", StringComparison.Ordinal);
            var main = html.IndexOf("<main class=\"site-main\" id=\"main-content\" tabindex=\"-1\">", StringComparison.Ordinal);
            var mainEnd = html.IndexOf("</main>", main < 0 ? 0 : main, StringComparison.Ordinal);
            var footer = html.IndexOf("<footer class=\"fbc-footer\"", StringComparison.Ordinal);
            Assert.True(header >= 0 && scroll > header && main > scroll && mainEnd > main && footer > mainEnd,
                $"{p.Path}: ヘッダー → スクロールする領域（本文）→ フッター の順になっていません");
            // フッターは、スクロールする領域（.site-scroll）を閉じたあとに置く（画面の下に固定するため）
            Assert.Contains("</div>", html.Substring(mainEnd, footer - mainEnd));
        });
    }

    [Fact]
    public void Every_page_has_the_site_name_link_in_the_header()
    {
        Assert.All(production, p => Assert.Contains("<a class=\"brand\" href=\"/\">Fara Labs</a>", p.BodyHtml));
    }

    [Fact]
    public void Footer_has_the_agreed_parts_and_the_report_link_carries_the_page()
    {
        foreach (var page in production)
        {
            var html = Text(page);
            Assert.Contains("<span class=\"fbc-footer-name\">Fara Labs</span>", html);
            Assert.Contains("プライバシーポリシー", html);
            Assert.Contains("© 2026 ", html);
            Assert.Contains("href=\"https://github.com/fara1991\"", html);
        }
        Assert.Contains("href=\"/report?from=%2Fworks\"", Text(production[1]));
        // 問い合わせ・プライバシーのページ自身は、?from= を付けない
        Assert.DoesNotContain("?from=", Text(production[3]));
        Assert.DoesNotContain("?from=", Text(production[4]));
    }

    [Fact]
    public void Pages_have_no_blazor_runtime_markers_and_no_data_fetch()
    {
        Assert.All(production, p =>
        {
            Assert.DoesNotContain("_framework", p.BodyHtml);
            Assert.DoesNotContain("<!--!-->", p.BodyHtml);
            Assert.DoesNotContain("__internal_", p.BodyHtml);
        });
    }

    [Fact]
    public void Home_has_the_hero_works_and_updates_from_the_data_files()
    {
        var html = Text(production[0]);
        // 見出し（h1）は 1 つだけ。画面の先頭に、アイコン（アバター）と名前を出す（Blazor の描画と同じ構造）
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<h1[ >]"));
        Assert.Matches("<h1 class=\"hero-name name-with-avatar reveal\"[^>]*><img class=\"avatar\" src=\"/images/avatar.jpg\" alt(=\"\")? width=\"64\" height=\"64\"[^>]*/?>Fara</h1>", html);
        Assert.DoesNotContain("sr-only", html);
        Assert.Contains("管理者のFaraです。作ったもの置き場です。", html);
        // 中央はサイトアイコンの img（文字の「F」は使わない）
        Assert.Matches("<a class=\"core\" href=\"/works\" aria-label=\"作ったもの\">\\s*<img src=\"/favicon.svg\" alt(=\"\")? width=\"64\" height=\"64\"", html);
        Assert.DoesNotContain("<span>F</span>", html);
        Assert.Contains("class=\"orbit-hint\"", html);
        // ホームの下側に、作品を並べるブロック（旧 #works）は無い。作品へは、軌道のアイコンと中央のアイコン・ボタンから
        Assert.DoesNotContain("work-card", html);
        Assert.DoesNotContain("id=\"works\"", html);
        Assert.Contains("最近の更新", html);
        Assert.Contains("class=\"satellite", html);
    }

    [Fact]
    public void Profile_page_is_gone_and_home_has_no_link_buttons()
    {
        // プロフィールのページ（/profile）は無い。リンクのページ（/links）も無い（SNS はフッターだけ）
        var all = string.Concat(production.Select(Text));
        Assert.DoesNotContain("/profile", all);
        Assert.DoesNotContain(">プロフィール</a>", all);
        Assert.DoesNotContain("class=\"link-card", all);
        var home = Text(production[0]);
        Assert.DoesNotContain("リンク一覧", home);
        Assert.DoesNotContain("btn btn-ghost", home);
        Assert.DoesNotContain("social-pill", home);
        Assert.DoesNotContain("/links", all);
    }

    [Fact]
    public void Works_page_lists_every_work_with_its_id_anchor()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var html = Text(production[1]);
        Assert.All(data.Works, w =>
        {
            Assert.Contains($"id=\"{w.Id}\"", html);
            Assert.Contains(w.Title, html);
        });
    }

    [Fact]
    public void Updates_page_has_no_filter_buttons_and_no_new_chip_in_the_static_body()
    {
        // 絞り込みは、ブラウザで Blazor が動いてから（静的な本文では押せない）。NEW は、見る人の今日の日付で決める
        var html = Text(production[2]);
        Assert.DoesNotContain("filter-chip", html);
        Assert.DoesNotContain("chip-new", html);
        Assert.Contains("update-item", html);
    }

    [Fact]
    public void Report_page_is_the_same_form_the_browser_renders_so_the_heading_does_not_change_on_boot()
    {
        var html = Text(production[3]);
        Assert.Contains("<h1 class=\"fbc-page-title\">問い合わせ</h1>", html);   // kit v0.5.0 から、題名はページの h1
        Assert.DoesNotContain("<h3>問い合わせ</h3>", html);
        Assert.Contains("id=\"report-message\"", html);
        Assert.Contains("id=\"report-category\"", html);
        Assert.Contains("fbc-page-header fbc-page-column", html);
    }

    [Fact]
    public void Aria_current_marks_the_page_in_the_static_header()
    {
        Assert.Contains("href=\"/works\" aria-current=\"page\"", Text(production[1]));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(Text(production[1]), "aria-current=\"page\""));
    }

    [Fact]
    public void Skip_link_is_in_the_static_body()
    {
        Assert.All(production, p => Assert.Contains("class=\"skip-link\" href=\"https://fara-labs.com" + p.Path + "#main-content\"", Text(p)));
    }

    [Fact]
    public void Not_found_body_is_readable_without_javascript_and_has_no_report_origin_page()
    {
        var html = Text(notFound);
        Assert.StartsWith("<div class=\"entry-static\">", notFound.BodyHtml);
        Assert.Equal("ページが見つかりません | Fara Labs", notFound.Title);
        Assert.Contains("class=\"fbc-not-found-code\"", html);
        Assert.Contains("ページが見つかりません", html);
        Assert.Contains("href=\"/\"", html);
        // どのページでもない URL なので、問い合わせに ?from= を付けず、ヘッダーの現在地も無い
        Assert.DoesNotContain("?from=", html);
        Assert.DoesNotContain("aria-current", html);
    }

    [Fact]
    public void Privacy_policy_matches_what_the_site_stores_in_the_browser()
    {
        // プライバシーポリシーの記載（localStorage・Cookie は使わない。sessionStorage にスクロール位置だけ）が、実装と合っているか
        var policy = Text(production[4]);
        Assert.Contains("Cookie を使いません", policy);
        Assert.Contains("日本時間の 0 時ごろ", policy);
        var body = policy[policy.IndexOf("<article", StringComparison.Ordinal)..policy.IndexOf("</article>", StringComparison.Ordinal)];
        // 閲覧者に意味のない実装の詳細（保存の仕組みの名前・ハッシュ・トークン・ホスト名）は書かない
        foreach (var tech in new[] { "ハッシュ", "トークン", "localStorage", "sessionStorage", "fara-labs.com", "秘密の鍵" }) Assert.DoesNotContain(tech, body);
        // 運営者の約束・宣言や法的な注記は書かない
        foreach (var banned in new[] { "匿名", "閲覧しません", "受け取りません", "外部送信規律", "個人関連情報" }) Assert.DoesNotContain(banned, policy);
        Assert.DoesNotContain("報告", policy);
        // 送信回数の制限（送信元の識別子を、その日の間だけ保存する）の記載
        Assert.Contains("問い合わせは、1 日に 3 回までです", policy);
        Assert.Contains("IP アドレスそのものは保存しません", policy);
        Assert.Contains("タブを閉じるまで使う保存領域", policy);
        // 初出の用語の説明・内部用語を使わないこと・利用目的の節・広告の追記予告を載せないこと
        Assert.DoesNotContain("掃除", policy);
        Assert.Contains("利用目的", policy);
        Assert.Contains("EU 圏内", policy);
        Assert.DoesNotContain("追記します", policy);
        Assert.Contains("Cloudflare（Turnstile など）と Google Fonts は、それぞれの仕組みで、ブラウザに情報を保存することがあります。", policy);
        var siteJs = File.ReadAllText(FindUp("src/FaraPortfolio.Web/wwwroot/js/site.js"));
        Assert.Contains("sessionStorage", siteJs);
        Assert.DoesNotContain("localStorage", siteJs);
        Assert.DoesNotContain("document.cookie", siteJs);
        var webSources = Directory.EnumerateFiles(Path.GetDirectoryName(FindUp("src/FaraPortfolio.Web/Program.cs"))!, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".razor")) && !f.EndsWith("Privacy.razor") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        Assert.All(webSources, f => Assert.DoesNotContain("localStorage", File.ReadAllText(f)));
    }

    private static string FindUp(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void Privacy_page_has_the_policy_in_the_wide_column()
    {
        var html = Text(production[4]);
        Assert.Contains("class=\"page container wide fbc-page-column fbc-page-column--wide\"", html);
        Assert.Contains("ブラウザに保存する情報", html);
        Assert.Contains("Cloudflare Turnstile", html);
        Assert.Contains(PolicyEnacted, html);
        Assert.Contains(PolicyRevised, html);
        // 外部サービスの表の列見出しと送信先
        foreach (var header in new[] { "送信先", "送られる情報", "目的", "送られるとき" }) Assert.Contains(header, html);
        foreach (var name in new[] { "Google Fonts", "Cloudflare Web Analytics", "Cloudflare Turnstile", "Discord" }) Assert.Contains(name, html);
    }

    [Fact]
    public void Develop_render_points_fara_labs_links_to_the_develop_site_but_production_does_not()
    {
        // 作者のホームページ（フッター）と、作品のリンク（pokemon.fara-labs.com など）
        Assert.Contains("href=\"https://dev.fara-labs.com/\"", Text(develop[0]));
        Assert.Contains("pokemon-dev.fara-labs.com", Text(develop[1]));
        Assert.DoesNotContain("//pokemon.fara-labs.com", Text(develop[1]));
        Assert.Contains("href=\"https://fara-labs.com/\"", Text(production[0]));
        Assert.Contains("//pokemon.fara-labs.com", Text(production[1]));
    }
}
