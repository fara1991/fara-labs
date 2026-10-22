using FaraPortfolio.Web.Layout;
using FaraPortfolio.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FaraPortfolio.Tests;

/// <summary>お知らせの帯（Layout/MaintenanceBanner.razor）の表示。見出し（【お知らせ】）を 1 行目に、本文を次の行に出す。</summary>
public class NoticeBannerTests
{
    private static async Task<string> RenderAsync(MaintenanceInfo info)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(info);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<MaintenanceBanner>()).ToHtmlString()));
    }

    [Fact]
    public async Task The_label_comes_first_and_the_text_follows_it_in_its_own_element()
    {
        var html = await RenderAsync(new MaintenanceInfo(MaintenanceState.Notice, "10月25日（土）20:00〜21:00 ごろ、サイトを更新します"));
        Assert.Contains("<div class=\"maintenance-notice\" role=\"status\"><strong class=\"maintenance-notice-title\">【お知らせ】</strong><span class=\"maintenance-notice-text\">10月25日（土）20:00〜21:00 ごろ、サイトを更新します</span></div>", html);
    }

    [Fact]
    public async Task With_no_message_the_default_text_is_shown_after_the_label()
    {
        var html = await RenderAsync(new MaintenanceInfo(MaintenanceState.Notice, ""));
        Assert.Contains("<strong class=\"maintenance-notice-title\">【お知らせ】</strong><span class=\"maintenance-notice-text\">まもなくメンテナンスを行います。</span>", html);
    }

    [Fact]
    public async Task Nothing_is_shown_when_there_is_no_notice()
    {
        Assert.DoesNotContain("maintenance-notice", await RenderAsync(MaintenanceInfo.Off));
    }
}
