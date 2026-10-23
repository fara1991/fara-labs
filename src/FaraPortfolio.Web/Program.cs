using FaraPortfolio.Web;
using FaraPortfolio.Web.Services;
using FaraSiteKit.Core;
using FaraSiteKit.Razor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var baseAddress = builder.HostEnvironment.BaseAddress;
var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

// サイトの中身（wwwroot/data/*.json）は起動のときに 1 回だけ読む。最初の描画の前に必要（静的な本文を、空の画面に置き換えないため）
var data = await SiteData.LoadAsync(http);
builder.Services.AddSingleton(data);

// 共通部品（fara-site-kit の Razor。フッター・報告フォームなど）の設定。値は Services/SiteInfo.cs。
// 確認用のホストで開いているときの、fara-labs.com 配下のリンクの切り替えは、開いているホスト名で決める
builder.Services.AddFaraSiteKit(options => SiteInfo.Configure(options, data.Profile));
SiteLinks.CurrentHost = new Uri(baseAddress).Host;

// メンテナンスの状態（Worker の変数 MAINTENANCE。external/fara-site-kit/worker/maintenance.js）。読めなければ通常
builder.Services.AddSingleton(await MaintenanceInfo.LoadAsync(http));

builder.Services.AddSingleton(http);
builder.Services.AddSingleton<BrowserClock>();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var host = builder.Build();

// 「今日」はブラウザの日付（更新履歴の NEW）。js/site.js が読めなくても、アプリは起動する（NEW が出ないだけ）
try
{
    var today = await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string>("faraSite.today");
    if (DateOnly.TryParseExact(today, "yyyy-MM-dd", out var date)) host.Services.GetRequiredService<BrowserClock>().Today = date;
}
catch (JSException)
{
}

await host.RunAsync();
