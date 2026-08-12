namespace SartainStudios.Schema.Api;

public static class TenantErrors
{
    public static readonly Error NotResolved = Error.Unauthorized(
        "Tenant.NotResolved",
        "The current user or organization could not be resolved from the request.");
}