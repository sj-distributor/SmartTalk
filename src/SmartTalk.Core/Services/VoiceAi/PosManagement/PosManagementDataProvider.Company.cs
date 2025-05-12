using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider : IScopedDependency
{
    Task CreatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task UpdatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task DeletePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default);

    Task<PosCompany> GetPosCompanyAsync(int id, CancellationToken cancellationToken);
}

public partial class PosManagementDataProvider : IPosManagementDataProvider
{
    public async Task CreatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosCompanyAsync(PosCompany company, bool isForceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(company, cancellationToken).ConfigureAwait(false);
        
        if (isForceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PosCompany> GetPosCompanyAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosCompany>(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}