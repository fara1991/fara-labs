using FaraPortfolio.Web.Models;

namespace FaraPortfolio.Web.Services;

/// <summary>
/// ページごとの題名（title）と説明（description）。ページ（Pages/*.razor）の PageTitle と、静的な HTML の head（tools/PrerenderPages → kit の build-pages）が、同じ値を使う。
/// </summary>
public static class PageCatalog
{
    public sealed record Page(string Path, string Title, string Description);

    public const string ReportPath = "/report";
    public const string PrivacyPath = "/privacy";

    public static Page Home(Profile p) => new("/", SiteInfo.Name, p.Tagline);
    public static Page ProfilePage(Profile p) => new("/profile", $"Profile | {p.Name}", p.Bio);
    public static Page Works(Profile p) => new("/works", $"Works | {p.Name}", "作ったものの紹介です。Web アプリ、ツール、VRChat アバターの改変などを載せています。");
    public static Page Updates(Profile p) => new("/updates", $"Updates | {p.Name}", "サイトや作品の更新履歴です。");
    public static Page Links(Profile p) => new("/links", $"Links | {p.Name}", "SNS・リンクの一覧です。");
    public static Page Report() => new(ReportPath, $"問い合わせ | {SiteInfo.Name}", "不具合・要望・ご意見を、作者に直接送れる問い合わせフォームです。");
    public static Page Privacy() => new(PrivacyPath, $"プライバシーポリシー | {SiteInfo.Name}", "このサイトで扱う情報と、その目的をまとめたプライバシーポリシーです。");

    /// <summary>公開するページ（sitemap に載せる順）。</summary>
    public static IReadOnlyList<Page> All(Profile p) =>
        [Home(p), ProfilePage(p), Works(p), Updates(p), Links(p), Report(), Privacy()];
}
