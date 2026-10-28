using System.Text.Json;
using FaraPortfolio.Web.Models;

namespace FaraPortfolio.Web.Services;

/// <summary>
/// サイトの中身（wwwroot/data/*.json）。ブラウザでは起動のときに 1 回だけ HTTP で読み（LoadAsync）、全ページで共有する。
/// 静的な本文の書き出し（tools/PrerenderPages）とテストは、ファイルから読む（Load）。
/// 読めないときは例外にして、起動（書き出し）を失敗させる（壊れたページを公開しない）。
/// </summary>
public sealed class SiteData
{
    public const string ProfileFile = "profile.json";
    public const string WorksFile = "works.json";
    public const string UpdatesFile = "updates.json";

    public required Profile Profile { get; init; }
    public required IReadOnlyList<Work> Works { get; init; }
    /// <summary>新しい順。</summary>
    public required IReadOnlyList<UpdateEntry> Updates { get; init; }

    /// <summary>ファイル（フォルダの中の 3 つの JSON）から読む。</summary>
    public static SiteData Load(string dataDir) =>
        LoadAll(file => Task.FromResult(File.ReadAllText(Path.Combine(dataDir, file)))).GetAwaiter().GetResult();

    /// <summary>ブラウザから、同じオリジンの data/*.json を読む。</summary>
    public static Task<SiteData> LoadAsync(HttpClient http) =>
        LoadAll(file => http.GetStringAsync("data/" + file));

    /// <summary>3 つを同時に読む。読み方（readFile：ファイル・HTTP）によらず、同じ形にして、同じエラーにする。</summary>
    private static async Task<SiteData> LoadAll(Func<string, Task<string>> readFile)
    {
        var profile = Read<Profile>(ProfileFile, readFile);
        var works = Read<List<Work>>(WorksFile, readFile);
        var updates = Read<List<UpdateEntry>>(UpdatesFile, readFile);
        await Task.WhenAll(profile, works, updates);
        return Create(profile.Result, works.Result, updates.Result);
    }

    private static SiteData Create(Profile profile, List<Work> works, List<UpdateEntry> updates) => new()
    {
        Profile = profile,
        Works = works,
        Updates = updates.OrderByDescending(u => u.Date).ToList(),
    };

    /// <summary>1 つの JSON を読む。読めない・空のときは、どのファイルかが分かる例外にする（読み方は readFile）。</summary>
    private static async Task<T> Read<T>(string file, Func<string, Task<string>> readFile)
    {
        try
        {
            var text = await readFile(file);
            return JsonSerializer.Deserialize<T>(text, SiteJson.Options)
                   ?? throw new InvalidDataException($"{file} が空です");
        }
        catch (Exception ex) when (ex is JsonException or IOException or HttpRequestException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException($"{file} を読めません: {ex.Message}", ex);
        }
    }
}
