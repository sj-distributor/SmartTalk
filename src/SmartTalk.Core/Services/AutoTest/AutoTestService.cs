using System.Diagnostics;
using AutoMapper;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.AutoTest;
using NAudio.Wave;
using NAudio.Lame;
using NAudio.Wave.SampleProviders;
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

    private async Task<byte[]> ProcessAudioConversationAsync(List<byte[]> customerMp3List, string prompt, CancellationToken cancellationToken)
    {
        if (customerMp3List == null || customerMp3List.Count == 0)
            throw new ArgumentException("没有音频输入");

        var conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var client = new ChatClient("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        var options = new ChatCompletionOptions
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(ChatOutputAudioVoice.Alloy, ChatOutputAudioFormat.Wav)
        };

        var wavFiles = new List<string>();

        try
        {
            foreach (var userMp3 in customerMp3List)
            {
                if (userMp3 == null || userMp3.Length == 0)
                    continue;

                var wavFile = Path.GetTempFileName() + ".wav";
                ConvertMp3ToWav(userMp3, wavFile);
                wavFiles.Add(wavFile);

                conversationHistory.Add(new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(File.ReadAllBytes(wavFile)), ChatInputAudioFormat.Wav)));

                var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

                var aiWavFile = Path.GetTempFileName() + ".wav";
                await File.WriteAllBytesAsync(aiWavFile, completion.Value.OutputAudio.AudioBytes.ToArray(), cancellationToken);
                wavFiles.Add(aiWavFile);

                conversationHistory.Add(new AssistantChatMessage(completion.Value.OutputAudio.Transcript));
            }

            var mergedWavFile = Path.GetTempFileName() + ".wav";
            MergeWavFiles(wavFiles, mergedWavFile);

            return await File.ReadAllBytesAsync(mergedWavFile, cancellationToken);
        }
        finally
        {
            foreach (var f in wavFiles)
            {
                if (File.Exists(f)) File.Delete(f);
            }
        }
    }

    private void ConvertMp3ToWav(byte[] mp3Bytes, string outputWavFile)
    {
        var tempMp3 = Path.GetTempFileName() + ".mp3";
        File.WriteAllBytes(tempMp3, mp3Bytes);

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -i \"{tempMp3}\" \"{outputWavFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();

        File.Delete(tempMp3);
    }

    private void MergeWavFiles(List<string> wavFiles, string outputFile)
    {
        if (wavFiles.Count == 0)
            throw new ArgumentException("没有 WAV 文件可合并");

        var listFile = Path.GetTempFileName();
        File.WriteAllLines(listFile, wavFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'"));

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();

        File.Delete(listFile);
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