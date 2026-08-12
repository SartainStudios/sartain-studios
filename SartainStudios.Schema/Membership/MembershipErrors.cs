using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Membership;

public static class MembershipErrors
{
    public const string EmailField = "email";
    public const string RoleField = "role";
    public const string EmailRequired = "Email is required.";

    public static readonly Error InvalidId = Error.Validation(
        "Membership.InvalidId",
        "The supplied membership id is not a valid identifier.");

    public static readonly Error AlreadyInvited = Error.Conflict(
        "Membership.AlreadyInvited",
        "A membership already exists for this email in the organization.");

    public static readonly Error AlreadyMember = Error.Conflict(
        "Membership.AlreadyMember",
        "This user is already a member of the organization.");

    public static readonly Error OnlyOwnerCanGrantOwnership = Error.Forbidden(
        "Membership.OnlyOwnerCanGrantOwnership",
        "Only an owner can grant ownership.");

    public static readonly Error OnlyOwnerCanRemoveOwner = Error.Forbidden(
        "Membership.OnlyOwnerCanRemoveOwner",
        "Only an owner can remove another owner.");

    public static readonly Error CannotDemoteLastOwner = Error.Conflict(
        "Membership.CannotDemoteLastOwner",
        "Cannot demote the last owner of the organization.");

    public static readonly Error CannotRemoveLastOwner = Error.Conflict(
        "Membership.CannotRemoveLastOwner",
        "Cannot remove the last owner of the organization.");

    public static readonly Error InviteNotFound = Error.NotFound(
        "Membership.InviteNotFound",
        "No pending invitation was found for this id.");

    public static readonly Error InviteBelongsToAnotherAccount = Error.Forbidden(
        "Membership.InviteBelongsToAnotherAccount",
        "This invitation belongs to a different account.");

    public static Error NotFound(string id)
    {
        return Error.NotFound(
            "Membership.NotFound",
            $"Membership with ID {id} was not found.");
    }

    public static Error InvalidRole(string options)
    {
        return Error.Validation(
            "Membership.InvalidRole",
            $"Role must be one of: {options}.");
    }
}