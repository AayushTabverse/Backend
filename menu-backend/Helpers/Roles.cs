using menu_backend.Models;

namespace menu_backend.Helpers;

/// <summary>
/// Role groups for [Authorize(Roles = ...)]. Keep in sync with the frontend's shared/permissions.ts.
/// </summary>
public static class Roles
{
    /// <summary>Restaurant owner (and platform super admin).</summary>
    public const string Owner = "RestaurantAdmin,SuperAdmin";

    /// <summary>Owner + Manager: menu, tables, staff, inventory, analytics.</summary>
    public const string Management = Owner + ",Manager";

    /// <summary>Management + Cashier: settle bills, udhaar, bill history.</summary>
    public const string Billing = Management + ",Cashier";

    /// <summary>Everyone who uses the Take Order screen.</summary>
    public const string FloorStaff = Billing + ",Captain,Waiter";

    /// <summary>Kitchen display.</summary>
    public const string KitchenDisplay = Management + ",Kitchen";

    /// <summary>Any logged-in restaurant staff.</summary>
    public const string AllStaff = FloorStaff + ",Kitchen";

    public static bool IsOwner(UserRole role) =>
        role is UserRole.RestaurantAdmin or UserRole.SuperAdmin;

    /// <summary>
    /// Roles a user may create, deactivate or delete. Owners manage everyone below them;
    /// managers manage everyone below manager.
    /// </summary>
    public static bool CanManage(UserRole actor, UserRole target) => actor switch
    {
        UserRole.SuperAdmin or UserRole.RestaurantAdmin =>
            target is UserRole.Manager or UserRole.Cashier or UserRole.Captain or UserRole.Waiter or UserRole.Kitchen or UserRole.Helper,
        UserRole.Manager =>
            target is UserRole.Cashier or UserRole.Captain or UserRole.Waiter or UserRole.Kitchen or UserRole.Helper,
        _ => false
    };
}
