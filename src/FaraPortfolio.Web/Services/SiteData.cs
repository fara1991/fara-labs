using System.Net.Http.Json;
using System.Reflection;
using FaraPortfolio.Web.Models;

namespace FaraPortfolio.Web.Services;

/// <summary>
/// サイトの中身（wwwroot/data/*.json）。起動時に 1 回だけ読み込み、全ページで共有する。
/// JSON が壊れていてもサイト自体は表示し、<see cref="Errors"/> に内容を残す。
/// </summary>
public sealed class SiteData
{
    private readonly List<string> _errors = [];

    public Profile Profile { get; private set; } = new();
    public IReadOnlyList<Work> Works { get; private set; } = [];
    /// <summary>新しい順。</summary>
    public IReadOnlyList<UpdateEntry> Updates { get; private set; } = [];
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>ビルド時に埋め込まれるコミット SHA（短縮。git 管理外でビルドしたときは "dev"）。</summary>
    public static string BuildId { get; } = ReadBuildId();

    public async Task LoadAsync(HttpClient http)
    {
        var profile = FetchAsync<Profile>(http, "profile.json");
        var works = FetchAsync<List<Work>>(http, "works.json");
        var updates = FetchAsync<List<UpdateEntry>>(http, "updates.json");
        await Task.WhenAll(profile, works, updates);

        Profile = profile.Result ?? new();
        Works = works.Result ?? [];
        Updates = (updates.Result ?? []).OrderByDescending(u => u.Date).ToList();
    }

    private async Task<T?> FetchAsync<T>(HttpClient http, string file)
    {
        try
        {
            // デプロイごとにクエリを変えて、ブラウザに古い JSON が残らないようにする
            return await http.GetFromJsonAsync<T>($"data/{file}?v={BuildId}", SiteJson.Options);
        }
        catch (Exception ex)
        {
            _errors.Add($"{file}: {ex.Message}");
            return default;
        }
    }

    private static string ReadBuildId()
    {
        var version = typeof(SiteData).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var plus = version?.IndexOf('+') ?? -1;
        if (plus < 0) return "dev";
        var sha = version![(plus + 1)..];
        return sha.Length > 7 ? sha[..7] : sha;
    }
}
