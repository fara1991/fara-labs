using System.Linq;

namespace FaraPortfolio.Web.Services;

/// <summary>フッターの「問い合わせ」のリンクの組み立て（Components/SiteFooter.razor。テストできるよう、画面から分けている）。</summary>
public static class SiteFooterLinks
{
    /// <summary>
    /// 問い合わせのリンク。見ているページのパスを ?from= に付ける（どのページの報告かが分かるように）。
    /// 報告ページ・プライバシーポリシー・404 のとき、英数字・- _ / . 以外を含むパスのときは付けない（js/report.js・worker/report.js と同じ条件）。
    /// 404 ページは、どの URL でも同じ 404.html を返すので、そこからは付けない（書き出し時のパスが入ってしまうため）。
    /// </summary>
    public static string ReportHref(string path)
    {
        if (path is "/report" or "/privacy" or "/not-found" || path.Length == 0 || path.Length > 300 || path[0] != '/' || path.StartsWith("//")) return "/report";
        return path.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.')
            ? "/report?from=" + Uri.EscapeDataString(path)
            : "/report";
    }
}
