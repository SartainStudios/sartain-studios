using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Billing;
using SartainStudios.Api.Service.Client;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Health;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Membership;
using SartainStudios.Api.Service.Onboarding;
using SartainStudios.Api.Service.Organization;
using SartainStudios.Api.Service.Project;
using SartainStudios.Api.Service.Timekeeping;

namespace SartainStudios.Api.ServiceCollection;

public static class ApplicationServices
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<Token>();
        services.AddSingleton<Password>();
        services.AddSingleton<Draft>();
        services.AddSingleton<Deletion>();
        services.AddSingleton<Session>();
        services.AddSingleton<Lookup>();
        services.AddSingleton<Provisioning>();
        services.AddSingleton<Roster>();
        services.AddSingleton<Assignment>();
        services.AddSingleton<Sequence>();
        services.AddSingleton<Tracker>();
        services.AddSingleton<Timeline>();
        services.AddSingleton<Editing>();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ClientService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<BillingContractService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<AccountService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<Access>();
        services.AddScoped<WorkSessionService>();
        services.AddScoped<OnboardingService>();
        services.AddScoped<HealthService>();
    }
}