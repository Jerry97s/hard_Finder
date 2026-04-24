namespace HdLabs.Memo.Ai;

public sealed record AiMemoContext(
    string TitlePlain,
    string TitleXaml,
    string BodyPlain,
    string BodyXaml);

public sealed record AiChatRequest(
    string Message,
    bool IncludeMemo,
    AiMemoContext? Memo,
    string? Mode);

public sealed record AiChatResponse(
    string Reply);

