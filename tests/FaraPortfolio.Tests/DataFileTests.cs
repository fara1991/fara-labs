using System.Text.Json;
using FaraPortfolio.Web.Components;
using FaraPortfolio.Web.Models;

namespace FaraPortfolio.Tests;

/// <summary>
/// wwwroot/data/*.json を手で編集したときの書き間違いを、デプロイ前（CI）で検出する。
/// </summary>
public class DataFileTests
{
    private static T Load<T>(string file)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", file));
        return JsonSerializer.Deserialize<T>(json, SiteJson.Options)
               ?? throw new InvalidDataException($"{file} が空です");
    }

    private static bool IsAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [Fact]
    public void Profile_IsValid()
    {
        var profile = Load<Profile>("profile.json");

        Assert.False(string.IsNullOrWhiteSpace(profile.Name));
        Assert.All(profile.Socials, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Label));
            if (!string.IsNullOrWhiteSpace(s.Url))
                Assert.True(IsAbsoluteHttpUrl(s.Url) || s.Url.StartsWith("mailto:"), $"{s.Label} の url が不正です: {s.Url}");
        });
    }

    [Fact]
    public void Works_AreValid()
    {
        var works = Load<List<Work>>("works.json");

        Assert.NotEmpty(works);
        Assert.Equal(works.Count, works.Select(w => w.Id).Distinct().Count());
        Assert.All(works, w =>
        {
            Assert.Matches("^[a-z0-9-]+$", w.Id);
            Assert.False(string.IsNullOrWhiteSpace(w.Title), $"{w.Id}: title が空です");
            Assert.False(string.IsNullOrWhiteSpace(w.Summary), $"{w.Id}: summary が空です");
            Assert.True(Icon.Exists(w.Icon), $"{w.Id}: icon '{w.Icon}' は Components/Icon.razor にありません");
            Assert.Contains(w.Accent, new[] { "violet", "pink", "indigo", "cyan" });
            Assert.All(w.Links, l => Assert.True(IsAbsoluteHttpUrl(l.Url), $"{w.Id}: url が不正です: {l.Url}"));
        });
    }

    [Fact]
    public void Updates_AreValid()
    {
        var updates = Load<List<UpdateEntry>>("updates.json");
        var categories = UpdateCategories.All.Select(c => c.Key).ToHashSet();

        Assert.All(updates, u =>
        {
            Assert.NotEqual(default, u.Date);
            Assert.False(string.IsNullOrWhiteSpace(u.Title), $"{u.Date}: title が空です");
            Assert.True(categories.Contains(u.Category), $"{u.Date}: category '{u.Category}' は {string.Join(" / ", categories)} のどれかにしてください");
            if (!string.IsNullOrWhiteSpace(u.Link))
                Assert.True(u.Link.StartsWith('/') || IsAbsoluteHttpUrl(u.Link), $"{u.Date}: link が不正です: {u.Link}");
        });
    }
}
