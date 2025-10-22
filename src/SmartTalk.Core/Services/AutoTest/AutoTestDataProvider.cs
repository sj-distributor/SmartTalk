using AutoMapper;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider : IScopedDependency
{
    Task<AutoTestScenario> GetAutoTestScenarioByIdAsync(int id, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider : IAutoTestDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public AutoTestDataProvider(IRepository repository, IMapper mapper, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async Task<AutoTestScenario> GetAutoTestScenarioByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<AutoTestScenario>(id, cancellationToken).ConfigureAwait(false);
    }
}