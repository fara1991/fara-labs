namespace FaraPortfolio.Web.Services;

/// <summary>
/// 「問い合わせ」フォームの制限（Pages/Report.razor の属性と、wwwroot/js/report.js が読む）。
/// 長さは worker/report.js と数値を同じにしてある。数え方は違う（ここは画面側の UTF-16 の長さではなく、js/report.js では文字単位で数える）。
/// </summary>
public static class ReportLimits
{
    public const int MaxMessage = 2000;
    public const int MinMessage = 5;
    public const int MaxContact = 200;

    /// <summary>種類の ID（worker/report.js の CATEGORIES と同じ）と、画面に出す名前。</summary>
    public static readonly IReadOnlyList<(string Id, string Label)> Categories =
    [
        ("bug", "不具合・記事の間違い"),
        ("request", "要望"),
        ("other", "その他"),
    ];
}
