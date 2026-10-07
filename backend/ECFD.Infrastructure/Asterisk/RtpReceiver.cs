using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ECFD.Application.Audio;
using Microsoft.Extensions.Logging;

namespace ECFD.Infrastructure.Asterisk;

/// <summary>Opens a UDP port that receives one call leg's audio.</summary>
public interface IMediaReceiverFactory
{
    /// <summary>Starts listening on <paramref name="port"/>; <paramref name="onPcm"/> gets 16-bit little-endian PCM.</summary>
    IAsyncDisposable Start(int port, Action<byte[]> onPcm);
}

public class RtpReceiverFactory : IMediaReceiverFactory
{
    private readonly ILoggerFactory _loggers;

    public RtpReceiverFactory(ILoggerFactory loggers)
    {
        _loggers = loggers;
    }

    public IAsyncDisposable Start(int port, Action<byte[]> onPcm) =>
        RtpReceiver.Start(port, onPcm, _loggers.CreateLogger<RtpReceiver>());
}

/// <summary>
/// Receives the RTP stream Asterisk's External Media channel sends for one call leg and hands the
/// audio on as little-endian PCM. Packets that aren't RTP are ignored.
/// </summary>
public sealed class RtpReceiver : IAsyncDisposable
{
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private readonly Action<byte[]> _onPcm;
    private readonly ILogger _logger;

    public int Port { get; }
    public long PacketsReceived { get; private set; }

    private RtpReceiver(int port, Action<byte[]> onPcm, ILogger logger)
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        _onPcm = onPcm;
        _logger = logger;
        _loop = Task.Run(ReceiveLoopAsync);
    }

    /// <summary>Port 0 picks a free port (used by tests); read it back from <see cref="Port"/>.</summary>
    public static RtpReceiver Start(int port, Action<byte[]> onPcm, ILogger logger) => new(port, onPcm, logger);

    private async Task ReceiveLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try
            {
                packet = await _udp.ReceiveAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                continue; // Windows reports ICMP "port unreachable" from earlier sends as a receive error
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!RtpPacket.TryGetPayload(packet.Buffer, out int offset, out int length, out _, out _) || length == 0)
            {
                continue;
            }
            PacketsReceived++;
            try
            {
                _onPcm(RtpPacket.L16ToLittleEndian(packet.Buffer.AsSpan(offset, length)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Audio handler failed on port {Port}: {Error}", Port, ex.Message);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _udp.Dispose();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }
        _stop.Dispose();
    }
}
