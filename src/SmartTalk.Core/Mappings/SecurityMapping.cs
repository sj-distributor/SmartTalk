using AutoMapper;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.Commands.Authority;
using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Dto.Security.Data;
using SmartTalk.Messages.DTO.Security.Data;

namespace SmartTalk.Core.Mappings;

public class SecurityMapping : Profile
{
    public SecurityMapping()
    {
        CreateMap<CreateOrUpdateRoleDto, Role>()
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Name, opt => opt.Ignore());
        CreateMap<CreateOrUpdateRoleUserDto, RoleUser>();
        CreateMap<CreateOrUpdateRolePermissionDto, RolePermission>();
        CreateMap<CreateOrUpdateUserPermissionDto, UserPermission>();
        CreateMap<CreateOrUpdatePermissionDto, Permission>();
        CreateMap<Role, RoleDto>().ReverseMap();
        CreateMap<RoleUser, RoleUserDto>()
            .ForMember(x => x.CreatedDate, opt => opt.MapFrom(x => x.CreatedOn))
            .ForMember(x => x.ModifiedDate, opt => opt.MapFrom(x => x.ModifiedOn));
        CreateMap<RoleUserDto, RoleUser>()
            .ForMember(x => x.CreatedOn, opt => opt.MapFrom(x => x.CreatedDate))
            .ForMember(x => x.ModifiedOn, opt => opt.MapFrom(x => x.ModifiedDate));
        CreateMap<UserPermission, UserPermissionDto>().ReverseMap();
        CreateMap<RolePermission, RolePermissionDto>().ReverseMap();
        CreateMap<Permission, PermissionDto>().ReverseMap();
        CreateMap<RoleUser, UpdateResponse>().ReverseMap();
    }
}