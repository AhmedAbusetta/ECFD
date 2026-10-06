using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace ECFD.Tests;

/// <summary>
/// Runs the demo attack script through the real ASP.NET pipeline and listens on the
/// dashboard hub exactly like the frontend does.
/// </summary>
public class DemoPipelineIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] EventNames =
    {
        "call.started", "transcript.final", "tactic.detected", "stage.changed",
        "risk.updated", "alert.raised", "call.ended"
    };

    private readonly WebApplicationFactory<Program> _factory;

    public DemoPipelineIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Otp_Attack_Script_Reaches_Critical_And_Streams_Events_To_Dashboard()
    {
        var received = new ConcurrentQueue<(string Name, JsonElement Payload)>();
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/dashboard"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        foreach (var name in EventNames)
        {
            hub.On<JsonElement>(name, payload => received.Enqueue((name, payload)));
        }
        await hub.StartAsync();

        var http = _factory.CreateClient();
        (await http.PostAsJsonAsync("/api/demo/start-call", new { caller = "1002", callee = "1001" }))
            .EnsureSuccessStatusCode();

        string[] script =
        {
            "ألو مساء الخير، أنا أحمد من الدعم الفني للـ IT",
            "عندنا مشكلة في حسابك ولازم نصلحها حالاً",
            "قولي الكود اللي وصلك في رسالة عشان نوقف الاختراق"
        };
        JsonElement last = default;
        foreach (var line in script)
        {
            var response = await http.PostAsJsonAsync("/api/demo/utterance", new { text = line });
            response.EnsureSuccessStatusCode();
            last = await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.Equal("CredentialExtraction", last.GetProperty("stage").GetString());
        Assert.Equal("Critical", last.GetProperty("severity").GetString());

        // A follow-up utterance on an already-critical call must not raise a second alert.
        (await http.PostAsJsonAsync("/api/demo/utterance", new { text = script[2] })).EnsureSuccessStatusCode();
        (await http.PostAsync("/api/demo/end-call", null)).EnsureSuccessStatusCode();

        await WaitUntil(() => received.Any(e => e.Name == "call.ended"));

        var names = received.Select(e => e.Name).ToList();
        Assert.Contains("call.started", names);
        Assert.Contains("transcript.final", names);
        Assert.Contains("stage.changed", names);
        Assert.Contains("risk.updated", names);
        Assert.Single(names, n => n == "alert.raised");

        var tactics = received.Where(e => e.Name == "tactic.detected")
            .Select(e => e.Payload.GetProperty("tactic").GetString())
            .ToList();
        Assert.Contains("OTP_REQUEST", tactics);
        Assert.Contains("IDENTITY_CLAIM", tactics);
    }

    [Fact]
    public async Task Audio_Upload_Goes_Through_Asr_Into_The_Pipeline()
    {
        var http = _factory.CreateClient();
        (await http.PostAsJsonAsync("/api/demo/start-call", new { caller = "mic", callee = "1001" }))
            .EnsureSuccessStatusCode();

        // The in-process mock ASR ignores the bytes and returns its scripted phrases.
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        form.Add(audio, "audio", "utterance.webm");

        var response = await http.PostAsync("/api/demo/audio", form);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("text").GetString()));
        Assert.Equal("faster-whisper-mock-v1", body.GetProperty("asrModel").GetString());
        Assert.True(body.TryGetProperty("riskScore", out _));

        (await http.PostAsync("/api/demo/end-call", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Audio_Upload_Without_File_Is_Rejected()
    {
        var http = _factory.CreateClient();
        (await http.PostAsJsonAsync("/api/demo/start-call", new { caller = "mic", callee = "1001" }))
            .EnsureSuccessStatusCode();

        var response = await http.PostAsync("/api/demo/audio", new MultipartFormDataContent());

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        (await http.PostAsync("/api/demo/end-call", null)).EnsureSuccessStatusCode();
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Expected SignalR events did not arrive.");
            }
            await Task.Delay(50);
        }
    }
}
