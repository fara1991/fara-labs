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

    /// <summary>ファイル（フォルダの中の 3 つの JSON）から読む。同期で読む（書き出しツールとテスト用。ブラウザでは使わない）。</summary>
    public static SiteData Load(string dataDir)
    {
        T ReadFile<T>(string file)
        {
            try
            {
                return Deserialize<T>(file, File.ReadAllText(Path.Combine(dataDir, file)));
            }
            catch (Exception ex) when (IsReadError(ex))
            {
                throw Wrap(file, ex);
            }
        }

        return Create(ReadFile<Profile>(ProfileFile), ReadFile<List<Work>>(WorksFile), ReadFile<List<UpdateEntry>>(UpdatesFile));
    }

    /// <summary>ブラウザから、同じオリジンの data/*.json を読む。3 つを同時に読む。</summary>
    public static async Task<SiteData> LoadAsync(HttpClient http)
    {
        var profile = ReadAsync<Profile>(ProfileFile, http);
        var works = ReadAsync<List<Work>>(WorksFile, http);
        var updates = ReadAsync<List<UpdateEntry>>(UpdatesFile, http);
        await Task.WhenAll(profile, works, updates);
        return Create(await profile, await works, await updates);
    }

    private static SiteData Create(Profile profile, List<Work> works, List<UpdateEntry> updates) => new()
    {
        Profile = profile,
        Works = works,
        Updates = updates.OrderByDescending(u => u.Date).ToList(),
    };

    /// <summary>1 つの JSON を HTTP で読む。読めない・空のときは、どのファイルかが分かる例外にする。</summary>
    private static async Task<T> ReadAsync<T>(string file, HttpClient http)
    {
        try
        {
            return Deserialize<T>(file, await http.GetStringAsync("data/" + file));
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            throw Wrap(file, ex);
        }
    }

    private static T Deserialize<T>(string file, string text) =>
        JsonSerializer.Deserialize<T>(text, SiteJson.Options)
        ?? throw new InvalidDataException($"{file} が空です");

    private static bool IsReadError(Exception ex) =>
        ex is JsonException or IOException or HttpRequestException or InvalidDataException or NotSupportedException;

    private static InvalidDataException Wrap(string file, Exception ex) =>
        new($"{file} を読めません: {ex.Message}", ex);
}
