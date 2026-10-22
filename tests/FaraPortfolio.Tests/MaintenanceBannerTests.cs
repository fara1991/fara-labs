using System.Text.RegularExpressions;

namespace FaraPortfolio.Tests;

/// <summary>
/// メンテナンスのお知らせの帯（.maintenance-notice）の位置を、CSS と配置（MainLayout）の書き方で確かめる。
/// 帯は、ヘッダーとスクロールする領域（.site-scroll）の間に置く。スクロールの外なので、ヘッダーの下で動かずに残る。
/// 実際のスクロールは見ないので、配置の順番と CSS の値で固定する。
/// </summary>
public class MaintenanceBannerTests
{
    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    private static readonly string Css = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/css/app.css"));

    /// <summary>セレクタの本体（{ } の中身）を取る。同じセレクタが複数あれば、すべてつなげて返す。</summary>
    private static string RuleBody(string selector)
    {
        var pattern = "(?m)^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}";
        var bodies = Regex.Matches(Css, pattern).Select(m => m.Groups[1].Value);
        var joined = string.Join("\n", bodies);
        Assert.False(string.IsNullOrWhiteSpace(joined), $"CSS に {selector} のルールがありません");
        return joined;
    }

    [Fact]
    public void Notice_IsAFixedRowBetweenTheHeaderAndTheScrollArea()
    {
        var body = RuleBody(".maintenance-notice");
        // 帯は画面の枠（.app-shell）の 2 段目の行。スクロールの外なので、sticky にしない
        Assert.Matches(@"flex:\s*none;", body);
        Assert.DoesNotMatch(@"position:\s*sticky;", body);
    }

    [Fact]
    public void Banner_IsRightAfterTheHeaderAndBeforeTheScrollArea()
    {
        // 帯は SiteHeader の直後・スクロールする領域（.site-scroll）の前。順番が変わると、スクロールの中に入ってしまう
        var layout = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/Layout/MainLayout.razor"));
        Assert.Matches(@"<SiteHeader />\s*<MaintenanceBanner />\s*<div class=""site-scroll""", layout);
    }

    [Fact]
    public void NoticeHeight_IsNoLongerAddedToThePageJumpOffset()
    {
        // 帯はスクロールの外にあるので、ページ内ジャンプの位置に帯の高さを足さない（二重に計上しない）
        Assert.DoesNotMatch(@"--notice-h", Css);
        var js = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/js/site.js"));
        Assert.DoesNotMatch("notice-h", js);
        Assert.DoesNotMatch("ResizeObserver", js);
    }

    [Fact]
    public void Notice_label_is_the_first_line_and_the_text_is_on_the_next_line()
    {
        var banner = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/Layout/MaintenanceBanner.razor"));
        Assert.Matches(@"<strong class=""maintenance-notice-label"">【お知らせ】</strong><span class=""maintenance-notice-text"">@Info\.NoticeText</span>", banner);
        // 見出しと本文は、それぞれ行の箱（block）にする。太字は帯から継承する
        var css = Css.Replace("\r\n", "\n");
        Assert.Contains(".maintenance-notice-label, .maintenance-notice-text { display: block; }", css);
        Assert.Matches(@"\.maintenance-notice \{[^}]*font-weight:\s*700;", css);
    }
}
