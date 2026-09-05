using SmartPantry.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace SmartPantry.Permissions;

public class SmartPantryPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SmartPantryPermissions.GroupName);

        var booksPermission = myGroup.AddPermission(SmartPantryPermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(SmartPantryPermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(SmartPantryPermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(SmartPantryPermissions.Books.Delete, L("Permission:Books.Delete"));

        var authorsPermission = myGroup.AddPermission(SmartPantryPermissions.Authors.Default, L("Permission:Authors"));
        authorsPermission.AddChild(SmartPantryPermissions.Authors.Create, L("Permission:Authors.Create"));
        authorsPermission.AddChild(SmartPantryPermissions.Authors.Edit, L("Permission:Authors.Edit"));
        authorsPermission.AddChild(SmartPantryPermissions.Authors.Delete, L("Permission:Authors.Delete"));
        //Define your own permissions here. Example:
        //myGroup.AddPermission(SmartPantryPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SmartPantryResource>(name);
    }
}
