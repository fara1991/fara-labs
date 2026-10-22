using System.Text.Json;
using FaraPortfolio.Web.Models;

namespace FaraPortfolio.Web.Services;

/// <summary>
/// サイトの中身（wwwroot/data/*.json）。起動（書き出し）のときに 1 回だけ読み、全ページで共有する。
/// 中身は HTML に埋め込まれるので、ブラウザは JSON を読まない。読めないときは例外にして、書き出しを失敗させる（壊れたページを公開しない）。
/// </summary>
public sealed class SiteData
{
    public required Profile Profile { get; init; }
    public required IReadOnlyList<Work> Works { get; init; }
    /// <summary>新しい順。</summary>
    public required IReadOnlyList<UpdateEntry> Updates { get; init; }

    public static SiteData Load(string dataDir) => new()
    {
        Profile = Read<Profile>(dataDir, "profile.json"),
        Works = Read<List<Work>>(dataDir, "works.json"),
        Updates = Read<List<UpdateEntry>>(dataDir, "updates.json").OrderByDescending(u => u.Date).ToList(),
    };

    private static T Read<T>(string dataDir, string file)
    {
        var path = Path.Combine(dataDir, file);
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, SiteJson.Options)
                   ?? throw new InvalidDataException($"{file} が空です");
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException($"{file} を読めません: {ex.Message}", ex);
        }
    }
}
