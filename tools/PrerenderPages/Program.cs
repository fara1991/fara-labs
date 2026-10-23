using System.Text.Encodings.Web;
using System.Text.Json;
using FaraPortfolio.Prerender;
using FaraPortfolio.Web.Services;

// 使い方: dotnet run --project tools/PrerenderPages -c Release -- --data <data フォルダ> --host <公開するホスト名> --out <pages.json> --not-found-out <not-found.json>
// kit の build-pages に渡す pages.json（path・title・description・bodyHtml の配列）を書く。
string? dataDir = null, host = null, outFile = null, notFoundOut = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--data": dataDir = args[++i]; break;
        case "--host": host = args[++i]; break;
        case "--out": outFile = args[++i]; break;
        case "--not-found-out": notFoundOut = args[++i]; break;
        default: Console.Error.WriteLine($"知らない引数: {args[i]}"); return 2;
    }
}
if (dataDir is null || host is null || outFile is null || notFoundOut is null)
{
    Console.Error.WriteLine("--data・--host・--out・--not-found-out を指定してください");
    return 2;
}

var result = await PagePrerenderer.RenderAllAsync(SiteData.Load(dataDir), host);
var pages = result.Pages;
var jsonOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var json = JsonSerializer.Serialize(
    pages.Select(p => new { path = p.Path, title = p.Title, description = p.Description, bodyHtml = p.BodyHtml }),
    jsonOptions);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
await File.WriteAllTextAsync(outFile, json + "\n");
await File.WriteAllTextAsync(notFoundOut, JsonSerializer.Serialize(new { title = result.NotFound.Title, bodyHtml = result.NotFound.BodyHtml }, jsonOptions) + "\n");
Console.WriteLine($"{pages.Count} ページの本文を書き出しました: {outFile}");
return 0;
