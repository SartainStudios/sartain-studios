namespace SartainStudios.Client.Service.Caching;

public static class CacheKeys
{
    public const string ClientPrefix = "clients:";
    public const string ProjectPrefix = "projects:";
    public const string BillingContractPrefix = "billing-contracts:";

    public const string ClientList = ClientPrefix + "list";
    public const string ProjectList = ProjectPrefix + "list";
    public const string BillingContractListPrefix = BillingContractPrefix + "list";

    public static string Client(string id)
    {
        return $"{ClientPrefix}item:{id}";
    }

    public static string Project(string id)
    {
        return $"{ProjectPrefix}item:{id}";
    }

    public static string BillingContract(string id)
    {
        return $"{BillingContractPrefix}item:{id}";
    }

    public static string BillingContractList(string? projectId)
    {
        return string.IsNullOrWhiteSpace(projectId)
            ? BillingContractListPrefix
            : $"{BillingContractListPrefix}:project:{projectId}";
    }
}