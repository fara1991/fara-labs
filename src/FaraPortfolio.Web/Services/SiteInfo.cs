namespace FaraPortfolio.Web.Services;

/// <summary>フッターなどに出す、サイトの名前・公開年・作者。4 サイト（pokemon・avatar・bot・portfolio）で、フッターの形を同じにする。</summary>
public static class SiteInfo
{
    public const string Name = "Fara Portfolio";
    /// <summary>サイトを公開した年（© の年）。</summary>
    public const int PublishedYear = 2026;
    public const string Author = "Fara";
    /// <summary>作者のホームページ（確認用の書き出しでは SiteOptions.Localize で置き換える）。</summary>
    public const string PortfolioUrl = "https://fara-labs.com/";
}
