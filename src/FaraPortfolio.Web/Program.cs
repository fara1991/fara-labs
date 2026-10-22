using FaraPortfolio.Web;
using FaraPortfolio.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

SiteLinks.CurrentHost = new Uri(builder.HostEnvironment.BaseAddress).Host;

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

// メンテナンスの状態（Worker の変数 MAINTENANCE。worker/maintenance.js）。読めなければ通常。
// メンテナンス中（MAINTENANCE=true）は、サイトの代わりに、メンテナンス画面だけを動かす
var maintenance = await MaintenanceInfo.LoadAsync(http);
builder.Services.AddSingleton(maintenance);
if (maintenance.State == MaintenanceState.On)
{
    builder.RootComponents.Add<MaintenanceScreen>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
    await builder.Build().RunAsync();
    return;
}

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton(http);
builder.Services.AddSingleton<SiteData>();

var host = builder.Build();
await host.Services.GetRequiredService<SiteData>().LoadAsync(http);
await host.RunAsync();
