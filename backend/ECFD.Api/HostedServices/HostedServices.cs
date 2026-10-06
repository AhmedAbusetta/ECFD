using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECFD.Api.HostedServices;

public class AsteriskHostedService : BackgroundService
{
    private readonly ILogger<AsteriskHostedService> _logger;

    public AsteriskHostedService(ILogger<AsteriskHostedService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("AsteriskHostedService: stub - ARI WebSocket client not implemented yet; no calls will be tracked.");
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Heartbeat / listener loop
                await Task.Delay(5000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}

public class MediaGatewayHostedService : BackgroundService
{
    private readonly ILogger<MediaGatewayHostedService> _logger;

    public MediaGatewayHostedService(ILogger<MediaGatewayHostedService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("MediaGatewayHostedService: stub - RTP listener not implemented yet; no media is received.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Media ingestion loop
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}

/// <summary>
/// Calls the ASR /health once at startup so a scaled-to-zero cloud GPU (Modal) loads the model
/// before the first real utterance arrives. Fire-and-forget: failures are only logged.
/// </summary>
public class AsrWarmUpHostedService : BackgroundService
{
    private readonly string _asrUrl;
    private readonly ILogger<AsrWarmUpHostedService> _logger;

    public AsrWarmUpHostedService(string asrUrl, ILogger<AsrWarmUpHostedService> logger)
    {
        _asrUrl = asrUrl;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var started = DateTime.UtcNow;
        try
        {
            using var response = await http.GetAsync(_asrUrl.TrimEnd('/') + "/health", stoppingToken);
            _logger.LogInformation("ASR warm-up: {Url} answered {Status} after {Seconds:F0}s.",
                _asrUrl, (int)response.StatusCode, (DateTime.UtcNow - started).TotalSeconds);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("ASR warm-up: {Url} not reachable ({Error}); the fallback ASR will be used if configured.", _asrUrl, ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
