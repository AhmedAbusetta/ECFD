using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ECFD.Application.Alerts;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;
using ECFD.Application.Progression;
using ECFD.Infrastructure.Persistence;
using ECFD.Infrastructure.MLClients;
using ECFD.Infrastructure.SignalR;
using ECFD.Api.HostedServices;
using ECFD.Api.Pipeline;
using ECFD.Infrastructure.Asterisk;

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
        builder.Services.AddHostedService(sp => new AsrWarmUpHostedService(mlOptions.AsrUrl, sp.GetRequiredService<ILogger<AsrWarmUpHostedService>>(),
        TimeSpan.FromMinutes(mlOptions.AsrKeepWarmMinutes)));
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
// Voice anti-spoofing (ml/antispoof): scores the caller's sentences in the background; mock = no voice evidence
if (!mlOptions.UseMocks && !string.IsNullOrWhiteSpace(mlOptions.AntiSpoofUrl))
{
    builder.Services.AddHttpClient<IAntiSpoofClient, HttpAntiSpoofClient>(c =>
    {
        c.BaseAddress = new Uri(mlOptions.AntiSpoofUrl);
        c.Timeout = TimeSpan.FromSeconds(mlOptions.AntiSpoofTimeoutSeconds);
    });
    // a second warm-up loop (AddHostedService would drop it as a duplicate of the ASR one)
    builder.Services.AddSingleton<IHostedService>(sp => new AsrWarmUpHostedService(mlOptions.AntiSpoofUrl,
        sp.GetRequiredService<ILogger<AsrWarmUpHostedService>>(), TimeSpan.FromMinutes(mlOptions.AsrKeepWarmMinutes), "Anti-spoofing"));
}
else
{
    builder.Services.AddSingleton<IAntiSpoofClient, MockAntiSpoofClient>();
}

// The call pipeline shared by real PBX calls and the dashboard's simulated calls
builder.Services.AddSingleton<CallPipeline>();

// Telephony (ADR-0001): watch the PBX over ARI and tap answered calls; off unless Asterisk:Enabled=true
var asteriskOptions = builder.Configuration.GetSection(AsteriskOptions.SectionName).Get<AsteriskOptions>() ?? new AsteriskOptions();
if (asteriskOptions.Enabled && string.IsNullOrWhiteSpace(asteriskOptions.MediaHost))
{
    Console.Error.WriteLine("Asterisk:Enabled is true but Asterisk:MediaHost is empty - set it to this machine's IP " +
                            "as the PBX sees it (e.g. the laptop's address on the hotspot). Telephony stays off.");
    asteriskOptions.Enabled = false;
}
if (asteriskOptions.Enabled)
{
    builder.Services.AddSingleton(asteriskOptions);
    builder.Services.AddHttpClient<IAriClient, AriClient>(c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddSingleton<IMediaReceiverFactory, RtpReceiverFactory>();
    builder.Services.AddSingleton<ICallSink, PipelineCallSink>();
    builder.Services.AddSingleton(sp => new CallTapManager(
        sp.GetRequiredService<IAriClient>(), sp.GetRequiredService<IMediaReceiverFactory>(),
        sp.GetRequiredService<ICallSink>(), asteriskOptions, sp.GetRequiredService<ILogger<CallTapManager>>(),
        asteriskOptions.Segmenter));
    builder.Services.AddSingleton<IEmployeeWarner>(sp => sp.GetRequiredService<CallTapManager>());
    builder.Services.AddHostedService<AsteriskHostedService>();
}
else
{
    builder.Services.AddSingleton<IEmployeeWarner, NoEmployeeWarner>();
}

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
