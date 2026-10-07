using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ECFD.Application.Audio;
using Microsoft.Extensions.Logging;

namespace ECFD.Infrastructure.Asterisk;

/// <summary>Where a tapped call's sentences go (implemented by the API's call pipeline).</summary>
public interface ICallSink
{
    Task<Guid> StartCallAsync(string externalCallId, string callerEndpoint, string employeeEndpoint, CancellationToken ct);
    /// <summary>A finished sentence from one side of the call (16 kHz little-endian PCM).</summary>
    Task OnUtteranceAsync(Guid sessionId, string speaker, string utteranceId, byte[] pcm16le, CancellationToken ct);
    /// <summary>The sentence spoken so far, for live words on the dashboard. Best effort.</summary>
    Task OnPartialAsync(Guid sessionId, string speaker, string utteranceId, byte[] pcm16le, CancellationToken ct);
    /// <summary>A detection was too short to be a sentence: withdraw any live words shown for it.</summary>
    Task OnDiscardedAsync(Guid sessionId, string speaker, string utteranceId, CancellationToken ct);
    Task EndCallAsync(Guid sessionId, CancellationToken ct);
}

/// <summary>
/// Turns ARI events into tapped calls (ADR-0001). When a call between two phones is answered, each side
/// gets its own tap: a Snoop channel copying that person's voice + an External Media channel streaming
/// it to one of our UDP ports, joined in a bridge of their own. The callers' own call is never touched,
/// so if ECFD crashes the phone call simply continues (fail-open). Each tap's audio is cut into
/// sentences and passed to <see cref="ICallSink"/> with the speaker already known from the phone leg.
/// </summary>
public sealed class CallTapManager
{
    private static readonly Regex PjsipChannel = new(@"^PJSIP/(?<ext>.+)-[0-9a-fA-F]+$", RegexOptions.Compiled);

    private readonly IAriClient _ari;
    private readonly IMediaReceiverFactory _media;
    private readonly ICallSink _sink;
    private readonly AsteriskOptions _options;
    private readonly SegmenterOptions _segmenter;
    private readonly ILogger<CallTapManager> _logger;
    private readonly ConcurrentDictionary<string, TappedCall> _byChannel = new();
    private readonly ConcurrentQueue<int> _freePorts;
    private readonly SemaphoreSlim _setupLock = new(1, 1);

    public CallTapManager(IAriClient ari, IMediaReceiverFactory media, ICallSink sink, AsteriskOptions options,
        ILogger<CallTapManager> logger, SegmenterOptions? segmenter = null)
    {
        _ari = ari;
        _media = media;
        _sink = sink;
        _options = options;
        _logger = logger;
        _segmenter = segmenter ?? new SegmenterOptions();
        _freePorts = new ConcurrentQueue<int>(Enumerable.Range(options.MediaPortStart, options.MediaPortCount));
    }

    public int ActiveCalls => _byChannel.Values.Distinct().Count();

    public async Task HandleEventAsync(JsonElement ev, CancellationToken ct)
    {
        switch (Str(ev, "type"))
        {
            case "Dial" when Str(ev, "dialstatus") == "ANSWER":
                if (ev.TryGetProperty("caller", out var caller) && ev.TryGetProperty("peer", out var peer))
                {
                    await OnAnsweredAsync(caller, peer, ct);
                }
                break;
            case "ChannelDestroyed" when ev.TryGetProperty("channel", out var channel):
                var id = Str(channel, "id");
                if (id != null && _byChannel.TryGetValue(id, out var call))
                {
                    foreach (var leg in call.Legs)
                    {
                        _byChannel.TryRemove(leg.ChannelId, out _);
                    }
                    _ = Task.Run(() => TeardownAsync(call, CancellationToken.None));
                }
                break;
        }
    }

