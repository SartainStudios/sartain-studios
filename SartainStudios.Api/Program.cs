using System.Text.Json;
using System.Text.Json.Serialization;
using QuestPDF;
using QuestPDF.Infrastructure;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Health;
using SartainStudios.Api.ServiceCollection;

Settings.License = LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddMongo(builder.Configuration);
builder.Services.AddGoogle(builder.Configuration);
builder.Services.AddJwt(builder.Configuration);
builder.Services.AddEmail(builder.Configuration);
builder.Services.AddClientSettings(builder.Configuration);
builder.Services.AddHourLimitMonitor(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddAuthorization();
builder.Services.AddCors(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var indexInitializer = scope.ServiceProvider.GetRequiredService<IIndexInitializer>();
    await indexInitializer.InitializeIndexesAsync();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(Cors.ClientPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();