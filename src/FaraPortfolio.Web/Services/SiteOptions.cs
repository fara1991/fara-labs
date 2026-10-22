namespace FaraPortfolio.Web.Services;

/// <summary>
/// 書き出すサイトのホスト名（本番は fara-labs.com、確認用は dev.fara-labs.com）。
/// 書き出しのたびに 1 つに決まる値で、リンクの置き換え・canonical・sitemap に使う。静的な状態は持たない（テストを並列に動かせるように）。
/// </summary>
public sealed record SiteOptions(string Host)
{
    public const string ProductionHost = "fara-labs.com";

    /// <summary>サイトの origin（例: https://fara-labs.com）。canonical・OGP の URL に使う。</summary>
    public string Origin => "https://" + Host;

    /// <summary>確認用（dev.fara-labs.com）の書き出しかどうか。</summary>
    public bool IsDevelop => SiteLinks.IsDevelop(Host);

    /// <summary>fara-labs.com 配下のリンクを、このサイトの確認用の URL に置き換える（確認用の書き出しのときだけ）。</summary>
    public string Localize(string url) => SiteLinks.Localize(url, Host);
}
