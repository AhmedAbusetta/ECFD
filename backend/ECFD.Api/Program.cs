using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;
using ECFD.Application.Progression;
using ECFD.Infrastructure.Persistence;
using ECFD.Infrastructure.MLClients;
using ECFD.Infrastructure.SignalR;
using ECFD.Api.HostedServices;

var builder = WebApplication.CreateBuilder(args);
// Machine-local, gitignored overrides (e.g. the private Modal ASR URL); command-line args still win.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddCommandLine(args);

// Add Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

// Database (In-Memory default for instant local dev, PostgreSQL configurable via env)
builder.Services.AddDbContext<EcfdDbContext>(options =>
{
    options.UseInMemoryDatabase("EcfdDevDb");
});

// Register Domain & Application Engines
builder.Services.AddSingleton<IRiskEngine, RiskEngine>();
builder.Services.AddSingleton<IAttackProgressionEngine, AttackProgressionEngine>();
builder.Services.AddSingleton<ISignalRNotifier, SignalRNotifier>();

// Register ML Clients: in-process mocks by default, real FastAPI services when MlServices:UseMocks=false
var mlOptions = builder.Configuration.GetSection(MlServicesOptions.SectionName).Get<MlServicesOptions>() ?? new MlServicesOptions();
if (mlOptions.UseMocks)
{
    builder.Services.AddSingleton<IAsrClient, MockAsrClient>();
    builder.Services.AddSingleton<INlpClient, MockNlpClient>();
}
else
{
    // Primary ASR (e.g. Cohere on Modal) with an optional fallback (local faster-whisper) when it fails or is slow.
    builder.Services.AddHttpClient("asr-primary", c =>
    {
        c.BaseAddress = new Uri(mlOptions.AsrUrl);
        c.Timeout = TimeSpan.FromSeconds(mlOptions.AsrTimeoutSeconds);
    });
    if (!string.IsNullOrWhiteSpace(mlOptions.AsrFallbackUrl))
    {
        builder.Services.AddHttpClient("asr-fallback", c =>
        {
            c.BaseAddress = new Uri(mlOptions.AsrFallbackUrl);
            c.Timeout = TimeSpan.FromSeconds(mlOptions.AsrTimeoutSeconds);
        });
    }
    builder.Services.AddTransient<IAsrClient>(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        IAsrClient primary = new HttpAsrClient(factory.CreateClient("asr-primary"));
        if (string.IsNullOrWhiteSpace(mlOptions.AsrFallbackUrl))
            return primary;
        return new FallbackAsrClient(primary, new HttpAsrClient(factory.CreateClient("asr-fallback")),
            TimeSpan.FromSeconds(mlOptions.AsrFallbackAfterSeconds), sp.GetRequiredService<ILogger<FallbackAsrClient>>());
    });
    if (mlOptions.AsrWarmUpOnStart)
        builder.Services.AddHostedService(sp => new AsrWarmUpHostedService(mlOptions.AsrUrl, sp.GetRequiredService<ILogger<AsrWarmUpHostedService>>()));
    builder.Services.AddHttpClient<INlpClient, HttpNlpClient>(c =>
    {
        c.BaseAddress = new Uri(mlOptions.NlpUrl);
        c.Timeout = TimeSpan.FromSeconds(mlOptions.NlpTimeoutSeconds);
    });
}
// AI call analyst (ADR-0005): optional, runs in the background next to the rules
if (!mlOptions.UseMocks && !string.IsNullOrWhiteSpace(mlOptions.AnalystUrl))
{
    builder.Services.AddHttpClient<IAnalystClient, HttpAnalystClient>(c =>
    {
        c.BaseAddress = new Uri(mlOptions.AnalystUrl);
        c.Timeout = TimeSpan.FromSeconds(mlOptions.AnalystTimeoutSeconds);
    });
}
else
{
    builder.Services.AddSingleton<IAnalystClient, DisabledAnalystClient>();
}
builder.Services.AddSingleton<IAntiSpoofClient, MockAntiSpoofClient>();

// Register Telephony & Media Gateway Background Services
builder.Services.AddHostedService<AsteriskHostedService>();
builder.Services.AddHostedService<MediaGatewayHostedService>();

// CORS for Frontend SignalR Connection
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard");

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
