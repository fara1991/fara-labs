using System.Text.Encodings.Web;
using System.Text.Json;
using FaraPortfolio.Prerender;
using FaraPortfolio.Web.Services;

// 使い方: dotnet run --project tools/PrerenderPages -c Release -- --data <data フォルダ> --host <公開するホスト名> --out <pages.json>
// kit の build-pages に渡す pages.json（path・title・description・bodyHtml の配列）を書く。
string? dataDir = null, host = null, outFile = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--data": dataDir = args[++i]; break;
        case "--host": host = args[++i]; break;
        case "--out": outFile = args[++i]; break;
        default: Console.Error.WriteLine($"知らない引数: {args[i]}"); return 2;
    }
}
if (dataDir is null || host is null || outFile is null)
{
    Console.Error.WriteLine("--data・--host・--out を指定してください");
    return 2;
}

var pages = await PagePrerenderer.RenderAllAsync(SiteData.Load(dataDir), host);
var json = JsonSerializer.Serialize(
    pages.Select(p => new { path = p.Path, title = p.Title, description = p.Description, bodyHtml = p.BodyHtml }),
    new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
await File.WriteAllTextAsync(outFile, json + "\n");
Console.WriteLine($"{pages.Count} ページの本文を書き出しました: {outFile}");
return 0;
