using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ECFD.Application.Alerts;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;
using ECFD.Application.Progression;
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

// Register Domain & Application Engines
builder.Services.AddSingleton<IRiskEngine, RiskEngine>();
builder.Services.AddSingleton<IAttackProgressionEngine, AttackProgressionEngine>();
builder.Services.AddSingleton<ISignalRNotifier, SignalRNotifier>();

// Register ML Clients: in-process mocks by default, real FastAPI services when MlServices:UseMocks=false
var mlOptions = builder.Configuration.GetSection(MlServicesOptions.SectionName).Get<MlServicesOptions>() ?? new MlServicesOptions();
// "localhost" is tried over IPv6 first; the Python services listen on IPv4 only, so on Windows every new
// connection lost about two seconds before falling back - on each rule check, analyst call and warning.
mlOptions.AsrUrl = Loopback(mlOptions.AsrUrl)!;
mlOptions.NlpUrl = Loopback(mlOptions.NlpUrl)!;
mlOptions.AnalystUrl = Loopback(mlOptions.AnalystUrl);
mlOptions.AntiSpoofUrl = Loopback(mlOptions.AntiSpoofUrl);
if (mlOptions.UseMocks)
{
    builder.Services.AddSingleton<IAsrClient, MockAsrClient>();
    builder.Services.AddSingleton<INlpClient, MockNlpClient>();
}
else
{
    builder.Services.AddHttpClient<IAsrClient, HttpAsrClient>(c =>
    {
        c.BaseAddress = new Uri(mlOptions.AsrUrl);
        c.Timeout = TimeSpan.FromSeconds(mlOptions.AsrTimeoutSeconds);
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
builder.Services.AddSingleton<CallLogStore>(); // history of finished calls (verdicts, not audio)
builder.Services.AddSingleton<CallPipeline>();

// Telephony (ADR-0001): watch the PBX over ARI and tap answered calls; off unless Asterisk:Enabled=true
var asteriskOptions = builder.Configuration.GetSection(AsteriskOptions.SectionName).Get<AsteriskOptions>() ?? new AsteriskOptions();
asteriskOptions.AriUrl = Loopback(asteriskOptions.AriUrl)!;
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

// CORS for Frontend SignalR Connection. Cors:AllowedOrigins lists the dashboards' addresses when the dashboard
// runs on another machine (e.g. ["http://192.168.1.88:3000"]).
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
    ? configured
    : new[] { "http://localhost:3000", "http://127.0.0.1:3000" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
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

// http://localhost:8002 -> http://127.0.0.1:8002 (other hosts are left alone)
static string? Loopback(string? url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        ? new UriBuilder(uri) { Host = "127.0.0.1" }.Uri.ToString().TrimEnd('/')
        : url;

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
