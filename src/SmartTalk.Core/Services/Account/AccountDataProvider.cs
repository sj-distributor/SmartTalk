using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Messages.Dto.Users;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;

namespace SmartTalk.Core.Services.Account
{
    public interface IAccountDataProvider : IScopedDependency
    {
        Task<UserAccount> GetUserAccountAsync(int id, CancellationToken cancellationToken = default);
        
        Task<UserAccountDto> GetUserAccountByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    }
    
    public partial class AccountDataProvider : IAccountDataProvider
    {
        private readonly IMapper _mapper;
        private readonly IRepository _repository;

        public AccountDataProvider(IRepository repository, IMapper mapper)
        {
            _mapper = mapper;
            _repository = repository;
        }

        public async Task<UserAccount> GetUserAccountAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _repository.QueryNoTracking<UserAccount>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        }
        
        public async Task<UserAccountDto> GetUserAccountByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            Log.Information($"SmartTalk apiKey: {apiKey}", apiKey);
            
            var accountApiKey = await _repository.QueryNoTracking<UserAccountApiKey>()
                .Where(x => x.ApiKey == apiKey)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (accountApiKey == null)
                return null;

            var account = await GetUserAccountAsync(id: accountApiKey.UserAccountId, cancellationToken: cancellationToken).ConfigureAwait(false);
 
            return account != null ? _mapper.Map<UserAccountDto>(account) : null;
        }
    }
}