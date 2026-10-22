namespace FaraPortfolio.Web.Services;

/// <summary>
/// 確認用サイト（dev.fara-labs.com）の書き出しでは、fara-labs.com 配下のリンクを確認用の URL に置き換える。
/// 例: https://pokemon.fara-labs.com/ → https://pokemon-dev.fara-labs.com/、https://fara-labs.com/ → https://dev.fara-labs.com/
/// GitHub など fara-labs.com 以外のリンクはそのまま。本番（fara-labs.com）の書き出しでは何も置き換えない。
/// </summary>
public static class SiteLinks
{
    private const string Domain = "fara-labs.com";

    public static bool IsDevelop(string host) => host.Equals("dev." + Domain, StringComparison.OrdinalIgnoreCase);

    public static string Localize(string url, string currentHost)
    {
        if (!IsDevelop(currentHost) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        var host = uri.Host.ToLowerInvariant();
        string? devHost = null;
        if (host == Domain) devHost = "dev." + Domain;
        else if (host.EndsWith("." + Domain) && !host.StartsWith("dev.") && !host[..^(Domain.Length + 1)].EndsWith("-dev"))
            devHost = host[..^(Domain.Length + 1)] + "-dev." + Domain;
        if (devHost is null) return url;
        return new UriBuilder(uri) { Host = devHost, Port = -1 }.Uri.AbsoluteUri;
    }
}
