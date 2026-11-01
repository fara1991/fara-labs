using FaraPortfolio.Prerender;

namespace FaraPortfolio.Tests;

/// <summary>tools/PrerenderPages の引数の読み取り（値の欠け・知らない引数は、例外でなく理由を返す）。</summary>
public sealed class PrerenderArgsTests
{
    private static readonly string[] Complete =
    [
        "--data", "d", "--host", "h", "--out", "o.json", "--not-found-out", "n.json",
    ];

    [Fact]
    public void 引数がすべてそろえば_値を読む()
    {
        var (options, error) = PrerenderArgs.Parse(Complete);

        Assert.Null(error);
        Assert.Equal(new PrerenderOptions("d", "h", "o.json", "n.json"), options);
    }

    [Fact]
    public void 値の無い引数は_例外でなく理由を返す()
    {
        var (options, error) = PrerenderArgs.Parse(["--data", "d", "--host"]);

        Assert.Null(options);
        Assert.Equal("--host の後に値がありません", error);
    }

    [Fact]
    public void 知らない引数は_理由を返す()
    {
        var (options, error) = PrerenderArgs.Parse(["--bogus", "x"]);

        Assert.Null(options);
        Assert.Equal("知らない引数: --bogus", error);
    }

    [Fact]
    public void 必要な引数が欠けたら_理由を返す()
    {
        var (options, error) = PrerenderArgs.Parse(["--data", "d", "--host", "h", "--out", "o.json"]);

        Assert.Null(options);
        Assert.Equal("--data・--host・--out・--not-found-out を指定してください", error);
    }
}
