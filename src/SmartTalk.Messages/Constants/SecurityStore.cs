using System.Reflection;

namespace SmartTalk.Messages.Constants;

public static class SecurityStore
{
    public static class Roles
    {
        public const string User = nameof(User);
        public const string Administrator = nameof(Administrator);
        public const string SuperAdministrator = nameof(SuperAdministrator);
    }
    
    public static class Permissions
    {
        public const string CanViewPhoneOrder = nameof(CanViewPhoneOrder);

        public const string CanViewAccountManagement = nameof(CanViewAccountManagement);

        public const string CanCreateAccount = nameof(CanCreateAccount);

        public const string CanDeleteAccount = nameof(CanDeleteAccount);

        public const string CanCopyAccount = nameof(CanCopyAccount);

        public const string CanUpdateAccount = nameof(CanUpdateAccount);
        
        public const string CanViewMerchPrinter = nameof(CanViewMerchPrinter);
        
        private static List<string> _allPermissions;

        public static List<string> AllPermissions
        {
            get
            {
                if (_allPermissions != null) return _allPermissions;

                _allPermissions = new List<string>();

                var fields =
                    typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                foreach (var field in fields)
                {
                    if (!field.IsLiteral || field.IsInitOnly || field.FieldType != typeof(string)) continue;

                    _allPermissions.Add((string)field.GetValue(null));
                }

                return _allPermissions;
            }
        }
        
        public static string PermissionChinese (string permission) =>
            permission switch
            {
                nameof(CanViewPhoneOrder) => "进去通话记录页面",
                nameof(CanViewAccountManagement) => "进去账户管理",
                nameof(CanCreateAccount) => "新增账户",
                nameof(CanDeleteAccount) => "删除账户",
                nameof(CanCopyAccount) => "复制账户",
                nameof(CanUpdateAccount) => "更新账户",
                nameof(CanViewMerchPrinter) => "进入打印设产",
                _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null)
            };
    }
}