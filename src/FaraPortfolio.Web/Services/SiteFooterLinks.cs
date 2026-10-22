using System.Linq;

namespace FaraPortfolio.Web.Services;

/// <summary>フッターの「作者へ報告」のリンクの組み立て（Components/SiteFooter.razor。テストできるよう、画面から分けている）。</summary>
public static class SiteFooterLinks
{
    /// <summary>
    /// 「作者へ報告」のリンク。見ているページのパスを ?from= に付ける（どのページの報告かが分かるように）。
    /// 報告ページ・プライバシーポリシーのとき、英数字・- _ / . 以外を含むパスのときは付けない（ReportForm.CleanPage・worker/report.js と同じ条件）。
    /// </summary>
    public static string ReportHref(string path)
    {
        if (path is "/report" or "/privacy" || path.Length == 0 || path.Length > 300 || path[0] != '/' || path.StartsWith("//")) return "/report";
        return path.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.')
            ? "/report?from=" + Uri.EscapeDataString(path)
            : "/report";
    }
}
