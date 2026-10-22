using System.Text.RegularExpressions;

namespace FaraPortfolio.Tests;

/// <summary>
/// メンテナンスのお知らせの帯（.maintenance-notice）が、スクロールしてもヘッダーの真下に残ることを、CSS の書き方で確かめる。
/// 実際のスクロールは見ないので、ヘッダー（z-index 50）・本文（z-index 5 まで）との重なりと、ページ内ジャンプの位置を CSS の値で固定する。
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
    public void Notice_IsStickyJustBelowTheHeader()
    {
        var body = RuleBody(".maintenance-notice");
        Assert.Matches(@"position:\s*sticky;", body);
        // ヘッダーは下線 1px ぶん --header-h より高い
        Assert.Matches(@"top:\s*calc\(var\(--header-h\)\s*\+\s*1px\);", body);
        Assert.DoesNotMatch(@"position:\s*relative;", body);
    }

    [Fact]
    public void Notice_SitsBetweenTheBodyAndTheHeader()
    {
        var notice = int.Parse(Regex.Match(RuleBody(".maintenance-notice"), @"z-index:\s*(\d+);").Groups[1].Value);
        var header = int.Parse(Regex.Match(RuleBody(".site-header"), @"z-index:\s*(\d+);").Groups[1].Value);

        // 本文の要素で一番大きい z-index（ヒーローの吹き出し 5）より前、ヘッダー（50）より後ろ
        var bodyMax = Regex.Matches(Css, @"z-index:\s*(\d+);")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(z => z > 0 && z < 1000)
            .Where(z => z != header)
            .Where(z => z != 40)
            .Max();
        Assert.True(notice > bodyMax, $"帯の z-index（{notice}）が本文（最大 {bodyMax}）より大きくありません");
        Assert.True(notice < header, $"帯の z-index（{notice}）がヘッダー（{header}）より小さくありません");
    }

    [Fact]
    public void NoticeSpace_IsAddedOnlyWhenTheNoticeIsOnThePage()
    {
        // 帯の分の余白（--notice-h）は、帯があるページ（html:has(.maintenance-notice)）でだけ入る。普段は 0
        Assert.Matches(@"--notice-h:\s*0px;", RuleBody(":root"));
        Assert.Matches(@"html:has\(\.maintenance-notice\)\s*\{[^}]*--notice-h:\s*[\d.]+rem;", Css);
    }

    [Fact]
    public void PageJumps_StopBelowTheHeaderAndTheNoticeOnce()
    {
        // ページ内ジャンプは html の scroll-padding-top だけで決まる（作品側に scroll-margin-top を足すと二重に計上される）
        Assert.Matches(@"scroll-padding-top:\s*calc\(var\(--header-h\)\s*\+\s*var\(--notice-h\)\s*\+\s*16px\);", RuleBody("html"));
        Assert.DoesNotMatch("scroll-margin-top", RuleBody(".work-detail"));
    }

    [Fact]
    public void NoticeHeight_IsMeasuredByScript()
    {
        var js = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/js/site.js"));
        Assert.Contains("'--notice-h'", js);
        Assert.Contains("ResizeObserver", js);
        Assert.Contains(".maintenance-notice", js);
        // 帯が消えたら、測った値を外して CSS の既定（0）に戻す
        Assert.Contains("removeProperty('--notice-h')", js);
    }

    [Fact]
    public void Banner_IsRightAfterTheHeaderAndBeforeMain()
    {
        // 帯は SiteHeader の直後・<main> の前。順番が変わると、ヘッダーの下に固定できない
        var layout = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/Layout/MainLayout.razor"));
        Assert.Matches(@"<SiteHeader />\s*<MaintenanceBanner />\s*<main class=""site-main"">", layout);
    }
}
