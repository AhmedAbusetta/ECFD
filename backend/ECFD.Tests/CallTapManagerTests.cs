using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ECFD.Infrastructure.Asterisk;
using static ECFD.Tests.TelephonyTestAudio;

namespace ECFD.Tests;

public class CallTapManagerTests
{
    private sealed class FakeAri : IAriClient
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public string? FailOn { get; set; }

        private Task Record(string call)
        {
            Calls.Enqueue(call);
            if (FailOn != null && call.StartsWith(FailOn))
            {
                throw new AriException("simulated ARI failure");
            }
            return Task.CompletedTask;
        }

        public Task SnoopAsync(string channelId, string snoopId, CancellationToken ct = default) => Record($"snoop {channelId} as {snoopId}");
        public Task ExternalMediaAsync(string channelId, string externalHost, CancellationToken ct = default) => Record($"media {channelId} -> {externalHost}");
        public Task CreateBridgeAsync(string bridgeId, CancellationToken ct = default) => Record($"bridge {bridgeId}");
        public Task AddToBridgeAsync(string bridgeId, IEnumerable<string> channelIds, CancellationToken ct = default) => Record($"add {bridgeId} {string.Join(",", channelIds)}");
        public Task HangupAsync(string channelId, CancellationToken ct = default) => Record($"hangup {channelId}");
        public Task DestroyBridgeAsync(string bridgeId, CancellationToken ct = default) => Record($"destroy {bridgeId}");
        public Task RunEventsAsync(Func<JsonElement, Task> onEvent, Action? onConnected, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeMedia : IMediaReceiverFactory
    {
        public ConcurrentDictionary<int, Action<byte[]>> Open { get; } = new();

        public IAsyncDisposable Start(int port, Action<byte[]> onPcm)
        {
            Open[port] = onPcm;
            return new Closer(() => Open.TryRemove(port, out _));
        }

        private sealed class Closer : IAsyncDisposable
        {
            private readonly Action _close;
            public Closer(Action close) => _close = close;
            public ValueTask DisposeAsync()
            {
                _close();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeSink : ICallSink
    {
        public ConcurrentQueue<(string Caller, string Employee)> Started { get; } = new();
        public ConcurrentQueue<(string Speaker, int Ms)> Sentences { get; } = new();
        public ConcurrentQueue<Guid> Ended { get; } = new();

        public Task<Guid> StartCallAsync(string externalCallId, string callerEndpoint, string employeeEndpoint, CancellationToken ct)
        {
            Started.Enqueue((callerEndpoint, employeeEndpoint));
            return Task.FromResult(Guid.NewGuid());
        }

        public Task OnUtteranceAsync(Guid sessionId, string speaker, string utteranceId, byte[] pcm16le, CancellationToken ct)
        {
            Sentences.Enqueue((speaker, DurationMs(pcm16le)));
            return Task.CompletedTask;
        }

        public Task OnPartialAsync(Guid sessionId, string speaker, string utteranceId, byte[] pcm16le, CancellationToken ct) => Task.CompletedTask;

        public ConcurrentQueue<string> Discarded { get; } = new();

        public Task OnDiscardedAsync(Guid sessionId, string speaker, string utteranceId, CancellationToken ct)
        {
            Discarded.Enqueue(utteranceId);
            return Task.CompletedTask;
        }

        public Task EndCallAsync(Guid sessionId, CancellationToken ct)
        {
            Ended.Enqueue(sessionId);
            return Task.CompletedTask;
        }
    }

    private readonly FakeAri _ari = new();
    private readonly FakeMedia _media = new();
    private readonly FakeSink _sink = new();
    private readonly CallTapManager _taps;

    public CallTapManagerTests()
    {
        var options = new AsteriskOptions { MediaHost = "10.0.0.5", MediaPortStart = 40000, MediaPortCount = 4, EmployeeExtensions = new[] { "1001" } };
        _taps = new CallTapManager(_ari, _media, _sink, options, NullLogger<CallTapManager>.Instance);
    }

    private static JsonElement Dial(string status, string callerId = "1696-1", string callerName = "PJSIP/1002-00000001",
        string peerId = "1696-2", string peerName = "PJSIP/1001-00000002") =>
        JsonSerializer.SerializeToElement(new
        {
            type = "Dial",
            dialstatus = status,
            caller = new { id = callerId, name = callerName, state = "Up" },
            peer = new { id = peerId, name = peerName, state = "Up" },
        });

    private static JsonElement Destroyed(string id) =>
        JsonSerializer.SerializeToElement(new { type = "ChannelDestroyed", channel = new { id, name = "PJSIP/x-1" } });

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
        Assert.True(condition());
    }

    [Fact]
    public async Task Answered_Call_Taps_Both_Sides_With_The_Right_Speaker()
    {
        await _taps.HandleEventAsync(Dial("ANSWER"), CancellationToken.None);

        Assert.Equal(("1002", "1001"), Assert.Single(_sink.Started));
        var calls = _ari.Calls.ToList();
        Assert.Contains(calls, c => c.StartsWith("snoop 1696-1 as "));                     // caller's voice
        Assert.Contains(calls, c => c.StartsWith("snoop 1696-2 as "));                     // employee's voice
        Assert.Contains(calls, c => c.StartsWith("media ") && c.EndsWith("-> 10.0.0.5:40000"));
        Assert.Contains(calls, c => c.StartsWith("media ") && c.EndsWith("-> 10.0.0.5:40001"));
        Assert.Equal(2, calls.Count(c => c.StartsWith("bridge ")));
        Assert.Equal(2, calls.Count(c => c.StartsWith("add ")));
        Assert.Equal(2, _media.Open.Count);
        Assert.Equal(1, _taps.ActiveCalls);
    }

    [Fact]
    public async Task Audio_On_Each_Port_Becomes_Sentences_From_That_Speaker()
    {
        await _taps.HandleEventAsync(Dial("ANSWER"), CancellationToken.None);

        _media.Open[40000](Concat(Silence(300), Tone(1200), Silence(1500))); // caller's port
        _media.Open[40001](Concat(Silence(300), Tone(900), Silence(1500)));  // employee's port

        await WaitFor(() => _sink.Sentences.Count == 2);
        Assert.Contains(_sink.Sentences, s => s.Speaker == "CALLER");
        Assert.Contains(_sink.Sentences, s => s.Speaker == "EMPLOYEE");
    }

    [Fact]
    public async Task A_Click_Is_Discarded_So_Its_Live_Words_Can_Be_Cleared()
    {
        await _taps.HandleEventAsync(Dial("ANSWER"), CancellationToken.None);

        _media.Open[40000](Concat(Silence(300), Tone(80), Silence(1500)));

        await WaitFor(() => _sink.Discarded.Count == 1);
        Assert.Empty(_sink.Sentences);
    }

    [Fact]
    public async Task Employee_Is_Recognised_Even_When_The_Employee_Dials()
    {
        await _taps.HandleEventAsync(Dial("ANSWER", callerName: "PJSIP/1001-0000000a", peerName: "PJSIP/1002-0000000b"),
            CancellationToken.None);

        Assert.Equal(("1002", "1001"), Assert.Single(_sink.Started));
    }

    [Fact]
    public async Task Ringing_Duplicates_And_Non_Phone_Channels_Are_Ignored()
    {
        await _taps.HandleEventAsync(Dial("RINGING"), CancellationToken.None);
        await _taps.HandleEventAsync(Dial("ANSWER", callerName: "Snoop/1696-1-00000003"), CancellationToken.None);
        await _taps.HandleEventAsync(JsonDocument.Parse("""{"type":"StasisStart"}""").RootElement, CancellationToken.None);
        Assert.Empty(_sink.Started);

        await _taps.HandleEventAsync(Dial("ANSWER"), CancellationToken.None);
        await _taps.HandleEventAsync(Dial("ANSWER"), CancellationToken.None);
        Assert.Single(_sink.Started);
    }

    [Fact]
    public async Task Hangup_Emits_The_Last_Sentence_Removes_The_Taps_And_Frees_The_Ports()
    {
        await _taps.HandleEventAsync(Dial("ANSWER"), CancellationToken.None);
        _media.Open[40000](Concat(Silence(300), Tone(1000))); // still talking when the line drops

        await _taps.HandleEventAsync(Destroyed("1696-2"), CancellationToken.None);
        await _taps.HandleEventAsync(Destroyed("1696-1"), CancellationToken.None); // the other leg goes too

        await WaitFor(() => _sink.Ended.Count == 1);
        Assert.Single(_sink.Sentences);
        Assert.Equal(4, _ari.Calls.Count(c => c.StartsWith("hangup ")));  // 2 snoops + 2 media channels
        Assert.Equal(2, _ari.Calls.Count(c => c.StartsWith("destroy ")));
        Assert.Empty(_media.Open);
        Assert.Equal(0, _taps.ActiveCalls);

        // ports go back to the pool: the next call can use them again
        await _taps.HandleEventAsync(Dial("ANSWER", callerId: "2-1", peerId: "2-2"), CancellationToken.None);
        Assert.Equal(2, _media.Open.Count);
    }

    [Fact]
    public async Task A_Failed_Tap_Is_Cleaned_Up_And_The_Other_Side_Still_Works()
    {
        _ari.FailOn = "snoop 1696-2"; // the employee's side cannot be tapped

        await _taps.HandleEventAsync(Dial("ANSWER"), CancellationToken.None);

        Assert.Single(_media.Open); // only the caller's port stays open
        Assert.Contains(_ari.Calls, c => c.StartsWith("destroy "));
        Assert.Equal(1, _taps.ActiveCalls);
    }
}

public class AsteriskOptionsTests
{
    [Fact]
    public void Segmenter_Settings_Bind_From_Configuration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Asterisk:Enabled"] = "true",
                ["Asterisk:MediaHost"] = "192.168.1.5",
                ["Asterisk:EmployeeExtensions:0"] = "2001",
                ["Asterisk:Segmenter:SilenceToCloseMs"] = "1234",
                ["Asterisk:Segmenter:MinSpeechRms"] = "250",
            })
            .Build();

        var o = config.GetSection("Asterisk").Get<AsteriskOptions>()!;

        Assert.True(o.Enabled);
        Assert.Equal("192.168.1.5", o.MediaHost);
        Assert.Equal(new[] { "2001" }, o.Employees);   // replaces the lab default instead of adding to it
        Assert.Equal(1234, o.Segmenter.SilenceToCloseMs);
        Assert.Equal(250, o.Segmenter.MinSpeechRms);
        Assert.Equal(600, o.Segmenter.PreRollMs); // untouched settings keep their defaults
        Assert.Equal(new Uri("ws://localhost:8088/ari/events?app=ecfd-stasis&subscribeAll=true"), o.EventsUri());
    }

    [Fact]
    public void Without_Configuration_The_Lab_Employee_Is_1001()
    {
        Assert.Equal(new[] { "1001" }, new AsteriskOptions().Employees);
    }
}
