using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using ECFD.Application.Interfaces;
using ECFD.Application.Risk;
using ECFD.Application.Progression;
using ECFD.Infrastructure.Persistence;
using ECFD.Infrastructure.MLClients;
using ECFD.Infrastructure.SignalR;
using ECFD.Api.Hubs;
using ECFD.Api.HostedServices;

var builder = WebApplication.CreateBuilder(args);

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

// Register ML Clients (Default to Mock clients for zero-dependency local startup)
builder.Services.AddSingleton<IAsrClient, MockAsrClient>();
builder.Services.AddSingleton<INlpClient, MockNlpClient>();
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
