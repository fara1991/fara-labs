using System.Net;
using FaraPortfolio.Web.Models;
using FaraPortfolio.Web.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace FaraPortfolio.Web.Publishing;

/// <summary>
/// 起動中のアプリから全ページを取得し、静的サイト（HTML + 静的ファイル）として書き出す。
/// Cloudflare Workers（Static Assets）にそのままデプロイできる形にする。
/// 使い方: dotnet run --project src/FaraPortfolio.Web -c Release --no-launch-profile -- --export dist [--site-host dev.fara-labs.com]
/// （出力先の相対パスは、リポジトリのルートからになる）
/// </summary>
public static class StaticSiteExporter
{
    /// <summary>404 の書き出し用のパス。存在しないので、UseStatusCodePagesWithReExecute で /not-found（404）になる。</summary>
    private const string NotFoundProbe = "/__static_export_404__";

    public static async Task ExportAsync(WebApplication app, string outputDir)
    {
        var logger = app.Logger;
        var site = app.Services.GetRequiredService<SiteOptions>();
        var data = app.Services.GetRequiredService<SiteData>();
        var baseAddress = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

        // 相対パスは、リポジトリのルート（src/FaraPortfolio.Web の 2 つ上）からにする。dotnet run の作業フォルダに左右されないように
        var repoRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));
        outputDir = Path.GetFullPath(Path.Combine(repoRoot, outputDir));
        // 出力先は丸ごと消すので、プロジェクトのフォルダやその親（--export . など）は受け付けない
        var projectDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(app.Environment.ContentRootPath));
        var outputTrimmed = Path.TrimEndingDirectorySeparator(outputDir);
        if (string.Equals(projectDir, outputTrimmed, StringComparison.OrdinalIgnoreCase) ||
            projectDir.StartsWith(outputTrimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"--export には専用の出力フォルダ（例: dist）を指定してください: {outputDir}");
        }
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        Directory.CreateDirectory(outputDir);

        // --- ページ ---------------------------------------------------------
        // Workers の html_handling（auto-trailing-slash）に合わせ、/works は works.html に書く
        var pages = new Dictionary<string, string>
        {
            ["/"] = "index.html",
            ["/profile"] = "profile.html",
            ["/works"] = "works.html",
            ["/updates"] = "updates.html",
            ["/links"] = "links.html",
            ["/report"] = "report.html",
            ["/privacy"] = "privacy.html",
        };
        foreach (var (path, file) in pages)
        {
            await WriteFromServerAsync(http, outputDir, path, file, HttpStatusCode.OK);
        }

        // 404 ページ（Workers の not_found_handling = "404-page" が /404.html を返す）
        await WriteFromServerAsync(http, outputDir, NotFoundProbe, "404.html", HttpStatusCode.NotFound);

        // --- 静的ファイル ---------------------------------------------------
        // サイトの中身（data/*.json）は HTML に埋め込み済みなので、公開しない
        CopyDirectory(app.Environment.WebRootPath, outputDir, file => !IsDataFile(app.Environment.WebRootPath, file));

        // 検索エンジン向け（確認用は載せない。確認用の noindex は deploy-develop の _headers で付ける）
        if (!site.IsDevelop)
        {
            await WriteTextAsync(outputDir, "sitemap.xml", BuildSitemap(site, pages.Keys));
        }
        await WriteTextAsync(outputDir, "robots.txt", BuildRobots(site));

        var fileCount = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories).Count();
        logger.LogInformation("Exported {Pages} pages, {Files} files in total to {Dir} ({Host}, {Works} works)",
            pages.Count + 1, fileCount, outputDir, site.Host, data.Works.Count);
    }

    /// <summary>サイトマップ（確認用は書かない）。ページの URL だけを並べる。</summary>
    public static string BuildSitemap(SiteOptions site, IEnumerable<string> paths)
    {
        var lines = paths.Select(p => $"  <url><loc>{site.Origin}{p}</loc></url>");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n"
               + string.Join("\n", lines) + "\n</urlset>\n";
    }

    /// <summary>robots.txt。本番では sitemap の場所を書く。</summary>
    public static string BuildRobots(SiteOptions site) =>
        site.IsDevelop
            ? "User-agent: *\nAllow: /\n"
            : $"User-agent: *\nAllow: /\n\nSitemap: {site.Origin}/sitemap.xml\n";

    /// <summary>ページを取得して書く。期待した状態コード（404 のページは 404）以外は失敗にする。</summary>
    private static async Task WriteFromServerAsync(HttpClient http, string outputDir, string path, string file, HttpStatusCode expected)
    {
        using var response = await http.GetAsync(path);
        if (response.StatusCode != expected)
        {
            throw new InvalidOperationException($"{path} returned {(int)response.StatusCode} (expected {(int)expected})");
        }
        await WriteBytesAsync(outputDir, file, await response.Content.ReadAsByteArrayAsync());
    }

    private static bool IsDataFile(string webRoot, string file) =>
        Path.GetRelativePath(webRoot, file).StartsWith("data" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static async Task WriteBytesAsync(string outputDir, string relativePath, byte[] bytes)
    {
        var path = Path.Combine(outputDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    private static async Task WriteTextAsync(string outputDir, string relativePath, string text) =>
        await WriteBytesAsync(outputDir, relativePath, System.Text.Encoding.UTF8.GetBytes(text));

    private static void CopyDirectory(string source, string target, Func<string, bool> include)
    {
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (!include(file)) continue;
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
