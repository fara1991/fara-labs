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

    public async Task InitializeAsync()
    {
        var data = SiteData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        production = await PagePrerenderer.RenderAllAsync(data, SiteInfo.ProductionHost);
        develop = await PagePrerenderer.RenderAllAsync(data, SiteInfo.DevelopHost);
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
            var footer = html.IndexOf("<footer class=\"fsk-footer\"", StringComparison.Ordinal);
            Assert.True(header >= 0 && scroll > header && main > scroll && mainEnd > main && footer > mainEnd,
                $"{p.Path}: ヘッダー → スクロールする領域（本文）→ フッター の順になっていません");
            // フッターは、スクロールする領域（.site-scroll）を閉じたあとに置く（画面の下に固定するため）
            Assert.Contains("</div>", html.Substring(mainEnd, footer - mainEnd));
        });
    }

    [Fact]
    public void Footer_has_the_agreed_parts_and_the_report_link_carries_the_page()
    {
        foreach (var page in production)
        {
            var html = Text(page);
            Assert.Contains("<span class=\"fsk-footer-name\">Fara Portfolio</span>", html);
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
        Assert.Contains("Portfolio", html);
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
    public void Report_page_is_readable_without_javascript()
    {
        var html = Text(production[5]);
        Assert.Contains("問い合わせ", html);
        Assert.Contains("JavaScript", html);
        Assert.Contains("href=\"/links\"", html);
    }

    [Fact]
    public void Privacy_page_has_the_policy_in_the_wide_column()
    {
        var html = Text(production[6]);
        Assert.Contains("class=\"page container wide fsk-page-column fsk-page-column--wide\"", html);
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
