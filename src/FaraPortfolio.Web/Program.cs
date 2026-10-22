using FaraPortfolio.Web;
using FaraPortfolio.Web.Models;
using FaraPortfolio.Web.Publishing;
using FaraPortfolio.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 静的 SSR: ページごとの完全な HTML を書き出す（dotnet run -- --export dist）。ブラウザ側の .NET 実行環境は使わない。
builder.Services.AddRazorComponents();

// 公開するホスト名。確認用（dev.fara-labs.com）の書き出しは --site-host dev.fara-labs.com で行う（リンクの -dev 置き換え・canonical・sitemap に使う）
builder.Services.AddSingleton(new SiteOptions(builder.Configuration["site-host"] ?? SiteOptions.ProductionHost));

// サイトの中身（wwwroot/data/*.json）は起動時に 1 回だけ読む。壊れていれば起動（書き出し）が失敗する
builder.Services.AddSingleton(_ => SiteData.Load(Path.Combine(builder.Environment.WebRootPath, "data")));

// dotnet run -- --export <dir> で静的サイトとして書き出す（Cloudflare へのデプロイ用）
var exportDir = builder.Configuration["export"];
var exporting = !string.IsNullOrEmpty(exportDir);
if (exporting)
{
    // 書き出し中は自分自身に HTTP でアクセスするので、空いているポートで HTTP のみ待ち受ける
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/not-found", createScopeForErrors: true);
}

// 未定義の URL は空の 404 ではなく /not-found ページを 404 ステータスで返す（書き出し時は 404.html になる）
app.UseStatusCodePagesWithReExecute("/not-found");

if (!exporting)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>();

if (exporting)
{
    await app.StartAsync();
    await StaticSiteExporter.ExportAsync(app, exportDir!);
    await app.StopAsync();
    return;
}

app.Run();

// テストから参照できるように（WebApplicationFactory など）
public partial class Program;
