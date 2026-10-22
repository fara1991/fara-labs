using System.Text;

namespace FaraPortfolio.Tests;

/// <summary>
/// docs/maintenance.html（Cloudflare の WAF カスタムルールの「カスタム応答」に貼り付ける、メンテナンス画面）と、設定の手順書。
/// 応答の本文には大きさの上限があるので、短いまま・外部に依存しないままであることを、直したときに確かめる。
/// </summary>
public class MaintenancePageTests
{
    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void MaintenanceHtml_IsSmallEnoughToPasteIntoTheCustomResponse()
    {
        var bytes = Encoding.UTF8.GetByteCount(File.ReadAllText(RepoFile("docs/maintenance.html")));
        Assert.True(bytes <= 1024, $"メンテナンス画面が大きすぎます（{bytes} バイト）。カスタム応答の本文の上限（1 KB 未満に保つ）を超えています");
    }

    [Fact]
    public void MaintenanceHtml_IsSelfContainedAndNotIndexed()
    {
        var html = File.ReadAllText(RepoFile("docs/maintenance.html"));
        Assert.StartsWith("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<html lang=\"ja\">", html);
        Assert.Contains("name=\"robots\" content=\"noindex\"", html);
        Assert.Contains("メンテナンス", html);
        // 外のファイルを読み込まない（メンテナンス中は、サイトの CSS・画像が出せない）
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rel=\"stylesheet\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html);
        // 外へのリンクは、作者の Twitter だけ（読み込むファイルではなく、押して開くリンク）
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, "https://").Count);
        Assert.Contains("<a href=\"https://twitter.com/game_fara\"", html);
    }

    [Fact]
    public void SetupGuide_ExplainsTheSteps()
    {
        var guide = File.ReadAllText(RepoFile("docs/report-and-maintenance.md"));
        // 報告: 登録する 3 つの値と、6 つの Worker
        foreach (var name in new[] { "TURNSTILE_SITE_KEY", "TURNSTILE_SECRET", "DISCORD_WEBHOOK_URL" })
            Assert.Contains(name, guide);
        foreach (var worker in new[] { "fara-pokemon", "fara-pokemon-dev", "fara-avatar", "fara-avatar-dev", "fara-twitch-bot", "fara-twitch-bot-dev" })
            Assert.Contains($"`{worker}`", guide);
        // Turnstile に登録するホスト名（サイトのホストと同じ）
        foreach (var host in new[] { "pokemon.fara-labs.com", "pokemon-dev.fara-labs.com", "avatar.fara-labs.com", "avatar-dev.fara-labs.com", "twitch-bot.fara-labs.com", "twitch-bot-dev.fara-labs.com" })
            Assert.Contains(host, guide);
        // メンテナンス: Worker の変数で切り替える（3 つの状態と、お知らせの文）
        foreach (var name in new[] { "MAINTENANCE", "MAINTENANCE_MESSAGE", "`notice`", "`true`", "`false`", "keep_vars" })
            Assert.Contains(name, guide);
        // お知らせは、false に戻せば一緒に消える、と書いてある
        Assert.Contains("一緒に消えます", guide);
    }

    [Fact]
    public void CloudflareSetup_LinksToTheGuide()
    {
        Assert.Contains("report-and-maintenance.md", File.ReadAllText(RepoFile("docs/cloudflare-setup.md")));
    }
}
