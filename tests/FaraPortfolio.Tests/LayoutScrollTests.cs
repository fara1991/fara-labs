using System.Text.RegularExpressions;

namespace FaraPortfolio.Tests;

/// <summary>
/// ヘッダーを固定し、その下の領域（.site-scroll）だけがスクロールする画面の枠を、配置・CSS・スクリプトの書き方で確かめる。
/// 実際のスクロールは見ない（ブラウザでの確認は PR の確認項目）。
/// </summary>
public class LayoutScrollTests
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
    private static readonly string Js = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/wwwroot/js/site.js"));
    private static readonly string Layout = File.ReadAllText(RepoFile("src/FaraPortfolio.Web/Layout/MainLayout.razor"));

    /// <summary>セレクタの本体（{ } の中身）を取る。同じセレクタが複数あれば、すべてつなげて返す。</summary>
    private static string RuleBody(string selector)
    {
        var pattern = "(?m)^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}";
        var joined = string.Join("\n", Regex.Matches(Css, pattern).Select(m => m.Groups[1].Value));
        Assert.False(string.IsNullOrWhiteSpace(joined), $"CSS に {selector} のルールがありません");
        return joined;
    }

    [Fact]
    public void The_frame_is_exactly_the_viewport_and_the_window_does_not_scroll()
    {
        var shell = RuleBody(".app-shell");
        Assert.Matches(@"height:\s*100vh;\s*height:\s*100dvh;", shell);
        Assert.Matches(@"overflow:\s*hidden;", shell);
        Assert.Matches(@"flex-direction:\s*column;", shell);
        // 端末の下部バーの伸縮に合わせる。#app も同じ高さの下限にする（ウィンドウがはみ出してスクロールしないように）
        Assert.Matches(@"#app\s*\{[^}]*min-height:\s*100dvh;", Css);
    }

    [Fact]
    public void Header_stays_in_the_frame_and_is_not_sticky()
    {
        var header = RuleBody(".site-header");
        Assert.Matches(@"position:\s*relative;", header);
        Assert.DoesNotMatch(@"position:\s*sticky;", header);
        // 進捗の線は、ヘッダーの上端に付く（ヘッダーが position: relative で基準になる）
        Assert.Matches(@"position:\s*absolute;", RuleBody(".scroll-progress"));
    }

    [Fact]
    public void The_scroll_area_is_the_only_scrolling_element_and_keeps_anchors_below_its_top()
    {
        var area = RuleBody(".site-scroll");
        Assert.Matches(@"overflow-y:\s*auto;", area);
        Assert.Matches(@"min-height:\s*0;", area);
        Assert.Matches(@"flex:\s*1\s+1\s+auto;", area);
        Assert.Matches(@"scroll-padding-top:\s*16px;", area);
        // 窓（html）側には、ページ内ジャンプの余白も、スクロールの動きも置かない（領域の中で完結させる）
        var html = RuleBody("html");
        Assert.DoesNotMatch("scroll-padding-top", html);
        Assert.DoesNotMatch("scroll-behavior", html);
        Assert.Matches(@"scroll-behavior:\s*smooth;", area);
    }

    [Fact]
    public void Layout_puts_the_banner_outside_the_scroll_area_and_the_footer_inside_it()
    {
        var scroll = Layout.IndexOf("<div class=\"site-scroll\">", StringComparison.Ordinal);
        Assert.True(scroll > 0, "MainLayout に .site-scroll がありません");
        Assert.True(Layout.IndexOf("<main class=\"site-main\">", StringComparison.Ordinal) > scroll);
        // フッターは、スクロールの中（末尾）に入る
        Assert.True(Layout.IndexOf("<SiteFooter />", StringComparison.Ordinal) > scroll);
        Assert.Contains("<div class=\"app-shell\">", Layout);
    }

    [Fact]
    public void Script_reads_scroll_position_from_the_scroll_area_not_the_window()
    {
        Assert.Contains("document.querySelector('.site-scroll')", Js);
        Assert.Contains("el.scrollTop", Js);
        // is-scrolled（ヘッダーの見た目）と進捗の線は、この領域の値で決める
        Assert.Contains("root.classList.toggle('is-scrolled', y > 8)", Js);
        // reveal の監視も、この領域を root にする
        Assert.Contains("{ root: container,", Js);
        // スクロールは泡立たないので、capture で受け取る
        Assert.Contains("addEventListener('scroll', onScroll, { passive: true, capture: true })", Js);
    }

    [Fact]
    public void Navigating_to_another_page_resets_the_scroll_area_but_not_on_hash_jumps()
    {
        Assert.Contains("resetScroll()", Js);
        Assert.Contains("if (location.hash) return;", Js);
        Assert.Contains("scrollTo({ top: 0, behavior: 'instant' })", Js);
        Assert.Contains("faraSite.resetScroll", Layout);
        // 同じページ内の移動（パスが同じ）では戻さない
        Assert.Contains("if (path != _path)", Layout);
    }

    [Fact]
    public void Blazor_and_scripts_agree_on_the_hooks()
    {
        // 配置の class と、スクリプトが探す class は同じ名前
        Assert.Contains("class=\"site-scroll\"", Layout);
        Assert.Contains("'.site-scroll'", Js);
        Assert.Contains("class=\"app-shell\"", Layout);
    }
}
