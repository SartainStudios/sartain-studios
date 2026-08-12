namespace SartainStudios.Schema.Project;

public sealed class Snapshot
{
    public string ProjectName { get; set; } = string.Empty;

    public string ProjectDescription { get; set; } = string.Empty;

    public string ServiceProvided { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; }

    public string BillingCycle { get; set; } = string.Empty;

    public string ContractId { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;
}