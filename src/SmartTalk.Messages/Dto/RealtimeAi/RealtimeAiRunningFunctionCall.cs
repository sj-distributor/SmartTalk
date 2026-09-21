namespace SmartTalk.Messages.Dto.RealtimeAi;

/// <summary>
/// The tool handler currently holding the provider receive loop, and when it started.
///
/// <para>One reference rather than two fields so a reader can never pair a stale start stamp with a
/// newer tool's name.</para>
/// </summary>
public sealed record RealtimeAiRunningFunctionCall(long StartedAt, string FunctionName);
