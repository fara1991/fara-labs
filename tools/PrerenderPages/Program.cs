using System.Text.Encodings.Web;
using System.Text.Json;
using FaraPortfolio.Prerender;
using FaraPortfolio.Web.Services;

// 使い方は PrerenderArgs.Usage。kit の build-pages に渡す pages.json（path・title・description・bodyHtml の配列）を書く。
var (parsed, error) = PrerenderArgs.Parse(args);
if (parsed is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(PrerenderArgs.Usage);
    return 2;
}
var (dataDir, host, outFile, notFoundOut) = parsed;

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
