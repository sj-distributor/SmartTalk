using AutoMapper;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.AutoTest;
using NAudio.Wave;
using NAudio.Lame;
using OpenAI.Audio;
using OpenAI.Chat;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService : IScopedDependency
{
    Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken);

    Task<AutoTestConversationAudioProcessReponse> AutoTestConversationAudioProcessAsync(AutoTestConversationAudioProcessCommand command, CancellationToken cancellationToken);
    
    Task<GetAutoTestDataSetResponse> GetAutoTestDataSetsAsync(GetAutoTestDataSetRequest request, CancellationToken cancellationToken);
    
    Task<GetAutoTestDataItemsByIdResponse> GetAutoTestDataItemsByIdAsync(GetAutoTestDataItemsByIdRequest request, CancellationToken cancellationToken);

    Task<CopyAutoTestDataSetResponse> CopyAutoTestDataItemsAsync(CopyAutoTestDataSetCommand command, CancellationToken cancellationToken);

    Task<DeleteAutoTestDataSetResponse> DeleteAutoTestDataItemAsync(DeleteAutoTestDataSetCommand command, CancellationToken cancellationToken);

    Task<AddAutoTestDataSetByQuoteResponse> AddAutoTestDataSetByQuoteAsync(AddAutoTestDataSetByQuoteCommand byQuoteCommand, CancellationToken cancellationToken);
}

public partial class AutoTestService : IAutoTestService
{
    private readonly IMapper _mapper;
    private readonly OpenAiSettings _openAiSettings;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IAutoTestActionHandlerSwitcher _autoTestActionHandlerSwitcher;
    private readonly IAutoTestDataImportHandlerSwitcher _autoTestDataImportHandlerSwitcher;

    public AutoTestService(IMapper mapper, OpenAiSettings openAiSettings, IAutoTestDataProvider autoTestDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, IAutoTestActionHandlerSwitcher autoTestActionHandlerSwitcher, IAutoTestDataImportHandlerSwitcher autoTestDataImportHandlerSwitcher)
    {
        _mapper = mapper;
        _openAiSettings = openAiSettings;
        _autoTestDataProvider = autoTestDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _autoTestActionHandlerSwitcher = autoTestActionHandlerSwitcher;
        _autoTestDataImportHandlerSwitcher = autoTestDataImportHandlerSwitcher;
    }
    
    public async Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var taskRecords = await _autoTestDataProvider.GetStatusTaskRecordsByTaskIdAsync(command.TaskId, AutoTestTaskRecordStatus.Pending, cancellationToken).ConfigureAwait(false);
        
        taskRecords.ForEach(x => x.Status = AutoTestTaskRecordStatus.Ongoing);
        
        await _autoTestDataProvider.UpdateTaskRecordsAsync(taskRecords, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var executionResult = await _autoTestActionHandlerSwitcher.GetHandler(scenario.ActionType).ActionHandleAsync(scenario, command.TaskId, cancellationToken).ConfigureAwait(false);
        
        return new AutoTestRunningResponse() { Data = executionResult };
    }

    public async Task<AutoTestConversationAudioProcessReponse> AutoTestConversationAudioProcessAsync(AutoTestConversationAudioProcessCommand command, CancellationToken cancellationToken)
    {
        var coreAudioRecords = await ProcessAudioConversationAsync(command.CustomerAudioList, command.Prompt, cancellationToken);

        return new AutoTestConversationAudioProcessReponse()
        {
            Data = coreAudioRecords
        };
    }

    private async Task<byte[]> ProcessAudioConversationAsync(
        List<byte[]> customerWav8kList,
        string prompt,
        CancellationToken cancellationToken)
    {
        if (customerWav8kList == null || customerWav8kList.Count == 0)
            throw new ArgumentException("没有音频输入");

        var conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var client = new ChatClient("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        var options = new ChatCompletionOptions
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Mp3)
        };

        var allAudioSegments = new List<byte[]>();

        foreach (var wavBytes in customerWav8kList)
        {
            if (wavBytes == null || wavBytes.Length == 0)
                continue;

            // Step 1: 转成 16kHz MP3
            var mp316kBytes = ConvertWav8kToMp3_16k(wavBytes);
            allAudioSegments.Add(mp316kBytes);

            // Step 2: 发送给 Chat
            conversationHistory.Add(new UserChatMessage(
                ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(mp316kBytes), ChatInputAudioFormat.Mp3)
            ));

            var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);
            var aiMp3 = completion.Value.OutputAudio.AudioBytes.ToArray();

            allAudioSegments.Add(aiMp3);

            // 文本记录
            conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));
        }

        // Step 3: 拼接所有 MP3
        return MergeMp3Segments(allAudioSegments);
    }

// ---------------- 8kHz WAV -> 16kHz MP3 ----------------
    private byte[] ConvertWav8kToMp3_16k(byte[] wavBytes)
    {
        using var wavStream = new MemoryStream(wavBytes);
        using var reader = new WaveFileReader(wavStream);

        var outFormat = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(reader, outFormat)
        {
            ResamplerQuality = 60
        };

        using var mp3Stream = new MemoryStream();
        using (var mp3Writer = new LameMP3FileWriter(mp3Stream, resampler.WaveFormat, LAMEPreset.STANDARD))
        {
            var buffer = new byte[resampler.WaveFormat.AverageBytesPerSecond];
            int read;
            while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
            {
                mp3Writer.Write(buffer, 0, read);
            }
        }

        return mp3Stream.ToArray();
    }

