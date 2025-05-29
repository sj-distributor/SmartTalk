# Realtime AI 双向通信接口文档 🎤💬

本文档定义了用于双向音频流通信的 WebSocket 接口事件。客户端通过此接口发送音频数据，并接收来自服务器的实时事件和响应音频。

## 概述

该 WebSocket 接口实现了客户端与服务器之间的双向实时通信。客户端发送包含音频数据的 `input event`，服务器则通过一系列 `output event` 进行响应，包括会话初始化、人声检测以及音频回应。

------

## 连接

- **端点 (Endpoint)**: `ws://{smarttalk domian}/api/Realtime/connect/{assistantId}` 
- Query Parameters: 
  - smarttalk domian 測試：smarttalktest.yamimeal.ca、PRD：smarttalk.yamimeal.ca
  - assistantId：選擇需要對話的助手
- Hearders: 
  - X-API-KEY：諮詢開發獲取權限
  - InputFormat、OutputFormat：輸入輸出音頻格式，目前支持`g711_ulaw`、`g711_alaw`、`pcm16`，輸入其他會報錯

------

## 输入事件 (Client -> Server)

客户端向服务器发送的事件。

### 1. 音频输入 (Audio Input)

当客户端有音频数据需要发送给服务器处理时，发送此事件。

**事件名**: 无 (直接发送 JSON 数据)

**JSON 结构**:

JSON

```
{
  "media": {
    "payload": "string"
  }
}
```

**字段说明**:

- ```
  media
  ```

   (object): 包含媒体数据的对象。

  - `payload` (string): Base64 编码的音频数据片段。**注意**: 请明确音频的编码格式、采样率、声道数等信息，或约定默认格式。

------

## 输出事件 (Server -> Client)

服务器向客户端发送的事件。所有输出事件都包含一个 `type` 字段来区分事件类型和一个 `session_id` 字段来标识当前会话。

### 1. 会话成功初始化 (Session Initialized)

当 WebSocket 连接建立并且服务器成功初始化会话后，发送此事件。

**JSON 结构**:

JSON

```
{
  "type": "SessionInitialized",
  "session_id": "string"
}
```

**字段说明**:

- `type` (string): 事件类型，固定为 `"SessionInitialized"`。
- `session_id` (string): 唯一标识当前会话的 ID。后续所有与此会话相关的事件都将包含此 ID。

### 2. 识别到人声 (Speech Detected)

当服务器在输入音频流中检测到人声活动时，发送此事件。

**JSON 结构**:

JSON

```
{
  "type": "SpeechDetected",
  "session_id": "string"
}
```

**字段说明**:

- `type` (string): 事件类型，固定为 `"SpeechDetected"`。
- `session_id` (string): 当前会话的 ID。

### 3. 回应音频片段 (Response Audio Delta)

当服务器有处理后的音频片段（例如，语音合成的回应）需要发送给客户端时，发送此事件。这通常是流式响应的一部分。

**JSON 结构**:

JSON

```
{
  "type": "ResponseAudioDelta",
  "Data": {
    "Base64Payload": "string"
  },
  "session_id": "string"
}
```

**字段说明**:

- `type` (string): 事件类型，固定为 `"ResponseAudioDelta"`。

- ```
  Data
  ```

   (object): 包含音频数据的对象。

  - `Base64Payload` (string): Base64 编码的音频数据片段。**注意**: 请明确此音频片段的编码格式、采样率、声道数等信息。

- `session_id` (string): 当前会话的 ID (建议添加此字段以保持一致性)。

------

## 通信流程示例

1. 客户端与 WebSocket 服务器建立连接。
2. 服务器发送 **会话成功初始化 (SessionInitialized)** 事件，包含 `session_id`。
3. 客户端开始发送 **音频输入 (Audio Input)** 事件流。
4. 当服务器在音频流中检测到人声时，发送 **识别到人声 (SpeechDetected)** 事件。
5. 服务器处理音频并生成回应，通过一个或多个 **回应音频片段 (ResponseAudioDelta)** 事件将音频流式传输回客户端。
6. 通信持续进行，直到任一方关闭连接。