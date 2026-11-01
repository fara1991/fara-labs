namespace FaraPortfolio.Prerender;

/// <summary>PrerenderPages の引数（--data・--host・--out・--not-found-out）。</summary>
public sealed record PrerenderOptions(string DataDir, string Host, string OutFile, string NotFoundOut);

/// <summary>
/// コマンドラインの引数を読む。値が無い・知らない引数・必要な引数の欠けは、理由（日本語）を Error に返す（例外にしない）。
/// </summary>
public static class PrerenderArgs
{
    public const string Usage = "使い方: dotnet run --project tools/PrerenderPages -c Release -- --data <data フォルダ> --host <公開するホスト名> --out <pages.json> --not-found-out <not-found.json>";

    private static readonly string[] Names = ["--data", "--host", "--out", "--not-found-out"];

    public static (PrerenderOptions? Options, string? Error) Parse(string[] args)
    {
        var values = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (!Names.Contains(name)) return (null, $"知らない引数: {name}");
            if (i + 1 >= args.Length) return (null, $"{name} の後に値がありません");
            values[name] = args[++i];
        }

        if (Names.Any(name => !values.ContainsKey(name)))
        {
            return (null, "--data・--host・--out・--not-found-out を指定してください");
        }

        return (new PrerenderOptions(values["--data"], values["--host"], values["--out"], values["--not-found-out"]), null);
    }
}
