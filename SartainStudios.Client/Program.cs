using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using SartainStudios.Client;
using SartainStudios.Client.Page;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Client.Service.Caching;
using SartainStudios.Client.ServiceCollection;
using SartainStudios.Schema.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DataCache>();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<TokenRefresher>();
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy(Metadata.Policy.Organization, policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.Claims.Any(claim =>
                claim.Type == nameof(JwtClaimName.OrganizationId)
                && !string.IsNullOrWhiteSpace(claim.Value)
                && claim.Value != "000000000000000000000000")));
});
builder.Services.AddApiClients(builder.Configuration, builder.HostEnvironment.BaseAddress);
builder.Services.AddServices();
await builder.Build().RunAsync();