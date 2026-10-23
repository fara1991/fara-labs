using System.Net.Http.Json;
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
    public static SiteData Load(string dataDir) => Create(
        Read<Profile>(ProfileFile, () => File.ReadAllText(Path.Combine(dataDir, ProfileFile))),
        Read<List<Work>>(WorksFile, () => File.ReadAllText(Path.Combine(dataDir, WorksFile))),
        Read<List<UpdateEntry>>(UpdatesFile, () => File.ReadAllText(Path.Combine(dataDir, UpdatesFile))));

    /// <summary>ブラウザから、同じオリジンの data/*.json を読む。</summary>
    public static async Task<SiteData> LoadAsync(HttpClient http)
    {
        var profile = await ReadAsync<Profile>(http, ProfileFile);
        var works = await ReadAsync<List<Work>>(http, WorksFile);
        var updates = await ReadAsync<List<UpdateEntry>>(http, UpdatesFile);
        return Create(profile, works, updates);
    }

    private static SiteData Create(Profile profile, List<Work> works, List<UpdateEntry> updates) => new()
    {
        Profile = profile,
        Works = works,
        Updates = updates.OrderByDescending(u => u.Date).ToList(),
    };

    private static T Read<T>(string file, Func<string> readText)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(readText(), SiteJson.Options)
                   ?? throw new InvalidDataException($"{file} が空です");
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException($"{file} を読めません: {ex.Message}", ex);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpClient http, string file)
    {
        try
        {
            return await http.GetFromJsonAsync<T>("data/" + file, SiteJson.Options)
                   ?? throw new InvalidDataException($"{file} が空です");
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException($"{file} を読めません: {ex.Message}", ex);
        }
    }
}
