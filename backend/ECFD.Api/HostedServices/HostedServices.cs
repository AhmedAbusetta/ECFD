using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ECFD.Infrastructure.Asterisk;

namespace ECFD.Api.HostedServices;

/// <summary>
/// Keeps the ARI event WebSocket to the PBX open (reconnecting if Asterisk restarts or the network drops)
/// and hands every event to the <see cref="CallTapManager"/>, which taps answered calls.
/// </summary>
public class AsteriskHostedService : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IAriClient _ari;
    private readonly CallTapManager _taps;
    private readonly AsteriskOptions _options;
    private readonly ILogger<AsteriskHostedService> _logger;

    public AsteriskHostedService(IAriClient ari, CallTapManager taps, AsteriskOptions options, ILogger<AsteriskHostedService> logger)
    {
        _ari = ari;
        _taps = taps;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool warned = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Connecting to Asterisk ARI at {Url} (app {App}) ...", _options.AriUrl, _options.AppName);
                await _ari.RunEventsAsync(
                    ev => _taps.HandleEventAsync(ev, stoppingToken),
                    () =>
                    {
                        warned = false;
                        _logger.LogInformation("Connected to Asterisk ARI. Calls between phones will be tapped automatically.");
                    },
                    stoppingToken);
                _logger.LogWarning("Asterisk ARI connection closed; reconnecting in {Seconds}s.", RetryDelay.TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!warned)
                {
                    _logger.LogWarning("Cannot reach Asterisk ARI at {Url}: {Error}. Retrying every {Seconds}s.",
                        _options.AriUrl, ex.Message, RetryDelay.TotalSeconds);
                    warned = true; // log once per outage, not every 5 seconds
                }
            }
            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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

    private readonly TimeSpan _keepWarmEvery;

    /// <param name="keepWarmEvery">Ping again at this interval for as long as the backend runs, so a cloud
    /// GPU never scales to zero (and cold-starts) mid-session; zero = warm up once at start only.</param>
    public AsrWarmUpHostedService(string asrUrl, ILogger<AsrWarmUpHostedService> logger, TimeSpan keepWarmEvery = default)
    {
        _asrUrl = asrUrl;
        _logger = logger;
        _keepWarmEvery = keepWarmEvery;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var first = true;
        try
        {
            do
            {
                var started = DateTime.UtcNow;
                try
                {
                    using var response = await http.GetAsync(_asrUrl.TrimEnd('/') + "/health", stoppingToken);
                    if (first || !response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("ASR warm-up: {Url} answered {Status} after {Seconds:F0}s.",
                            _asrUrl, (int)response.StatusCode, (DateTime.UtcNow - started).TotalSeconds);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("ASR warm-up: {Url} not reachable ({Error}).", _asrUrl, ex.Message);
                }
                first = false;
                if (_keepWarmEvery > TimeSpan.Zero)
                {
                    await Task.Delay(_keepWarmEvery, stoppingToken);
                }
            }
            while (_keepWarmEvery > TimeSpan.Zero && !stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
