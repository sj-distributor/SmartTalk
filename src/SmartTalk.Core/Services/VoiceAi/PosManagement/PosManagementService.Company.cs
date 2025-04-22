using AutoMapper;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Events.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementService : IScopedDependency
{
    Task<PosCompanyCreatedEvent> CreatePosCompanyAsync(CreatePosCompanyCommand command, CancellationToken cancellationToken);

    Task<PosCompanyUpdatedEvent> UpdatePosCompanyAsync(UpdatePosCompanyCommand command, CancellationToken cancellationToken);

    Task<PosCompanyUpdatedStatusEvent> UpdatePosCompanyStatusAsync(UpdatePosCompanyStatusCommand command, CancellationToken cancellationToken);
    
    Task<PosCompanyDeletedEvent> DeletePosCompanyAsync(DeletePosCompanyCommand command, CancellationToken cancellationToken);

    Task<GetPosCompanyDetailResponse> GetPosCompanyDetailAsync(GetPosCompanyDetailRequest request, CancellationToken cancellationToken);
}

public partial class PosManagementService : IPosManagementService
{
    public async Task<PosCompanyCreatedEvent> CreatePosCompanyAsync(CreatePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = new PosCompany
        {
            Name = command.Name, Description = command.Description, Status = false
        };

        await _posManagementDataProvider.CreatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyCreatedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyUpdatedEvent> UpdatePosCompanyAsync(UpdatePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        company.Name = command.Name;
        company.Description = command.Description;

        await _posManagementDataProvider.UpdatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyUpdatedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyUpdatedStatusEvent> UpdatePosCompanyStatusAsync(UpdatePosCompanyStatusCommand command, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        company.Status = command.Status;

        await _posManagementDataProvider.UpdatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyUpdatedStatusEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyDeletedEvent> DeletePosCompanyAsync(DeletePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (company == null) throw new Exception("Can't find company with id:" + command.Id);
        
        await _posManagementDataProvider.DeletePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        // todo delete stores

        return new PosCompanyDeletedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<GetPosCompanyDetailResponse> GetPosCompanyDetailAsync(GetPosCompanyDetailRequest request, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (company == null) throw new Exception("Can't find company with id:" + request.Id);
        
        return new GetPosCompanyDetailResponse
        {
            Data = _mapper.Map<PosCompanyDto>(company)
        };
    }
}