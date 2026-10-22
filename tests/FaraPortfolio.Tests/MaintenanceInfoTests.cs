using System.Net;
using FaraPortfolio.Web.Services;
using Xunit;

namespace FaraPortfolio.Tests;

/// <summary>メンテナンスの状態（Worker の変数 MAINTENANCE / MAINTENANCE_MESSAGE）の読み取り。読めなければ通常として扱う。</summary>
public class MaintenanceInfoTests
{
    [Theory]
    [InlineData("{\"state\":\"off\",\"message\":\"\"}", MaintenanceState.Off)]
    [InlineData("{\"state\":\"notice\",\"message\":\"20:00ごろ\"}", MaintenanceState.Notice)]
    [InlineData("{\"state\":\"on\",\"message\":\"20:00ごろ\"}", MaintenanceState.On)]
    [InlineData("{\"state\":\"ON\"}", MaintenanceState.On)]
    [InlineData("{\"state\":\" notice \"}", MaintenanceState.Notice)]
    public void The_state_is_read_from_the_json(string json, MaintenanceState expected) =>
        Assert.Equal(expected, MaintenanceInfo.Parse(json).State);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<!doctype html><title>app</title>")]   // 開発中など、Worker が無くて index.html が返ってきたとき
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"state\":5}")]
    [InlineData("{\"state\":\"maintenance\"}")]
    [InlineData("{\"state\":null}")]
    public void Anything_unreadable_is_treated_as_normal(string? json) => Assert.Equal(MaintenanceInfo.Off, MaintenanceInfo.Parse(json));

    [Fact]
    public void The_message_is_kept_only_while_a_notice_or_maintenance_is_on()
    {
        Assert.Equal("20:00ごろ", MaintenanceInfo.Parse("{\"state\":\"notice\",\"message\":\"  20:00ごろ \"}").Message);
        Assert.Equal("20:00ごろ", MaintenanceInfo.Parse("{\"state\":\"on\",\"message\":\"20:00ごろ\"}").Message);
        Assert.Equal("", MaintenanceInfo.Parse("{\"state\":\"off\",\"message\":\"20:00ごろ\"}").Message);   // 通常に戻したら、お知らせも消える
        Assert.Equal("", MaintenanceInfo.Parse("{\"state\":\"notice\",\"message\":5}").Message);
        Assert.Equal("", MaintenanceInfo.Parse("{\"state\":\"notice\"}").Message);
    }

    [Fact]
    public void The_notice_text_falls_back_to_a_default()
    {
        Assert.Equal("まもなくメンテナンスを行います。", new MaintenanceInfo(MaintenanceState.Notice, "").NoticeText);
        Assert.Equal("まもなくメンテナンスを行います。", new MaintenanceInfo(MaintenanceState.Notice, "  ").NoticeText);
        Assert.Equal("10月25日 20:00ごろ", new MaintenanceInfo(MaintenanceState.Notice, "10月25日 20:00ごろ").NoticeText);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
        public string? RequestedPath { get; private set; }
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => this.respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPath = request.RequestUri!.AbsolutePath;
            return respond(request, cancellationToken);
        }
    }

    private static HttpClient Client(FakeHandler handler) => new(handler) { BaseAddress = new Uri("https://pokemon.example/") };

    [Fact]
    public async Task Load_reads_the_state_from_the_maintenance_endpoint()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"state\":\"on\",\"message\":\"20:00ごろ\"}") }));
        var info = await MaintenanceInfo.LoadAsync(Client(handler));
        Assert.Equal(new MaintenanceInfo(MaintenanceState.On, "20:00ごろ"), info);
        Assert.Equal("/api/maintenance", handler.RequestedPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Load_treats_an_error_response_as_normal(HttpStatusCode status)
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"state\":\"on\"}") }));
        Assert.Equal(MaintenanceInfo.Off, await MaintenanceInfo.LoadAsync(Client(handler)));
    }

    [Fact]
    public async Task Load_treats_a_network_failure_as_normal()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("offline"));
        Assert.Equal(MaintenanceInfo.Off, await MaintenanceInfo.LoadAsync(Client(handler)));
    }

    [Fact]
    public async Task Load_treats_an_unexpected_exception_as_normal()
    {
        var handler = new FakeHandler((_, _) => throw new InvalidOperationException("unexpected"));
        Assert.Equal(MaintenanceInfo.Off, await MaintenanceInfo.LoadAsync(Client(handler)));
    }

    [Fact]
    public async Task Load_gives_up_quickly_by_default()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"state\":\"on\"}") };
        });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(MaintenanceInfo.Off, await MaintenanceInfo.LoadAsync(Client(handler)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), "既定の待ち時間は 1 秒（起動を長く止めない）");
    }

    [Fact]
    public async Task Load_does_not_make_the_app_wait_for_a_slow_endpoint()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);   // キャンセルされるまで待つ
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"state\":\"on\"}") };
        });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var info = await MaintenanceInfo.LoadAsync(Client(handler), TimeSpan.FromMilliseconds(100));
        Assert.Equal(MaintenanceInfo.Off, info);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "時間切れで、通常として起動する");
    }

    [Fact]
    public void The_shipped_wrangler_config_keeps_dashboard_variables_and_runs_the_worker_only_for_the_api()
    {
        // 公開（wrangler deploy）のたびに、ダッシュボードの MAINTENANCE が消えないように。keep_vars はトップレベルだけが有効
        var text = File.ReadAllText(FindUp("wrangler.jsonc"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, @"""keep_vars""\s*:\s*true"));
        Assert.True(text.IndexOf("keep_vars", StringComparison.Ordinal) < text.IndexOf("\"env\"", StringComparison.Ordinal), "トップレベルにある（env の中ではない）");
        // サイト本体は静的アセットのまま（Worker を通すのは /api/* だけ。無料枠のリクエスト数を使わない）
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, @"""run_worker_first"": \[""/api/\*""\]").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, @"""binding"": ""ASSETS""").Count);
        Assert.Contains("\"main\": \"worker/index.js\"", text);
    }

    [Fact]
    public void Production_deploy_requires_MAINTENANCE_notice_and_turns_maintenance_on()
    {
        var yml = File.ReadAllText(FindUp(".github/workflows/deploy.yml"));
        Assert.Contains("node .github/scripts/require-maintenance.mjs fara-portfolio", yml);
        Assert.True(yml.IndexOf("require-maintenance.mjs", StringComparison.Ordinal) < yml.IndexOf("npx wrangler deploy", StringComparison.Ordinal), "確認は公開より前");
        Assert.Contains("--var MAINTENANCE:true", yml);
        Assert.Contains("inputs.skip_maintenance_check", yml);
        Assert.Contains("skip_maintenance_check:", yml);
        // 確認用（develop）の公開は、確認もメンテナンス化もしない
        var develop = File.ReadAllText(FindUp(".github/workflows/deploy-develop.yml"));
        Assert.DoesNotContain("require-maintenance", develop);
        Assert.DoesNotContain("MAINTENANCE:true", develop);
    }

    [Fact]
    public void Gate_script_and_maintenance_tests_run_in_ci()
    {
        Assert.True(File.Exists(FindUp(".github/scripts/require-maintenance.mjs")));
        var build = File.ReadAllText(FindUp(".github/workflows/build.yml"));
        Assert.Contains("test:gate", build);
        Assert.Contains("test:maintenance", build);
    }

    [Fact]
    public void Worker_returns_the_state_and_stops_other_apis_during_maintenance()
    {
        var js = File.ReadAllText(FindUp("worker/index.js"));
        Assert.Contains("url.pathname === \"/api/maintenance\"", js);
        Assert.Contains("maintenance.state === \"on\" && url.pathname.startsWith(\"/api/\")", js);
        Assert.Contains("env.ASSETS.fetch(request)", js);
    }

    private static string FindUp(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }
}