// ---------------- MP3 拼接 ----------------
    private byte[] MergeMp3Segments(List<byte[]> mp3Segments)
    {
        if (mp3Segments == null || mp3Segments.Count == 0)
            throw new ArgumentException("没有音频输入");

        using var finalStream = new MemoryStream();

        foreach (var segment in mp3Segments)
        {
            if (segment == null || segment.Length == 0)
                continue;

            finalStream.Write(segment, 0, segment.Length);
        }

        return finalStream.ToArray();
    }

    public async Task<GetAutoTestDataSetResponse> GetAutoTestDataSetsAsync(GetAutoTestDataSetRequest request, CancellationToken cancellationToken)
    {
        var (count, dataSets) = await _autoTestDataProvider.GetAutoTestDataSetsAsync(request?.Page, request?.PageSize, request?.KeyName, cancellationToken).ConfigureAwait(false);
        
        return new GetAutoTestDataSetResponse
        {
            Data = new GetAutoTestDataSetData
            {
                Count = count,
                Records = dataSets.Select(x => _mapper.Map<AutoTestDataSetDto>(x)).ToList()
            }
        };
    }

    public async Task<GetAutoTestDataItemsByIdResponse> GetAutoTestDataItemsByIdAsync(GetAutoTestDataItemsByIdRequest request, CancellationToken cancellationToken)
    {
        var (count, dataItems) = await _autoTestDataProvider.GetAutoTestDataItemsBySetIdAsync(request.DataSetId, request.Page, request.PageSize, cancellationToken).ConfigureAwait(false);

        return new GetAutoTestDataItemsByIdResponse
        {
            Data = new GetAutoTestDataItemsByIdData
            {
                Count = count,
                Records = dataItems.Select(x => _mapper.Map<AutoTestDataItemDto>(x)).ToList()
            }
        };
    }

    public async Task<CopyAutoTestDataSetResponse> CopyAutoTestDataItemsAsync(CopyAutoTestDataSetCommand command, CancellationToken cancellationToken)
    { 
        if (command.ItemIds is null || command.ItemIds.Count == 0) throw new Exception("Please select the DataItem you want to copy.");
        
        var sourceItemIds = await _autoTestDataProvider.GetDataItemIdsByDataSetIdAsync(command.SourceDataSetId, cancellationToken).ConfigureAwait(false);
        
        var pickIds = command.ItemIds.Distinct().ToList();
        
        if (pickIds.Count == 0) throw new Exception("The selected DataItem does not exist in the source dataset.");
        
        var newTargetItems = pickIds.Select(id => new AutoTestDataSetItem
        {
            DataSetId = command.TargetDataSetId,
            DataItemId = id, 
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();
        
        await _autoTestDataProvider.AddAutoTestDataItemsAsync(newTargetItems, cancellationToken).ConfigureAwait(false);
        
        return new CopyAutoTestDataSetResponse();
    }

    public async Task<DeleteAutoTestDataSetResponse> DeleteAutoTestDataItemAsync(DeleteAutoTestDataSetCommand command, CancellationToken cancellationToken)
    {
        var itemIds = await _autoTestDataProvider.GetDataItemIdsByDataSetIdAsync(command.DataSetId, cancellationToken).ConfigureAwait(false);
        
        if(itemIds.Count == 0) throw new Exception("DataItem not found."); 
        
        var invalidItemIds = command.ItemsIds
            .Where(itemId => !itemIds.Contains(itemId))
            .ToList();
        
        if (invalidItemIds.Any()) throw new Exception("The selected DataItem does not exist in the source dataset.");
        
        await _autoTestDataProvider.DeleteAutoTestDataItemAsync(command.ItemsIds, command.DataSetId, cancellationToken).ConfigureAwait(false);
        
        return new DeleteAutoTestDataSetResponse();
    }

    public async Task<AddAutoTestDataSetByQuoteResponse> AddAutoTestDataSetByQuoteAsync(AddAutoTestDataSetByQuoteCommand command, CancellationToken cancellationToken)
    {
        var sets = await _autoTestDataProvider.GetAutoTestDataSetByIdAsync(command.DataSetId, cancellationToken).ConfigureAwait(false);
        
        if(sets == null) throw new Exception("DataSet not found");
        
        var newDataSet = new AutoTestDataSet
        {
            ScenarioId = sets.ScenarioId,
            KeyName = sets.KeyName + "-" + DateTimeOffset.UtcNow.ToString("yyyy:MM:dd:HH:mm:ss"),
            Name = sets.Name + "-" + DateTimeOffset.UtcNow.ToString("yyyy:MM:dd:HH:mm:ss"),
            IsDelete = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        await _autoTestDataProvider.AddAutoTestDataSetAsync(newDataSet, cancellationToken).ConfigureAwait(false);
        
        var dataItemIds = await _autoTestDataProvider.GetDataItemIdsByDataSetIdAsync(command.DataSetId, cancellationToken).ConfigureAwait(false);
        
        var newDataItems = dataItemIds.Select(dataItemId => new AutoTestDataSetItem
        {
            DataSetId = newDataSet.Id,
            DataItemId = dataItemId,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();
        
        await _autoTestDataProvider.AddAutoTestDataItemsAsync(newDataItems, cancellationToken).ConfigureAwait(false);

        return new AddAutoTestDataSetByQuoteResponse();
    }
}