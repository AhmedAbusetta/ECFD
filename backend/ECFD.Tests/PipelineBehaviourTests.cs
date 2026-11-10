using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECFD.Api.Pipeline;
using ECFD.Application.Audio;
using ECFD.Application.Interfaces;
using ECFD.Application.Progression;
using ECFD.Application.Risk;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;
using ECFD.Infrastructure.MLClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECFD.Tests;

public class PipelineBehaviourTests
{
    private sealed class FakeNlp : INlpClient
    {
        public Task<NlpResult> AnalyzeTextAsync(Guid sessionId, Guid segmentId, string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new NlpResult(segmentId, new List<TacticMatch> { new("OTP_REQUEST", 0.95f) }, "fake"));
    }

    private sealed class FakeAnalyst : IAnalystClient
    {
        private readonly bool _flagValid;
        public FakeAnalyst(bool flagValid) => _flagValid = flagValid;

        public Task<AnalystResult?> AnalyzeTurnAsync(Guid sessionId, string speaker, string text, string? callerLine = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AnalystResult?>(new AnalystResult(1, true, 5, "NONE", false, 5, 0, "Normal", "STEADY", "NORMAL", "", "", "", "",
                new List<string>(), new List<AnalystEvidence>(), new List<string>(), new List<string>(), "fake", 1, null, _flagValid));

        public Task EndCallAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoNotifier : ISignalRNotifier
    {
        public Task NotifyCallStartedAsync(CallSession session) => Task.CompletedTask;
        public Task NotifyTranscriptFinalAsync(Guid sessionId, TranscriptSegment segment) => Task.CompletedTask;
        public Task NotifyTranscriptPartialAsync(Guid sessionId, string utteranceId, string speaker, string text, IReadOnlyList<LiveTextPart>? parts = null) => Task.CompletedTask;
        public Task NotifyTacticDetectedAsync(Guid sessionId, Evidence evidence) => Task.CompletedTask;
        public Task NotifyStageChangedAsync(Guid sessionId, AttackStage previousStage, AttackStage newStage, string trigger) => Task.CompletedTask;
        public Task NotifyRiskUpdatedAsync(Guid sessionId, RiskResult risk) => Task.CompletedTask;
        public Task NotifyAlertRaisedAsync(Guid sessionId, Alert alert) => Task.CompletedTask;
        public Task NotifyAnalystUpdatedAsync(Guid sessionId, AnalystResult result) => Task.CompletedTask;
        public Task NotifyVoiceUpdatedAsync(Guid sessionId, VoiceUpdate update) => Task.CompletedTask;
        public Task NotifyEmployeeWarnedAsync(Guid sessionId, string kind, int? delayMs = null) => Task.CompletedTask;
        public Task NotifyCallEndedAsync(Guid sessionId) => Task.CompletedTask;
    }

    private static CallPipeline Pipeline(bool analystFlagValid, CallLogStore? log = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<INlpClient>(new FakeNlp());
        services.AddSingleton<IAnalystClient>(new FakeAnalyst(analystFlagValid));
        return new CallPipeline(services.BuildServiceProvider(), new NoNotifier(), new AttackProgressionEngine(), new RiskEngine(),
            NullLogger<CallPipeline>.Instance, log);
    }

    private static async Task<bool> WaitFor(Func<bool> condition, int ms = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until && !condition())
        {
            await Task.Delay(20);
        }
        return condition();
    }

    private static TranscriptSegment CallerTurn(CallSession s, string text = "قولي الكود") =>
        new() { CallSessionId = s.Id, Text = text, Speaker = "CALLER", Confidence = 0.9f, IsFinal = true };

    [Fact]
    public async Task A_Real_Request_Holds_The_Risk_At_The_Floor_After_The_Analyst_Agrees()
    {
        var pipeline = Pipeline(analystFlagValid: true);
        var session = await pipeline.StartCallAsync("c1", "1002", "1001", "EXTERNAL");

        await pipeline.AnalyzeSegmentAsync(session, CallerTurn(session));
        await Task.Delay(300); // the analyst answers in the background

        Assert.Equal(RiskFusion.ForbiddenRequestFloor, session.RequestFloor);
        Assert.NotEmpty(session.EvidenceList);
        Assert.True(session.CurrentRisk >= 80);
    }

    [Fact]
    public async Task A_Flag_The_Analyst_Calls_Not_A_Real_Request_Is_Taken_Back()
    {
        var pipeline = Pipeline(analystFlagValid: false);
        var session = await pipeline.StartCallAsync("c2", "1002", "1001", "EXTERNAL");

        await pipeline.AnalyzeSegmentAsync(session, CallerTurn(session, "متقولش الكود لأي حد"));

        Assert.True(await WaitFor(() => session.RequestFloor == 0));
        Assert.Empty(session.EvidenceList);
        Assert.True(session.CurrentRisk < 60);
    }

