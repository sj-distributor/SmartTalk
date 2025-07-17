using System.Reflection;
using System.Security.Claims;
using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Account.Exceptions;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Core.Services.Account
{
    public interface IAccountDataProvider : IScopedDependency
    {
        Task<(int, List<UserAccount>)> GetUserAccountAsync(
            int? id = null, string username = null, string password = null, string thirdPartyUserId = null, bool isActive = true, UserAccountIssuer? issuer = null, bool includeRoles = false, string userNameContain = null, int? pageSize = null, int? pageIndex = null, bool orderbyCreatedOn = false, CancellationToken cancellationToken = default);
        
        Task<UserAccountDto> GetUserAccountByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
        
        Task<UserAccountProfile> GetUserAccountProfileAsync(int userAccountId, CancellationToken cancellationToken);

        Task DeleteUserAccountProfileAsync(UserAccountProfile userAccountProfile, bool forceSave = true, CancellationToken cancellationToken = default);

        Task<(int, List<UserAccountDto>)> GetUserAccountDtoAsync(
            int? id = null, string username = null, string password = null, string thirdPartyUserId = null, bool isActive = true, UserAccountIssuer? issuer = null, bool includeRoles = false, string userNameContain = null, int? pageSize = null, int? pageIndex = null, bool orderbyCreatedOn = false, CancellationToken cancellationToken = default);

        List<Claim> GenerateClaimsFromUserAccount(UserAccountDto account);

        Task<UserAccount> CreateUserAccountAsync(
            string requestUserName, string requestPassword, UserAccountLevel accountLevel, string thirdPartyUserId = null,
            UserAccountIssuer authType = UserAccountIssuer.Self, UserAccountProfile profile = null, string creator = null, bool isProfile = true, CancellationToken cancellationToken = default);

        Task UpdateUserAccountAsync(UserAccount userAccount, bool forceSave = true, CancellationToken cancellationToken = default);
        
        Task DeleteUserAccountAsync(UserAccount userAccount, bool forceSave = true, CancellationToken cancellationToken = default);
        
        Task<(int, List<UserAccountDto>)> GetUserAccountDtoAsync(
            string userNameContain = null, UserAccountLevel? userAccountLevel = null, int? pageSize = null, int? pageIndex = null, bool orderByCreatedOn = false, CancellationToken cancellationToken = default);

        Task<UserAccount> IsUserAccountExistAsync(int id, CancellationToken cancellationToken);

        Task<UserAccount> GetUserAccountRolePermissionsByUserIdAsync(int? userId, CancellationToken cancellationToken);

        Task<UserAccount> GetUserAccountByUserIdAsync(int userId, CancellationToken cancellationToken);
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
        
        public async Task<UserAccountDto> GetUserAccountByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            Log.Information($"SmartTalk apiKey: {apiKey}", apiKey);
            
            var accountApiKey = await _repository.QueryNoTracking<UserAccountApiKey>()
                .Where(x => x.ApiKey == apiKey)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (accountApiKey == null)
                return null;

            var (count, accounts) = await GetUserAccountAsync(id: accountApiKey.UserAccountId, cancellationToken: cancellationToken).ConfigureAwait(false);
 
            var account = accounts.FirstOrDefault();
            
            return account != null ? _mapper.Map<UserAccountDto>(account) : null;
        }

        public async Task<UserAccountProfile> GetUserAccountProfileAsync(int userAccountId, CancellationToken cancellationToken)
        {
            return await _repository.FirstOrDefaultAsync<UserAccountProfile>(x => x.UserAccountId == userAccountId, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteUserAccountProfileAsync(
            UserAccountProfile userAccountProfile, bool forceSave = true, CancellationToken cancellationToken = default)
        {
            await _repository.DeleteAsync(userAccountProfile, cancellationToken).ConfigureAwait(false);

            if (forceSave)
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<(int, List<UserAccountDto>)> GetUserAccountDtoAsync(
            int? id = null, string username = null, string password = null, string thirdPartyUserId = null, bool isActive = true, UserAccountIssuer? issuer = null,
            bool includeRoles = false, string userNameContain = null, int? pageSize = null, int? pageIndex = null, bool orderbyCreatedOn = false, CancellationToken cancellationToken = default)
        {
            var (count, account) = await GetUserAccountAsync(id, username, password, thirdPartyUserId, isActive, issuer, includeRoles, userNameContain, pageSize, pageIndex, true, cancellationToken).ConfigureAwait(false);

            return account != null ? (count, _mapper.Map<List<UserAccountDto>>(account)) : (count, null);
        }
        
        private async Task<List<UserAccountDto>> GetUserAccountAsync(
            List<UserAccount> userAccounts, CancellationToken cancellationToken)
        {
            if (userAccounts == null) return null;
            
            var lastModifiedBy = userAccounts.Select(x => x.LastModifiedBy).ToList();
            
            var accounts = await _repository.Query<UserAccount>().Where(x => lastModifiedBy.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

            var targetUserAccount = _mapper.Map<List<UserAccountDto>>(userAccounts);

            targetUserAccount.ForEach(x =>
            {
                x.LastModifiedByName = accounts.FirstOrDefault(s => s.Id == x.LastModifiedBy)?.UserName;
            });
            
            return targetUserAccount;
        }
        
        public async Task<(int, List<UserAccount>)> GetUserAccountAsync(
            int? id = null, string username = null, string password = null, string thirdPartyUserId = null, bool isActive = true,
            UserAccountIssuer? issuer = null, bool includeRoles = false, string userNameContain = null, int? pageSize = null, int? pageIndex = null, bool orderbyCreatedOn = false,  CancellationToken cancellationToken = default)
        {
            var query = _repository.QueryNoTracking<UserAccount>().Where(x => x.IsActive == isActive);

            if (!id.HasValue && string.IsNullOrEmpty(username) && string.IsNullOrEmpty(thirdPartyUserId) && !pageSize.HasValue && !pageIndex.HasValue)
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

            if (!string.IsNullOrEmpty(userNameContain))
                query = query.Where(x => x.UserName.Contains(userNameContain));

            if (orderbyCreatedOn)
                query = query.OrderByDescending(x => x.CreatedOn);
            
            var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
             if (pageSize.HasValue && pageIndex.HasValue)
                query = query.Skip(pageSize.Value * (pageIndex.Value - 1)).Take(pageSize.Value);
            
            var account = await query
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (account is { Count: 0 }) return (count, null);
        
            await EnrichUserAccountsAsync(account, includeRoles, cancellationToken).ConfigureAwait(false);

            return (count, account);
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
            string requestUserName, string requestPassword, UserAccountLevel accountLevel, string thirdPartyUserId = null, 
            UserAccountIssuer authType = UserAccountIssuer.Self, UserAccountProfile profile = null, string creator = null, bool isProfile = true, CancellationToken cancellationToken = default)
        {
            var userAccount = new UserAccount
            {
                Uuid = Guid.NewGuid(),
                Issuer = authType,
                Creator = creator,
                UserName = requestUserName,
                Password = requestPassword?.ToSha256(),
                OriginalPassword = requestPassword ?? null,
                ThirdPartyUserId = thirdPartyUserId,
                IsActive = true,
                AccountLevel = accountLevel
            };
        
            await _repository.InsertAsync(userAccount, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (!isProfile) return userAccount;
            
            profile ??= new UserAccountProfile();

            profile.UserAccountId = userAccount.Id;
        
            await _repository.InsertAsync(profile, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            userAccount.UserAccountProfile = profile;
        
            return userAccount;
        }

        public async Task UpdateUserAccountAsync(
            UserAccount userAccount, bool forceSave = true, CancellationToken cancellationToken = default)
        {
            await _repository.UpdateAsync(userAccount, cancellationToken).ConfigureAwait(false);

            if (forceSave)
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteUserAccountAsync(UserAccount userAccount, bool forceSave = true, CancellationToken cancellationToken = default)
        {
            await _repository.DeleteAsync(userAccount, cancellationToken).ConfigureAwait(false);

            if (forceSave)
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        
        public async Task<(int, List<UserAccountDto>)> GetUserAccountDtoAsync(string userNameContain = null, UserAccountLevel? userAccountLevel = null,  int? pageSize = null, int? pageIndex = null,
            bool orderByCreatedOn = false, CancellationToken cancellationToken = default)
        {
            var query =  _repository.Query<UserAccount>().Where(x => x.Issuer == 0);

            if (!string.IsNullOrEmpty(userNameContain))
                query = query.Where(x => x.UserName.Contains(userNameContain));

            if (userAccountLevel != null)
                query = query.Where(x => x.AccountLevel == userAccountLevel);
            
            if (orderByCreatedOn)
                query = query.OrderByDescending(x => x.CreatedOn);

            var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            
            if (pageSize.HasValue && pageIndex.HasValue)
                query = query.Skip(pageSize.Value * (pageIndex.Value - 1)).Take(pageSize.Value);
            
            var account = await query
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (account is { Count: 0 }) return (count, null);
            
            var accountIds = query.Select(x => x.Id).ToList();

            var roleUsers = await (from roleUser in _repository.QueryNoTracking<RoleUser>().Where(x => accountIds.Contains(x.UserId))
                join role in _repository.Query<Role>() on roleUser.RoleId equals role.Id
                select new { roleUser.UserId, role}).ToListAsync(cancellationToken);
            
            var agentData = await (
                from storeUser in _repository.QueryNoTracking<PosStoreUser>().Where(x => accountIds.Contains(x.UserId))
                join posAgent in _repository.QueryNoTracking<PosAgent>() on storeUser.StoreId equals posAgent.StoreId
                join agent in _repository.Query<Agent>() on posAgent.AgentId equals agent.Id
                select new { storeUser.UserId, agent }
            ).ToListAsync(cancellationToken);

            account = account.Select(x =>
            {
                x.Roles = roleUsers.Where(s => s.UserId == x.Id).Select(x => x.role).ToList();

                x.Agents = agentData.Where(a => a.UserId == x.Id).Select(x => x.agent).ToList();
                
                return x;
            }).ToList();
            
            return (count, _mapper.Map<List<UserAccountDto>>(account));
        }
        
        public async Task<UserAccount> IsUserAccountExistAsync(int id, CancellationToken cancellationToken)
        {
            var userAccount = await _repository.FirstOrDefaultAsync<UserAccount>(x => x.Id == id, cancellationToken).ConfigureAwait(false);
            
            return userAccount;
        }

        public async Task<UserAccount> GetUserAccountRolePermissionsByUserIdAsync(int? userId, CancellationToken cancellationToken)
        {
            if (!userId.HasValue) throw new UserAccountAtLeastOneParamPassingException();
            
            var user = await _repository.FirstOrDefaultAsync<UserAccount>(x => x.Id == userId, cancellationToken).ConfigureAwait(false);

            var roles = await (from roleUser in _repository.Query<RoleUser>().Where(x => x.UserId == userId)
                join role in _repository.Query<Role>() on roleUser.RoleId equals role.Id
                join rolePermission in _repository.Query<RolePermission>() on role.Id equals rolePermission.RoleId
                join permission in _repository.Query<Permission>() on rolePermission.PermissionId equals permission.Id 
                select new {role, permission}).ToListAsync(cancellationToken);

            var userPermissions = await (from userPermission in _repository.Query<UserPermission>().Where( x => x.UserId == userId)
                join permission in _repository.Query<Permission>() on userPermission.PermissionId equals permission.Id
                select permission).ToListAsync(cancellationToken);
            
            if (user == null) throw new UserAccountAtLeastOneParamPassingException();
                        
            user.Roles = roles.Select(x => x.role).ToList();
            user.Permissions = roles.Select(x => x.permission).Concat(userPermissions).ToList();

            return user;
        }

        public async Task<UserAccount> GetUserAccountByUserIdAsync(int userId, CancellationToken cancellationToken)
        {
            return await _repository.Query<UserAccount>().Where(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}