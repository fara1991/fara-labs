using System.Text.Json;

namespace FaraPortfolio.Web.Models;

/// <summary>wwwroot/data/*.json を読むときの共通設定（camelCase・大文字小文字無視・コメント/末尾カンマ許可）。</summary>
public static class SiteJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

/// <summary>data/profile.json</summary>
public sealed class Profile
{
    public string Name { get; init; } = "";
    public string Tagline { get; init; } = "";
    public string Bio { get; init; } = "";
    public string[] Skills { get; init; } = [];
    public List<SocialLink> Socials { get; init; } = [];

    /// <summary>URL が設定されている SNS だけ（空欄のものはサイトに表示しない）。</summary>
    public IEnumerable<SocialLink> VisibleSocials => Socials.Where(s => !string.IsNullOrWhiteSpace(s.Url));
}

public sealed class SocialLink
{
    /// <summary>アイコンの種類: github / x / twitch / youtube / bluesky / mail / link</summary>
    public string Service { get; init; } = "link";
    public string Label { get; init; } = "";
    public string Handle { get; init; } = "";
    public string Url { get; init; } = "";
    public string Description { get; init; } = "";
}

/// <summary>data/works.json の 1 要素。</summary>
public sealed class Work
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Category { get; init; } = "";
    public string Status { get; init; } = "";
    /// <summary>Components/Icon.razor のアイコン名。</summary>
    public string Icon { get; init; } = "sparkles";
    /// <summary>カードの色味: violet / pink / indigo / cyan</summary>
    public string Accent { get; init; } = "violet";
    public string Summary { get; init; } = "";
    public string Description { get; init; } = "";
    public string[] Features { get; init; } = [];
    public string[] Tech { get; init; } = [];
    public List<WorkLink> Links { get; init; } = [];
}

public sealed class WorkLink
{
    public string Label { get; init; } = "";
    public string Url { get; init; } = "";
    /// <summary>true のリンクはメインボタン（塗りつぶし）で表示。</summary>
    public bool Primary { get; init; }
}

/// <summary>data/updates.json の 1 要素（更新履歴）。</summary>
public sealed class UpdateEntry
{
    public DateOnly Date { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    /// <summary>site / work / news（<see cref="UpdateCategories"/>）</summary>
    public string Category { get; init; } = "news";
    public string? Link { get; init; }
    public string? LinkLabel { get; init; }
}

public static class UpdateCategories
{
    public static readonly (string Key, string Label)[] All =
    [
        ("site", "サイト"),
        ("work", "作品"),
        ("news", "お知らせ"),
    ];

    public static string Label(string key) =>
        All.FirstOrDefault(c => c.Key == key).Label ?? key;
}
