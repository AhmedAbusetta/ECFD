using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ECFD.Infrastructure.Asterisk;

/// <summary>The few Asterisk REST Interface (ARI) operations ECFD needs to tap a call.</summary>
public interface IAriClient
{
    /// <summary>A Snoop channel that copies the audio coming FROM <paramref name="channelId"/> (one person's voice).</summary>
    Task SnoopAsync(string channelId, string snoopId, CancellationToken ct = default);
    /// <summary>A Snoop channel that speaks INTO <paramref name="channelId"/>: whatever it plays only that person hears.</summary>
    Task WhisperSnoopAsync(string channelId, string snoopId, CancellationToken ct = default);
    /// <summary>Plays media (e.g. "sound:/path/file" without extension) on a channel.</summary>
    Task PlayAsync(string channelId, string media, string playbackId, CancellationToken ct = default);
    /// <summary>An External Media channel that streams RTP to <paramref name="externalHost"/> (host:port).</summary>
    Task ExternalMediaAsync(string channelId, string externalHost, CancellationToken ct = default);
    Task CreateBridgeAsync(string bridgeId, CancellationToken ct = default);
    Task AddToBridgeAsync(string bridgeId, IEnumerable<string> channelIds, CancellationToken ct = default);
    /// <summary>Hangs up a channel; a channel that is already gone is not an error.</summary>
    Task HangupAsync(string channelId, CancellationToken ct = default);
    /// <summary>Destroys a bridge; a bridge that is already gone is not an error.</summary>
    Task DestroyBridgeAsync(string bridgeId, CancellationToken ct = default);
    /// <summary>Connects to the event WebSocket and calls <paramref name="onEvent"/> for every event until it closes.</summary>
    Task RunEventsAsync(Func<JsonElement, Task> onEvent, Action? onConnected, CancellationToken ct);
}

public class AriClient : IAriClient
{
    private readonly HttpClient _http;
    private readonly AsteriskOptions _options;

    public AriClient(HttpClient http, AsteriskOptions options)
    {
        _http = http;
        _options = options;
        _http.BaseAddress = new Uri(options.AriUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Authorization = BasicAuth(options);
    }

    private static AuthenticationHeaderValue BasicAuth(AsteriskOptions o) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.AriUser}:{o.AriPassword}")));

    public Task SnoopAsync(string channelId, string snoopId, CancellationToken ct = default) =>
        PostAsync($"channels/{Esc(channelId)}/snoop", ct,
            ("app", _options.AppName), ("spy", "in"), ("whisper", "none"), ("snoopId", snoopId));

    public Task WhisperSnoopAsync(string channelId, string snoopId, CancellationToken ct = default) =>
        PostAsync($"channels/{Esc(channelId)}/snoop", ct,
            ("app", _options.AppName), ("spy", "none"), ("whisper", "out"), ("snoopId", snoopId));

    public Task PlayAsync(string channelId, string media, string playbackId, CancellationToken ct = default) =>
        PostAsync($"channels/{Esc(channelId)}/play", ct, ("media", media), ("playbackId", playbackId));

    public Task ExternalMediaAsync(string channelId, string externalHost, CancellationToken ct = default) =>
        PostAsync("channels/externalMedia", ct,
            ("app", _options.AppName), ("channelId", channelId), ("external_host", externalHost),
            ("format", _options.MediaFormat), ("encapsulation", "rtp"), ("transport", "udp"),
            ("connection_type", "client"), ("direction", "both"));

    public Task CreateBridgeAsync(string bridgeId, CancellationToken ct = default) =>
        PostAsync("bridges", ct, ("type", "mixing"), ("bridgeId", bridgeId));

    public Task AddToBridgeAsync(string bridgeId, IEnumerable<string> channelIds, CancellationToken ct = default) =>
        PostAsync($"bridges/{Esc(bridgeId)}/addChannel", ct, ("channel", string.Join(",", channelIds)));

    public Task HangupAsync(string channelId, CancellationToken ct = default) =>
        DeleteAsync($"channels/{Esc(channelId)}", ct);

    public Task DestroyBridgeAsync(string bridgeId, CancellationToken ct = default) =>
        DeleteAsync($"bridges/{Esc(bridgeId)}", ct);

    public async Task RunEventsAsync(Func<JsonElement, Task> onEvent, Action? onConnected, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", BasicAuth(_options).ToString());
        await ws.ConnectAsync(_options.EventsUri(), ct);
        onConnected?.Invoke();

        var buffer = new byte[64 * 1024];
        var message = new List<byte>();
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
            message.AddRange(buffer.Take(result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }
            using var doc = JsonDocument.Parse(message.ToArray());
            message.Clear();
            await onEvent(doc.RootElement.Clone());
        }
    }

    private async Task PostAsync(string path, CancellationToken ct, params (string Key, string Value)[] query)
    {
        var url = path + "?" + string.Join("&", query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"));
        using var response = await _http.PostAsync(url, content: null, ct);
        await EnsureSuccess(response, $"POST {path}", ct);
    }

    private async Task DeleteAsync(string path, CancellationToken ct)
    {
        using var response = await _http.DeleteAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return; // already gone
        }
        await EnsureSuccess(response, $"DELETE {path}", ct);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, string what, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new AriException($"ARI {what} returned {(int)response.StatusCode}: {body}");
        }
    }

    private static string Esc(string id) => Uri.EscapeDataString(id);
}

public class AriException : Exception
{
    public AriException(string message) : base(message)
    {
    }
}