    [Fact]
    public async Task A_Sentence_Waits_For_An_Earlier_One_From_The_Other_Speaker()
    {
        var pipeline = Pipeline(true);
        var call = Guid.NewGuid();
        var t = DateTime.UtcNow;
        pipeline.QueueUtterance(call, "caller-1", t);
        pipeline.QueueUtterance(call, "employee-1", t.AddSeconds(1));

        var waiting = pipeline.WaitForEarlierUtterancesAsync(call, "employee-1", t.AddSeconds(1), TimeSpan.FromSeconds(3));
        await Task.Delay(150);
        Assert.False(waiting.IsCompleted); // the caller's sentence is still being transcribed

        pipeline.CompleteUtterance(call, "caller-1");
        await waiting.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task The_Earlier_Sentence_Never_Waits_For_A_Later_One_And_Waiting_Is_Bounded()
    {
        var pipeline = Pipeline(true);
        var call = Guid.NewGuid();
        var t = DateTime.UtcNow;
        pipeline.QueueUtterance(call, "caller-1", t);
        pipeline.QueueUtterance(call, "employee-1", t.AddSeconds(1));

        await pipeline.WaitForEarlierUtterancesAsync(call, "caller-1", t, TimeSpan.FromSeconds(3)).WaitAsync(TimeSpan.FromSeconds(1));

        var started = DateTime.UtcNow;
        await pipeline.WaitForEarlierUtterancesAsync(call, "employee-1", t.AddSeconds(1), TimeSpan.FromMilliseconds(200));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1)); // gives up instead of stalling the call
    }

    // ------------------------------------------------------------------ call history
    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static CallLogStore Store(string dir, bool saveTranscript = true) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CallLog:Path"] = Path.Combine(dir, "calls.jsonl"),
            ["CallLog:SaveTranscript"] = saveTranscript.ToString(),
        }).Build(), new Env { ContentRootPath = dir }, NullLogger<CallLogStore>.Instance);

    [Fact]
    public async Task A_Finished_Call_Is_Saved_With_Its_Verdict_And_Can_Be_Marked_Right_Or_Wrong()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecfd-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = Store(dir);
            var pipeline = Pipeline(true, store);
            var session = await pipeline.StartCallAsync("c3", "1002", "1001", "EXTERNAL");
            await pipeline.AnalyzeSegmentAsync(session, CallerTurn(session));
            await pipeline.EndCallAsync(session.Id);

            store.SaveFeedback(new CallFeedback(session.Id, false, "false alarm", DateTime.UtcNow));
            var recent = store.Recent(10);

            var entry = Assert.Single(recent);
            var json = System.Text.Json.JsonSerializer.Serialize(entry);
            Assert.Contains("قولي الكود", File.ReadAllText(Path.Combine(dir, "calls.jsonl")), StringComparison.Ordinal); // readable Arabic in the file
            Assert.Contains("false alarm", json);
            Assert.Contains("OtpRequest", json);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task The_Transcript_Is_Left_Out_When_Saving_It_Is_Switched_Off()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecfd-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = Store(dir, saveTranscript: false);
            var pipeline = Pipeline(true, store);
            var session = await pipeline.StartCallAsync("c4", "1002", "1001", "EXTERNAL");
            await pipeline.AnalyzeSegmentAsync(session, CallerTurn(session));
            await pipeline.EndCallAsync(session.Id);

            Assert.Single(store.Recent(10));
            Assert.DoesNotContain("قولي الكود", File.ReadAllText(Path.Combine(dir, "calls.jsonl")), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    // ------------------------------------------------------------------ speech service retry
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _codes;
        public int Calls { get; private set; }
        public ScriptedHandler(params HttpStatusCode[] codes) => _codes = new Queue<HttpStatusCode>(codes);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var code = _codes.Dequeue();
            var body = code == HttpStatusCode.OK
                ? "{\"segmentId\":\"" + Guid.Empty + "\",\"text\":\"تمام\",\"confidence\":0.9,\"isFinal\":true,\"startMs\":0,\"endMs\":500,\"modelVersion\":\"x\",\"inferenceDurationMs\":1}"
                : "oops";
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task The_Speech_Client_Retries_Once_After_A_Server_Error()
    {
        var handler = new ScriptedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var client = new HttpAsrClient(new HttpClient(handler) { BaseAddress = new Uri("http://asr.test") });

        var result = await client.AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[320]);

        Assert.Equal("تمام", result.Text);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task The_Speech_Client_Does_Not_Retry_A_Bad_Request_And_Gives_Up_After_The_Second_Error()
    {
        var bad = new ScriptedHandler(HttpStatusCode.BadRequest);
        await Assert.ThrowsAsync<MlServiceException>(() =>
            new HttpAsrClient(new HttpClient(bad) { BaseAddress = new Uri("http://asr.test") }).AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[320]));
        Assert.Equal(1, bad.Calls);

        var down = new ScriptedHandler(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<MlServiceException>(() =>
            new HttpAsrClient(new HttpClient(down) { BaseAddress = new Uri("http://asr.test") }).AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[320]));
        Assert.Equal(2, down.Calls);
    }
}
