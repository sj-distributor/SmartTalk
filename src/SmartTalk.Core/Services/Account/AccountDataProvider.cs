using System.Security.Claims;
using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Messages.Dto.Users;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Account.Exceptions;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Core.Services.Account
{
    public interface IAccountDataProvider : IScopedDependency
    {
        Task<UserAccount> GetUserAccountAsync(int id, CancellationToken cancellationToken = default);
        
        Task<UserAccountDto> GetUserAccountByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

        Task<UserAccountDto> GetUserAccountDtoAsync(
            int? id = null, string username = null, string password = null, string thirdPartyUserId = null, bool isActive = true, UserAccountIssuer? issuer = null, bool includeRoles = false, CancellationToken cancellationToken = default);

        List<Claim> GenerateClaimsFromUserAccount(UserAccountDto account);

        Task<UserAccount> CreateUserAccountAsync(
            string requestUserName, string requestPassword, string thirdPartyUserId = null,
            UserAccountIssuer authType = UserAccountIssuer.Self, UserAccountProfile profile = null, CancellationToken cancellationToken = default);
    }
    
    public partial class AccountDataProvider : IAccountDataProvider
    {
        private readonly IMapper _mapper;
        private readonly IRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public AccountDataProvider(IRepository repository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
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
        
        public async Task<UserAccountDto> GetUserAccountDtoAsync(
            int? id = null, string username = null, string password = null, string thirdPartyUserId = null, bool isActive = true, UserAccountIssuer? issuer = null, bool includeRoles = false, CancellationToken cancellationToken = default)
        {
            var account = await GetUserAccountAsync(id, username, password, thirdPartyUserId, isActive, issuer, includeRoles, cancellationToken).ConfigureAwait(false);

            return account != null ? _mapper.Map<UserAccountDto>(account) : null;
        }
        
        public async Task<UserAccount> GetUserAccountAsync(
            int? id = null, string username = null, string password = null, string thirdPartyUserId = null, bool isActive = true,
            UserAccountIssuer? issuer = null, bool includeRoles = false, CancellationToken cancellationToken = default)
        {
            var query = _repository.QueryNoTracking<UserAccount>().Where(x => x.IsActive == isActive);

            if (!id.HasValue && string.IsNullOrEmpty(username) && string.IsNullOrEmpty(thirdPartyUserId))
                throw new UserAccountAtLeastOneParamPassingException();

            if (id.HasValue)
                query = query.Where(x => x.Id == id);
        
            if (!string.IsNullOrEmpty(username))
                query = query.Where(x => x.UserName == username);

            if (!string.IsNullOrEmpty(password))
                query = query.Where(x => x.Password == password);
        
            if (!string.IsNullOrEmpty(thirdPartyUserId))
                query = query.Where(x => x.ThirdPartyUserId == thirdPartyUserId);

            if (issuer.HasValue)
                query = query.Where(x => x.Issuer == issuer.Value);
        
            var account = await query
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (account == null) return null;
        
            await EnrichUserAccountsAsync(new List<UserAccount> { account }, includeRoles, cancellationToken).ConfigureAwait(false);

            return account;
        }
        
        private async Task EnrichUserAccountsAsync(List<UserAccount> accounts, bool includeRoles, CancellationToken cancellationToken)
        {
            if (accounts == null || !accounts.Any()) return;
            {
                await EnrichUserAccountProfilesAsync(accounts, cancellationToken).ConfigureAwait(false);
            
                if (includeRoles) 
                    await EnrichUserAccountsRoleAsync(accounts, cancellationToken).ConfigureAwait(false);
            }
        }
        
        private async Task EnrichUserAccountProfilesAsync(List<UserAccount> accounts, CancellationToken cancellationToken)
        {
            var accountIds = accounts.Select(x => x.Id).ToList();

            var profiles = await _repository.Query<UserAccountProfile>()
                .Where(x => accountIds.Contains(x.UserAccountId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            accounts.ForEach(account => account.UserAccountProfile = profiles.FirstOrDefault(x => x.UserAccountId == account.Id));
        }
        
        private async Task EnrichUserAccountsRoleAsync(List<UserAccount> accounts, CancellationToken cancellationToken)
        {
            var accountIds = accounts.Select(x => x.Id).ToList();
            
            var roleUsers = await _repository.QueryNoTracking<RoleUser>()
                .Where(x => accountIds.Contains(x.UserId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var userRoleIds = roleUsers.Select(x => x.RoleId).Distinct().ToList();

            var userRoles = await _repository.QueryNoTracking<Role>()
                .Where(x => userRoleIds.Contains(x.Id))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            
            var rolePermissions = await _repository.QueryNoTracking<RolePermission>()
                .Where(x => userRoleIds.Contains(x.RoleId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            
            var userPermissions = await _repository.QueryNoTracking<UserPermission>()
                .Where(x => accountIds.Contains(x.UserId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var allPermissionIds = rolePermissions
                .Select(x => x.PermissionId)
                .Concat(userPermissions.Select(x => x.PermissionId)).Distinct().ToList();
            
            var allPermissions = await _repository.QueryNoTracking<Permission>()
                .Where(x => allPermissionIds.Contains(x.Id))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            
            accounts.ForEach(account =>
            {
                var thisAccountPermissionIds = new List<int>();
                var thisAccountRoleIds = roleUsers.Where(x => x.UserId == account.Id).Select(x => x.RoleId);
                
                thisAccountPermissionIds.AddRange(rolePermissions
                    .Where(x => thisAccountRoleIds.Contains(x.RoleId)).Select(x => x.PermissionId));

                thisAccountPermissionIds.AddRange( userPermissions
                    .Where(x => x.UserId == account.Id).Select(x => x.PermissionId));
                
                account.Roles = userRoles.Where(x => thisAccountRoleIds.Contains(x.Id)).ToList();
                account.Permissions = allPermissions.Where(x => thisAccountPermissionIds.Contains(x.Id)).ToList();
            });
        }
        
        public List<Claim> GenerateClaimsFromUserAccount(UserAccountDto account)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, account.UserName),
                new(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new(ClaimTypes.Authentication, AuthenticationSchemeConstants.SelfAuthenticationScheme)
            };
            claims.AddRange(account.Roles.Select(r => new Claim(ClaimTypes.Role, r.Name)));
            return claims;
        }
        
        public async Task<UserAccount> CreateUserAccountAsync(
            string requestUserName, string requestPassword, string thirdPartyUserId = null, 
            UserAccountIssuer authType = UserAccountIssuer.Self, UserAccountProfile profile = null, CancellationToken cancellationToken = default)
        {
            var userAccount = new UserAccount
            {
                Uuid = Guid.NewGuid(),
                Issuer = authType,
                UserName = requestUserName,
                Password = requestPassword?.ToSha256(),
                ThirdPartyUserId = thirdPartyUserId,
                IsActive = true
            };
        
            await _repository.InsertAsync(userAccount, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
            profile ??= new UserAccountProfile();

            profile.UserAccountId = userAccount.Id;
        
            await _repository.InsertAsync(profile, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            userAccount.UserAccountProfile = profile;
        
            return userAccount;
        }
    }
}