    private async Task OnAnsweredAsync(JsonElement callerChannel, JsonElement peerChannel, CancellationToken ct)
    {
        var a = Leg(callerChannel);
        var b = Leg(peerChannel);
        if (a == null || b == null)
        {
            return; // not two phones (e.g. one of our own channels)
        }

        await _setupLock.WaitAsync(ct);
        try
        {
            if (_byChannel.ContainsKey(a.Value.Id) || _byChannel.ContainsKey(b.Value.Id))
            {
                return; // already tapped
            }

            // The employee is whoever's extension is listed; otherwise the dialler is the caller.
            bool aIsEmployee = _options.Employees.Contains(a.Value.Ext);
            bool bIsEmployee = _options.Employees.Contains(b.Value.Ext);
            var (callerLeg, employeeLeg) = aIsEmployee && !bIsEmployee ? (b.Value, a.Value) : (a.Value, b.Value);

            var sessionId = await _sink.StartCallAsync($"PBX-{callerLeg.Id}", callerLeg.Ext, employeeLeg.Ext, ct);
            var call = new TappedCall(sessionId);
            _logger.LogInformation("Call answered: caller {Caller} -> employee {Employee}; tapping both sides.",
                callerLeg.Ext, employeeLeg.Ext);

            foreach (var (leg, speaker) in new[] { (callerLeg, "CALLER"), (employeeLeg, "EMPLOYEE") })
            {
                var tap = await TapLegAsync(sessionId, leg.Id, speaker, ct);
                if (tap != null)
                {
                    call.Legs.Add(tap);
                    _byChannel[leg.Id] = call;
                }
            }
            if (call.Legs.Count == 0)
            {
                await _sink.EndCallAsync(sessionId, ct);
            }
        }
        finally
        {
            _setupLock.Release();
        }
    }

    private async Task<LegTap?> TapLegAsync(Guid sessionId, string channelId, string speaker, CancellationToken ct)
    {
        if (!_freePorts.TryDequeue(out int port))
        {
            _logger.LogError("No free media port for {Speaker} on {Channel} (Asterisk:MediaPortCount too small?).", speaker, channelId);
            return null;
        }

        var tag = Guid.NewGuid().ToString("N")[..12];
        var tap = new LegTap(sessionId, channelId, speaker, port, $"ecfd-snoop-{tag}", $"ecfd-media-{tag}",
            $"ecfd-bridge-{tag}", _sink, _segmenter, _logger);
        try
        {
            tap.Receiver = _media.Start(port, tap.OnPcm); // listen before Asterisk starts sending
            await _ari.CreateBridgeAsync(tap.BridgeId, ct);
            await _ari.SnoopAsync(channelId, tap.SnoopId, ct);
            await _ari.ExternalMediaAsync(tap.MediaChannelId, $"{_options.MediaHost}:{port}", ct);
            await _ari.AddToBridgeAsync(tap.BridgeId, new[] { tap.SnoopId, tap.MediaChannelId }, ct);
            _logger.LogInformation("Tapping {Speaker} ({Channel}) -> {Host}:{Port}", speaker, channelId, _options.MediaHost, port);
            return tap;
        }
        catch (Exception ex)
        {
            _logger.LogError("Could not tap {Speaker} on {Channel}: {Error}", speaker, channelId, ex.Message);
            await ReleaseAsync(tap, CancellationToken.None);
            return null;
        }
    }

