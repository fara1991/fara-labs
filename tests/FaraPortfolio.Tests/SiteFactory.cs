using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FaraPortfolio.Tests;

/// <summary>
/// 書き出しと同じ構成のサイトを、テスト用のサーバーで動かす（dotnet run -- --export dist と同じ Razor のページ）。
/// 本番（fara-labs.com）の書き出しの既定の構成。
/// </summary>
public sealed class SiteFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("site-host", "fara-labs.com");
}

/// <summary>確認用（dev.fara-labs.com）の書き出しの構成（--site-host dev.fara-labs.com）。</summary>
public sealed class DevelopSiteFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("site-host", "dev.fara-labs.com");
}
