using SmartTalk.Core.Domain.Hr;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.Hr;
using SmartTalk.Messages.Requests.Hr;

namespace SmartTalk.Core.Services.Hr;

public interface IHrService : IScopedDependency
{
    Task AddHrInterviewQuestionsAsync(AddHrInterviewQuestionsCommand command, CancellationToken cancellationToken = default);

    Task<GetCurrentInterviewQuestionsResponse> GetCurrentInterviewQuestionsAsync(GetCurrentInterviewQuestionsRequest request, CancellationToken cancellationToken = default);
}

public class HrService : IHrService
{
    private readonly IHrDataProvider _hrDataProvider;

    public HrService(IHrDataProvider hrDataProvider)
    {
        _hrDataProvider = hrDataProvider;
    }

    public async Task AddHrInterviewQuestionsAsync(AddHrInterviewQuestionsCommand command, CancellationToken cancellationToken = default)
    {
        var questions = command.Questions.Select(x => new HrInterviewQuestion
        {
            Question = x,
            Section = command.Section,
            IsUsing = false
        }).ToList();
        
        await _hrDataProvider.AddHrInterviewQuestionsAsync(questions, cancellationToken: cancellationToken);
    }

    public async Task<GetCurrentInterviewQuestionsResponse> GetCurrentInterviewQuestionsAsync(GetCurrentInterviewQuestionsRequest request, CancellationToken cancellationToken = default)
    {
        var questions = await _hrDataProvider.GetHrInterviewQuestionDtosAsync(section: request.Section, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetCurrentInterviewQuestionsResponse
        {
            Data = new GetCurrentInterviewQuestionsResponseData()
            {
                Questions = questions
            }
        };
    }
}