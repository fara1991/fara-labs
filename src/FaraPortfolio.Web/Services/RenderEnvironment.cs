namespace FaraPortfolio.Web.Services;

/// <summary>
/// 画面を描いている場所。ブラウザ（Blazor が動いている）か、静的な本文の書き出し（tools/PrerenderPages。押せるボタンは出さない）か。
/// ブラウザは Program.cs が IsInteractive = true で登録する。
/// </summary>
public sealed record RenderEnvironment(bool IsInteractive);