    private async Task TeardownAsync(TappedCall call, CancellationToken ct)
    {
        foreach (var tap in call.Legs)
        {
            await tap.StopAsync(); // emits the last sentence and waits for its analysis to be queued
            await ReleaseAsync(tap, ct);
        }
        try
        {
            await _sink.EndCallAsync(call.SessionId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Ending call {Session} failed: {Error}", call.SessionId, ex.Message);
        }
        _logger.LogInformation("Call {Session} ended; taps removed.", call.SessionId);
    }

    private async Task ReleaseAsync(LegTap tap, CancellationToken ct)
    {
        foreach (var cleanup in new Func<Task>[]
                 {
                     () => _ari.HangupAsync(tap.SnoopId, ct),
                     () => _ari.HangupAsync(tap.MediaChannelId, ct),
                     () => _ari.DestroyBridgeAsync(tap.BridgeId, ct),
                 })
        {
            try
            {
                await cleanup();
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Tap cleanup: {Error}", ex.Message);
            }
        }
        if (tap.Receiver != null)
        {
            await tap.Receiver.DisposeAsync();
        }
        _freePorts.Enqueue(tap.Port);
    }

    private static (string Id, string Ext)? Leg(JsonElement channel)
    {
        var id = Str(channel, "id");
        var name = Str(channel, "name");
        if (id == null || name == null)
        {
            return null;
        }
        var m = PjsipChannel.Match(name);
        return m.Success ? (id, m.Groups["ext"].Value) : null;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private sealed class TappedCall
    {
        public TappedCall(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public Guid SessionId { get; }
        public List<LegTap> Legs { get; } = new();
    }

    /// <summary>One side of a call: audio in -> sentences -> sink, in order, without blocking the receiver.</summary>
    private sealed class LegTap
    {
        private readonly Guid _sessionId;
        private readonly ICallSink _sink;
        private readonly ILogger _logger;
        private readonly UtteranceSegmenter _segmenter;
        // Pcm == null: the detection was discarded (too short) - only its live words need clearing
        private readonly Channel<(string Id, byte[]? Pcm)> _sentences = Channel.CreateUnbounded<(string, byte[]?)>();
        private readonly Task _worker;
        private int _utteranceNo = 1;
        private int _partialBusy;

        public LegTap(Guid sessionId, string channelId, string speaker, int port, string snoopId, string mediaChannelId,
            string bridgeId, ICallSink sink, SegmenterOptions segmenter, ILogger logger)
        {
            _sessionId = sessionId;
            ChannelId = channelId;
            Speaker = speaker;
            Port = port;
            SnoopId = snoopId;
            MediaChannelId = mediaChannelId;
            BridgeId = bridgeId;
            _sink = sink;
            _logger = logger;
            _segmenter = new UtteranceSegmenter(segmenter, OnSentence, OnPartial, OnDiscarded);
            _worker = Task.Run(ProcessSentencesAsync);
        }

        public string ChannelId { get; }
        public string Speaker { get; }
        public int Port { get; }
        public string SnoopId { get; }
        public string MediaChannelId { get; }
        public string BridgeId { get; }
        public IAsyncDisposable? Receiver { get; set; }

        private string CurrentUtteranceId => $"{SnoopId}-{_utteranceNo}";

        /// <summary>Called from the receiver's thread for every packet.</summary>
        public void OnPcm(byte[] pcm) => _segmenter.Feed(pcm);

        private void OnSentence(byte[] pcm)
        {
            _sentences.Writer.TryWrite((CurrentUtteranceId, pcm));
            _utteranceNo++;
        }

        private void OnDiscarded()
        {
            _sentences.Writer.TryWrite((CurrentUtteranceId, null));
            _utteranceNo++;
        }

        private void OnPartial(byte[] pcm)
        {
            if (Interlocked.Exchange(ref _partialBusy, 1) == 1)
            {
                return; // the previous live update is still being transcribed; skip this one
            }
            var id = CurrentUtteranceId;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _sink.OnPartialAsync(_sessionId, Speaker, id, pcm, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Live transcript failed: {Error}", ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref _partialBusy, 0);
                }
            });
        }

        private async Task ProcessSentencesAsync()
        {
            await foreach (var (id, pcm) in _sentences.Reader.ReadAllAsync())
            {
                try
                {
                    if (pcm == null)
                    {
                        await _sink.OnDiscardedAsync(_sessionId, Speaker, id, CancellationToken.None);
                    }
                    else
                    {
                        await _sink.OnUtteranceAsync(_sessionId, Speaker, id, pcm, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Analysing a {Speaker} sentence failed: {Error}", Speaker, ex.Message);
                }
            }
        }

        public async Task StopAsync()
        {
            if (Receiver != null)
            {
                await Receiver.DisposeAsync(); // no more audio, so the segmenter isn't fed concurrently
                Receiver = null;
            }
            _segmenter.Flush();
            _sentences.Writer.TryComplete();
            await Task.WhenAny(_worker, Task.Delay(TimeSpan.FromSeconds(60)));
        }
    }
}
