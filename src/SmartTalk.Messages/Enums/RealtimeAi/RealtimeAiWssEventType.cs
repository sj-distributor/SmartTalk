namespace SmartTalk.Messages.Enums.RealtimeAi;

public enum RealtimeAiWssEventType
{
    Unknown,
    SessionInitializing,    // 会话正在初始化 (Engine 内部状态或发给外部)
    SessionInitialized,     // AI 服务商确认会话已初始化/更新
    SessionUpdateFailed,    // 会话初始化/更新失败
    AudioInputAppend,       // (此类型更多是 Engine 的一个动作，不一定作为事件对外)
    SpeechDetected,         // AI 服务商检测到用户开始说话
    SpeechEnded,            // AI 服务商检测到用户结束说话 (如果支持)
    TranscriptionPartial,   // 中间转录结果
    TranscriptionCompleted, // 最终转录结果
    ResponseAudioDelta,     // AI 的音频响应片段
    ResponseAudioDone,      // AI 的音频响应结束
    ResponseTextDelta,      // (如果适用) AI 的文本响应片段
    ResponseTurnCompleted,  // AI 完成了一轮完整的响应 (可能包含函数调用等)
    FunctionCallSuggested,  // AI 建议调用一个函数
    Error,                  // AI 服务商报告错误或引擎内部错误
    ConnectionStateChanged, // 底层 WebSocket 连接状态变更
}