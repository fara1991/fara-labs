using System.Text.Encodings.Web;
using System.Text.Unicode;
using FaraPortfolio.Web.Components;
using FaraPortfolio.Web.Pages;
using FaraPortfolio.Web.Services;
using FaraSiteKit.Core;
using FaraSiteKit.Razor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FaraPortfolio.Prerender;

/// <summary>入口ページ 1 枚（head の値と、本文の HTML）。kit の build-pages の pages.json の 1 要素になる。</summary>
public sealed record PrerenderedPage(string Path, string Title, string Description, string BodyHtml);

/// <summary>全ページと、404.html の本文（存在しない URL で、JavaScript が無くても読める本文）。</summary>
public sealed record PrerenderResult(IReadOnlyList<PrerenderedPage> Pages, PrerenderedPage NotFound);

/// <summary>
/// 検索エンジンと JavaScript の無い環境のために、ページの本文入りの HTML を作る。
/// ブラウザで Blazor が描くのと同じ部品（AppShell・ヘッダー・フッター・ページ）を、サーバー側で描く。
/// Blazor が起動すると、この本文は #app の中身ごと置き換わる（起動までは、読み込み表示の下にある）。
/// 問い合わせは、ブラウザと同じ ReportFormView（フォームの送信は JavaScript が要る）を描く。
/// </summary>
public static class PagePrerenderer
{
    /// <summary>
    /// 全ページを描く。host は公開するホスト名（本文のリンクを、確認用のときは確認用の URL にするため）。
    /// 注意: kit の SiteLinks.CurrentHost（静的な値）を書き換えるので、並列に呼ばない。
    /// </summary>
    public static async Task<PrerenderResult> RenderAllAsync(SiteData data, string host)
    {
        SiteLinks.CurrentHost = host;
        var services = new ServiceCollection();
        services.AddLogging();
        // 日本語を &#x....; にしない（本文を、そのまま読める HTML にする）
        services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));
        services.AddSingleton(data);
        services.AddSingleton<BrowserClock>();
        services.AddSingleton(new RenderEnvironment(IsInteractive: false));
        services.AddSingleton(new HttpClient());
        services.AddFaraSiteKit(options => SiteInfo.Configure(options, data.Profile));
        services.AddScoped<NavigationManager, PrerenderNavigationManager>();
        services.AddSingleton<IJSRuntime, NoJsRuntime>();
        await using var provider = services.BuildServiceProvider();

        var profile = data.Profile;
        (PageCatalog.Page Page, Type Component)[] pages =
        [
            (PageCatalog.Home(profile), typeof(Home)),
            (PageCatalog.ProfilePage(profile), typeof(Profile)),
            (PageCatalog.Works(profile), typeof(Works)),
            (PageCatalog.Updates(profile), typeof(Updates)),
            (PageCatalog.Links(profile), typeof(Links)),
            (PageCatalog.Report(), typeof(ReportContent)),
            (PageCatalog.Privacy(), typeof(Privacy)),
        ];

        var rendered = new List<PrerenderedPage>();
        foreach (var (page, component) in pages)
        {
            await using var scope = provider.CreateAsyncScope();
            var nav = (PrerenderNavigationManager)scope.ServiceProvider.GetRequiredService<NavigationManager>();
            nav.Set($"https://{host}/", $"https://{host}{page.Path}");
            await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                return await RenderBodyAsync(renderer, component);
            });
            rendered.Add(new PrerenderedPage(page.Path, page.Title, page.Description, html));
        }

        // 404.html: 存在しない URL でも使うので、どのページでもない URL にする（フッターの問い合わせに ?from= が付かず、ヘッダーのどれも選ばれない）
        await using var notFoundScope = provider.CreateAsyncScope();
        var notFoundNav = (PrerenderNavigationManager)notFoundScope.ServiceProvider.GetRequiredService<NavigationManager>();
        notFoundNav.Set($"https://{host}/", $"https://{host}/%E3%83%9A");
        await using var notFoundRenderer = new HtmlRenderer(notFoundScope.ServiceProvider, notFoundScope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var notFoundHtml = await notFoundRenderer.Dispatcher.InvokeAsync(() => RenderBodyAsync(notFoundRenderer, typeof(NotFoundContent)));
        return new PrerenderResult(rendered, new PrerenderedPage("/404", "ページが見つかりません | Fara", "", notFoundHtml));
    }

    private static readonly System.Text.RegularExpressions.Regex InternalAttribute = new(" __internal_[A-Za-z0-9_]+(?==| |>|/)");

    private static async Task<string> RenderBodyAsync(HtmlRenderer renderer, Type component)
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent(0, component);
            builder.CloseComponent();
        };
        var output = await renderer.RenderComponentAsync<StaticPageHost>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(StaticPageHost.Content)] = content }));
        // @onclick:preventDefault / stopPropagation が、静的な HTML には内部の属性（__internal_…）として出てしまうので外す（リンクは、そのまま使える）
        return InternalAttribute.Replace(output.ToHtmlString(), "");
    }

    /// <summary>本文の最上位の要素（bodyRootClass の entry-static）。kit の読み込み表示の JS が、この class を見て、起動までの間 inert にする。</summary>
    private sealed class StaticPageHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "entry-static");
            builder.OpenComponent<AppShell>(2);
            builder.AddAttribute(3, nameof(AppShell.ChildContent), Content);
            builder.CloseComponent();
            builder.CloseElement();
        }
    }

    private sealed class PrerenderNavigationManager : NavigationManager
    {
        public void Set(string baseUri, string uri) => Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            throw new NotSupportedException("静的な本文の書き出しでは、ページを移らない");
    }

    /// <summary>JavaScript を使わない（書き出しの描画では呼ばれない。呼ばれたら気付けるよう、例外にする）。</summary>
    private sealed class NoJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException($"書き出しでは JavaScript を呼べません: {identifier}");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
