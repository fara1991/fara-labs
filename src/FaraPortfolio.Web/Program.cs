using FaraPortfolio.Web;
using FaraPortfolio.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddSingleton(http);
builder.Services.AddSingleton<SiteData>();

var host = builder.Build();
await host.Services.GetRequiredService<SiteData>().LoadAsync(http);
await host.RunAsync();
