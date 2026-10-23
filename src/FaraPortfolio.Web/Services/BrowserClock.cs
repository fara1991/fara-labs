namespace FaraPortfolio.Web.Services;

/// <summary>
/// 「今日」の日付（ブラウザの時計・ブラウザのタイムゾーン）。更新履歴の NEW（更新から 14 日以内）に使う。
/// WebAssembly の DateTime.Now はブラウザのタイムゾーンを持たないので、起動のときに JS（faraSite.today）から受け取る。
/// 静的な本文の書き出しでは Today を入れない（NEW は、書き出した日ではなく、見る人の今日で決める）。
/// </summary>
public sealed class BrowserClock
{
    public const int NewDays = 14;

    public DateOnly? Today { get; set; }

    public bool IsNew(DateOnly date) =>
        Today is { } today && today.DayNumber - date.DayNumber is >= 0 and <= NewDays;
}
