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

    private static string Text(PrerenderedPage page) => WebUtility.HtmlDecode(page.BodyHtml);

    [Fact]
    public void Every_page_is_rendered_in_the_agreed_order_with_title_and_description()
    {
        Assert.Equal(["/", "/profile", "/works", "/updates", "/links", "/report", "/privacy"], production.Select(p => p.Path));
        Assert.Equal("Fara | Portfolio", production[0].Title);
        Assert.Equal("Works | Fara", production[2].Title);
        Assert.Equal("問い合わせ | Fara Portfolio", production[5].Title);
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
            Assert.Contains("<span class=\"fbc-footer-name\">Fara Portfolio</span>", html);
            Assert.Contains("プライバシーポリシー", html);
            Assert.Contains("© 2026 ", html);
            Assert.Contains("href=\"https://github.com/fara1991\"", html);
        }
        Assert.Contains("href=\"/report?from=%2Fworks\"", Text(production[2]));
        // 問い合わせ・プライバシーのページ自身は、?from= を付けない
        Assert.DoesNotContain("?from=", Text(production[5]));
        Assert.DoesNotContain("?from=", Text(production[6]));
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
        // 以前の 2 行の大きな見出し（hero-title）は出さず、控えめな大きさの h1 を 1 つだけ画面に出す
        Assert.DoesNotContain("hero-title", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<h1[ >]"));
        Assert.Contains("Fara's Portfolio</h1>", html);
        Assert.DoesNotContain("<h1 class=\"sr-only\">", html);
        Assert.Contains("class=\"orbit-hint\"", html);
        Assert.Contains("class=\"work-card", html);
        Assert.Contains("最近の更新", html);
        Assert.Contains("class=\"satellite", html);
    }

    [Fact]
    public void Works_page_lists_every_work_with_its_id_anchor()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var html = Text(production[2]);
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
        var html = Text(production[3]);
        Assert.DoesNotContain("filter-chip", html);
        Assert.DoesNotContain("chip-new", html);
        Assert.Contains("update-item", html);
    }

    [Fact]
    public void Report_page_is_the_same_form_the_browser_renders_so_the_heading_does_not_change_on_boot()
    {
        var html = Text(production[5]);
        Assert.Contains("<h1 class=\"fbc-page-title\">問い合わせ</h1>", html);   // kit v0.5.0 から、題名はページの h1
        Assert.DoesNotContain("<h3>問い合わせ</h3>", html);
        Assert.Contains("id=\"report-message\"", html);
        Assert.Contains("id=\"report-category\"", html);
        Assert.Contains("fbc-page-header fbc-page-column", html);
    }

    [Fact]
    public void Aria_current_marks_the_page_in_the_static_header()
    {
        Assert.Contains("href=\"/works\" aria-current=\"page\"", Text(production[2]));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(Text(production[2]), "aria-current=\"page\""));
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
        Assert.Equal("ページが見つかりません | Fara", notFound.Title);
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
        var policy = Text(production[6]);
        Assert.Contains("localStorage・Cookie などに保存しません", policy);
        // 送信回数の制限（送信元の識別子を、その日の間だけ保存する）の記載
        Assert.Contains("1 日に 3 回までに制限しています", policy);
        Assert.Contains("IP アドレスそのものは保存しません", policy);
        Assert.Contains("sessionStorage", policy);
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
        var html = Text(production[6]);
        Assert.Contains("class=\"page container wide fbc-page-column fbc-page-column--wide\"", html);
        Assert.Contains("ブラウザに保存する情報", html);
        Assert.Contains("Cloudflare Turnstile", html);
    }

    [Fact]
    public void Develop_render_points_fara_labs_links_to_the_develop_site_but_production_does_not()
    {
        // 作者のホームページ（フッター）と、作品のリンク（pokemon.fara-labs.com など）
        Assert.Contains("href=\"https://dev.fara-labs.com/\"", Text(develop[0]));
        Assert.Contains("pokemon-dev.fara-labs.com", Text(develop[2]));
        Assert.DoesNotContain("//pokemon.fara-labs.com", Text(develop[2]));
        Assert.Contains("href=\"https://fara-labs.com/\"", Text(production[0]));
        Assert.Contains("//pokemon.fara-labs.com", Text(production[2]));
    }
}
