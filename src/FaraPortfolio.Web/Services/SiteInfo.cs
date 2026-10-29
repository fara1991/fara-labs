using FaraPortfolio.Web.Models;
using FaraBlazorComponent.Core;

namespace FaraPortfolio.Web.Services;

/// <summary>
/// このサイトの名前・公開年・作者・確認用ホスト。共通部品（fara-blazor-component。フッター・報告フォームなど）が読む SiteOptions に、起動時に入れる（Program.cs）。
/// 4 サイト（pokemon・avatar・bot・portfolio）で、フッターの形を同じにする。
/// </summary>
public static class SiteInfo
{
    public const string Name = "Fara Labs";
    /// <summary>サイトを公開した年（© の年）。</summary>
    public const int PublishedYear = 2026;
    public const string Author = "Fara";
    /// <summary>作者のホームページ（確認用のホストで開いているときは、kit の SiteLinks.Localize が確認用に置き換える）。</summary>
    public const string PortfolioUrl = "https://fara-labs.com/";
    /// <summary>確認用サイトのホスト名（開いているホストがこれなら、fara-labs.com 配下のリンクを確認用に切り替える）。</summary>
    public const string DevelopHost = "dev.fara-labs.com";
    public const string ProductionHost = "fara-labs.com";

    /// <summary>フッターに出す SNS。kit が絵柄と色を持つサービス（SocialServices）だけ。</summary>
    private static readonly HashSet<string> FooterServices = ["github", "twitter", "twitch", "discord"];

    /// <summary>共通部品の設定に、このサイトの値（と、プロフィールの SNS）を入れる。</summary>
    public static void Configure(SiteOptions options, Profile profile)
    {
        options.Name = Name;
        options.Author = Author;
        options.PublishedYear = PublishedYear;
        options.AuthorHomeUrl = PortfolioUrl;
        options.HomeHref = "/";
        options.DevelopHost = DevelopHost;
        options.ReportPath = "/report";
        options.PrivacyPath = "/privacy";
        options.ReportApiPath = "/api/report";
        options.ReportConfigPath = "/api/report/config";
        foreach (var s in profile.VisibleSocials.Where(s => FooterServices.Contains(s.Service.ToLowerInvariant())))
            options.Social.Add(new SiteSocialLink(s.Service.ToLowerInvariant(), s.Url));
    }
}
