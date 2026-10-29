using Bunit;
using FaraPortfolio.Web.Components;
using FaraPortfolio.Web.Models;
using FaraPortfolio.Web.Pages;
using FaraPortfolio.Web.Services;
using FaraBlazorComponent.Razor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FaraPortfolio.Tests;

/// <summary>
/// ブラウザで動く部品（bUnit）: ヘッダーの現在地（aria-current）・メニューのフォーカス・スキップリンク・更新履歴の絞り込みと件数・NEW。
/// kit の SiteLinks など静的な設定に触れるので、同じコレクションで直列に動かす。
/// </summary>
[Collection("SiteLinks")]
public sealed class ComponentTests : BunitContext
{
    public ComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var profile = new FaraPortfolio.Web.Models.Profile { Name = "Fara", Tagline = "t", Bio = "b" };
        Services.AddFaraBlazorComponent(options => SiteInfo.Configure(options, profile));
        Services.AddSingleton(new BrowserClock { Today = new DateOnly(2026, 10, 23) });
        Services.AddSingleton(new RenderEnvironment(IsInteractive: true));
        Services.AddSingleton(new SiteData
        {
            Profile = profile,
            Works = [],
            Updates =
            [
                new UpdateEntry { Date = new DateOnly(2026, 10, 23), Title = "今日のサイト更新", Category = "site" },
                new UpdateEntry { Date = new DateOnly(2026, 10, 1), Title = "作品の更新", Category = "work" },
                new UpdateEntry { Date = new DateOnly(2025, 12, 1), Title = "古いお知らせ", Category = "news" },
            ],
        });
    }

    private void NavigateTo(string path) => Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);

    [Fact]
    public void Header_marks_only_the_current_page_with_aria_current()
    {
        NavigateTo("/works");
        var cut = Render<SiteHeader>();

        var current = cut.FindAll("a.nav-link[aria-current='page']");
        Assert.Single(current);
        Assert.Equal("作ったもの", current[0].TextContent.Trim());
        Assert.Contains("active", current[0].ClassName);
        Assert.Equal(6, cut.FindAll("a.nav-link:not([aria-current])").Count);
    }

    [Fact]
    public void Header_has_the_site_name_link_to_home_at_the_left()
    {
        NavigateTo("/works");
        var cut = Render<SiteHeader>();

        var brand = cut.Find("a.brand");
        Assert.Equal("/", brand.GetAttribute("href"));
        Assert.Equal("Fara Labs", brand.TextContent.Trim());
        // ナビ・メニューボタンより前（左）に置く。ナビの aria-current の数は変わらない
        var inner = cut.Find(".header-inner");
        Assert.Equal("brand", inner.Children[0].ClassName);
        Assert.Single(cut.FindAll("a[aria-current='page']"));
    }

    [Fact]
    public void Home_has_one_hidden_h1_and_the_site_icon_in_the_core()
    {
        NavigateTo("/");
        var cut = Render<Home>();

        // h1 は 1 つ。画面には出さない（.sr-only）ので、大きな見出し（hero-heading）は無い
        var h1 = cut.FindAll("h1");
        Assert.Single(h1);
        Assert.Equal("sr-only", h1[0].ClassName);
        Assert.Equal("Fara's Portfolio", h1[0].TextContent.Trim());
        Assert.Empty(cut.FindAll(".hero-heading"));

        // 軌道の中央は文字の「F」ではなく、サイトアイコン（装飾。大きさを指定してレイアウトのずれを出さない）
        var core = cut.Find(".core");
        Assert.Equal("true", core.GetAttribute("aria-hidden"));
        Assert.Empty(core.QuerySelectorAll("span"));
        var img = core.QuerySelector("img")!;
        Assert.Equal("/favicon.svg", img.GetAttribute("src"));
        Assert.Equal("", img.GetAttribute("alt"));
        Assert.Equal("64", img.GetAttribute("width"));
        Assert.Equal("64", img.GetAttribute("height"));
    }

    [Fact]
    public async Task Header_follows_navigation_and_closes_the_menu()
    {
        NavigateTo("/");
        var cut = Render<SiteHeader>();
        cut.Find("button.menu-toggle").Click();
        Assert.Equal("true", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => NavigateTo("/links"));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));
            Assert.Equal("リンク", cut.Find("a.nav-link[aria-current='page']").TextContent.Trim());
        });
    }

    [Fact]
    public void Opening_the_menu_moves_the_focus_to_the_first_link()
    {
        NavigateTo("/");
        var cut = Render<SiteHeader>();

        cut.Find("button.menu-toggle").Click();

        cut.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations, i => i.Identifier == "faraSite.focusMenu"));
        // 閉じるときは、フォーカスを動かさない
        var before = JSInterop.Invocations.Count(i => i.Identifier == "faraSite.focusMenu");
        cut.Find("button.menu-toggle").Click();
        Assert.Equal(before, JSInterop.Invocations.Count(i => i.Identifier == "faraSite.focusMenu"));
    }

    [Fact]
    public void Skip_link_points_at_the_main_area_of_the_current_page_and_focuses_it()
    {
        NavigateTo("/works");
        var cut = Render<AppShell>(p => p.AddChildContent("<p>本文</p>"));

        var skip = cut.Find("a.skip-link");
        Assert.Equal("http://localhost/works#main-content", skip.GetAttribute("href"));
        Assert.NotNull(cut.Find("main#main-content[tabindex='-1']"));

        skip.Click();

        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "faraSite.focusMain");
    }

    [Fact]
    public void Updates_page_shows_the_filter_with_counts_and_filters_by_category()
    {
        var cut = Render<Updates>();

        var chips = cut.FindAll("button.filter-chip");
        Assert.Equal(4, chips.Count);
        Assert.Equal("すべて 3", Normalize(chips[0].TextContent));
        Assert.Equal("サイト 1", Normalize(chips[1].TextContent));
        Assert.Equal("作品 1", Normalize(chips[2].TextContent));
        Assert.Equal("お知らせ 1", Normalize(chips[3].TextContent));
        Assert.Equal(3, cut.FindAll("li.update-item").Count);

        chips[2].Click();

        Assert.Single(cut.FindAll("li.update-item"));
        Assert.Contains("作品の更新", cut.Markup);
        Assert.Equal("true", cut.FindAll("button.filter-chip")[2].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll("button.filter-chip")[0].GetAttribute("aria-pressed"));

        cut.FindAll("button.filter-chip")[0].Click();
        Assert.Equal(3, cut.FindAll("li.update-item").Count);
    }

    [Fact]
    public void Updates_page_marks_new_only_within_14_days_of_the_browsers_today()
    {
        var cut = Render<Updates>();

        var newItems = cut.FindAll("li.update-item").Where(li => li.QuerySelector(".chip-new") is not null).ToList();
        // 2026-10-23（今日）だけ。2026-10-01 は 22 日前なので対象外
        Assert.Single(newItems);
        Assert.Contains("今日のサイト更新", newItems[0].TextContent);
    }

    [Fact]
    public void Updates_page_has_no_filter_when_not_interactive()
    {
        Services.AddSingleton(new RenderEnvironment(IsInteractive: false));
        var cut = Render<Updates>();

        Assert.Empty(cut.FindAll("button.filter-chip"));
        Assert.Equal(3, cut.FindAll("li.update-item").Count);
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
