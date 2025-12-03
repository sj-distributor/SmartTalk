using System.Diagnostics;
using System.Text;
using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.AutoTest;
using NAudio.Wave;
using NAudio.Lame;
using NAudio.Wave.SampleProviders;
using OpenAI.Audio;
using OpenAI.Chat;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Jobs;
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
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IAutoTestActionHandlerSwitcher _autoTestActionHandlerSwitcher;
    private readonly IAutoTestDataImportHandlerSwitcher _autoTestDataImportHandlerSwitcher;
    
    public AutoTestService(IMapper mapper, OpenAiSettings openAiSettings, IAgentDataProvider agentDataProvider, IAutoTestDataProvider autoTestDataProvider, ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, IAutoTestActionHandlerSwitcher autoTestActionHandlerSwitcher, IAutoTestDataImportHandlerSwitcher autoTestDataImportHandlerSwitcher)
    {
        _mapper = mapper;
        _openAiSettings = openAiSettings;
        _agentDataProvider = agentDataProvider;
        _autoTestDataProvider = autoTestDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _autoTestActionHandlerSwitcher = autoTestActionHandlerSwitcher;
        _autoTestDataImportHandlerSwitcher = autoTestDataImportHandlerSwitcher;
    }
    
    public async Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken)
    {
        Log.Information("AutoTestRunningAsync command:{@command}", command);
        
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        if (scenario == null) throw new Exception("Scenario not found");
        
        Log.Information("AutoTestRunningAsync scenario:{@scenario}", scenario);
        
        var taskRecords = await _autoTestDataProvider.GetStatusTaskRecordsByTaskIdAsync(command.TaskId, AutoTestTaskRecordStatus.Pending, cancellationToken).ConfigureAwait(false);
        
        taskRecords.ForEach(x => x.Status = AutoTestTaskRecordStatus.Ongoing);
        
        await _autoTestDataProvider.UpdateTaskRecordsAsync(taskRecords, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _autoTestActionHandlerSwitcher.GetHandler(scenario.ActionType, scenario.KeyName).ActionHandleAsync(scenario, command.TaskId, cancellationToken).ConfigureAwait(false);
        
        return new AutoTestRunningResponse();
    }

    public async Task<AutoTestConversationAudioProcessReponse> AutoTestConversationAudioProcessAsync(AutoTestConversationAudioProcessCommand command, CancellationToken cancellationToken)
    {
        var coreAudioRecords = await ProcessAudioConversationAsync(command.CustomerAudioList, command.Prompt, cancellationToken);

        return new AutoTestConversationAudioProcessReponse()
        {
            Data = coreAudioRecords
        };
    }

    private async Task<string> ProcessAudioConversationAsync(byte[] customerWav, string prompt, CancellationToken cancellationToken)
    {
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
        
        conversationHistory.Add(new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(customerWav), ChatInputAudioFormat.Wav)));
        
        var completion = await client.CompleteChatAsync(conversationHistory, options, cancellationToken);

        var aiReplyText = completion.Value.OutputAudio.Transcript ?? "";

        return aiReplyText;
    }


    private void MergeWavFilesToUniformFormat(List<string> wavFiles, string outputFile)
    {
        if (wavFiles.Count == 0)
            throw new ArgumentException("没有 WAV 文件可合并");

        var listFile = Path.GetTempFileName();
        File.WriteAllLines(listFile, wavFiles.Select(f => $"file '{f}'"));
        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -ar 24000 -ac 1 -acodec pcm_s16le \"{outputFile}\"";
        RunFfmpeg(args);
        File.Delete(listFile);
    }

    private void RunFfmpeg(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var err = process.StandardError.ReadToEnd();
            throw new Exception($"ffmpeg 执行失败：{err}");
        }
    }

    public async Task<GetAutoTestDataSetResponse> GetAutoTestDataSetsAsync(GetAutoTestDataSetRequest request, CancellationToken cancellationToken)
    {
        var (count, dataSets) = await _autoTestDataProvider.GetAutoTestDataSetsAsync(request?.Page, request?.PageSize, request?.KeyName, cancellationToken).ConfigureAwait(false);

        var recordImports  = await _autoTestDataProvider.GetImportDataRecordsByIdsAsync(dataSets.Select(x => x.ImportRecordId).ToList(), cancellationToken).ConfigureAwait(false);
        
        var records =  _mapper.Map<List<AutoTestDataSetDataRecords>>(dataSets);
        
        records.ForEach(x =>
        {
            var importRecord = recordImports.FirstOrDefault(y => y.Id == x.ImportRecordId);

            if (importRecord == null || string.IsNullOrWhiteSpace(importRecord.OpConfig)) return;
            
            var opConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(importRecord.OpConfig);
                
            if (opConfig.ContainsKey("StartDate") && opConfig["StartDate"] != null) x.StartDate = opConfig["StartDate"].ToString();

            if (opConfig.ContainsKey("StartDate") && opConfig["EndDate"] != null) x.EndDate = opConfig["EndDate"].ToString();
        });
        

        return new GetAutoTestDataSetResponse
        {
            Data = new GetAutoTestDataSetData
            {
                Count = count,
                Records = records
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
        
        await _autoTestDataProvider.AddAutoTestDataSetItemsAsync(newTargetItems, cancellationToken).ConfigureAwait(false);
        
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
            CreatedAt = DateTimeOffset.UtcNow,
            ImportRecordId = sets.ImportRecordId
        };
        
        await _autoTestDataProvider.AddAutoTestDataSetAsync(newDataSet, cancellationToken).ConfigureAwait(false);
        
        var dataItemIds = await _autoTestDataProvider.GetDataItemIdsByDataSetIdAsync(command.DataSetId, cancellationToken).ConfigureAwait(false);
        
        var newDataItems = dataItemIds.Select(dataItemId => new AutoTestDataSetItem
        {
            DataSetId = newDataSet.Id,
            DataItemId = dataItemId,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();
        
        await _autoTestDataProvider.AddAutoTestDataSetItemsAsync(newDataItems, cancellationToken).ConfigureAwait(false);

        return new AddAutoTestDataSetByQuoteResponse();
    }
}