using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ECFD.Application.Audio;
using ECFD.Infrastructure.Asterisk;
using static ECFD.Tests.TelephonyTestAudio;

namespace ECFD.Tests;

public class RtpPacketTests
{
    [Fact]
    public void Plain_Header_Payload_Starts_At_12()
    {
        var packet = ToRtpPackets(Tone(20)).First();

        Assert.True(RtpPacket.TryGetPayload(packet, out var offset, out var length, out var seq, out var pt));
        Assert.Equal(12, offset);
        Assert.Equal(640, length); // 20 ms of 16 kHz 16-bit audio
        Assert.Equal(1000, seq);
        Assert.Equal(118, pt);
    }

    [Fact]
    public void Csrc_And_Extension_Are_Skipped()
    {
        // version 2, extension bit, 1 CSRC; extension header with 1 word
        var packet = new byte[12 + 4 + 4 + 4 + 6];
        packet[0] = 0x80 | 0x10 | 0x01;
        packet[12 + 4 + 2] = 0;
        packet[12 + 4 + 3] = 1;

        Assert.True(RtpPacket.TryGetPayload(packet, out var offset, out var length, out _, out _));
        Assert.Equal(24, offset);
        Assert.Equal(6, length);
    }

    [Theory]
    [InlineData(new byte[] { 0x80, 0, 0 })]                                  // too short
    [InlineData(new byte[] { 0x40, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2 })] // version 1
    public void Rejects_Non_Rtp(byte[] packet)
    {
        Assert.False(RtpPacket.TryGetPayload(packet, out _, out _, out _, out _));
    }

    [Fact]
    public void L16_Big_Endian_Becomes_Little_Endian()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0xCD, 0xAB }, RtpPacket.L16ToLittleEndian(new byte[] { 0x12, 0x34, 0xAB, 0xCD }));
    }
}

public class UtteranceSegmenterTests
{
    private static (List<byte[]> Sentences, List<byte[]> Partials, UtteranceSegmenter Seg) Make(SegmenterOptions? o = null)
    {
        var sentences = new List<byte[]>();
        var partials = new List<byte[]>();
        return (sentences, partials, new UtteranceSegmenter(o ?? new SegmenterOptions(), sentences.Add, partials.Add));
    }

    [Fact]
    public void Two_Sentences_Separated_By_A_Pause()
    {
        var (sentences, partials, seg) = Make();

        seg.Feed(Concat(Silence(1000), Tone(1500), Silence(1500), Tone(800), Silence(1500)));

        Assert.Equal(2, sentences.Count);
        // speech + up to 600 ms kept from before it + 400 ms kept after it
        Assert.InRange(DurationMs(sentences[0]), 1500, 2550);
        Assert.InRange(DurationMs(sentences[1]), 800, 1850);
        Assert.NotEmpty(partials); // live words during the 1.5 s sentence
    }

    [Fact]
    public void Feeding_In_Small_Packets_Gives_The_Same_Result()
    {
        var (sentences, _, seg) = Make();
        var audio = Concat(Silence(500), Tone(1200), Silence(1500));

        for (int i = 0; i < audio.Length; i += 320) // 10 ms pieces, not frame-aligned to the 20 ms frames
        {
            seg.Feed(audio.AsSpan(i, Math.Min(320, audio.Length - i)));
        }

        Assert.Single(sentences);
    }

    [Fact]
    public void A_Long_Monologue_Is_Cut_At_The_Maximum()
    {
        var (sentences, _, seg) = Make();

        seg.Feed(Concat(Silence(300), Tone(20000), Silence(1500)));

        Assert.Equal(2, sentences.Count);
        Assert.InRange(DurationMs(sentences[0]), 14900, 15100);
    }

    [Fact]
    public void Clicks_And_Short_Noises_Are_Ignored()
    {
        var (sentences, _, seg) = Make();

        seg.Feed(Concat(Silence(500), Tone(40), Silence(1500), Tone(120), Silence(1500)));

        Assert.Empty(sentences);
    }

    [Fact]
    public void A_One_Word_Answer_Is_Kept()
    {
        var (sentences, _, seg) = Make();

        seg.Feed(Concat(Silence(500), Tone(250), Silence(1500))); // "لأ"

        Assert.Single(sentences);
    }

    [Fact]
    public void Flush_Emits_The_Sentence_In_Progress()
    {
        var (sentences, _, seg) = Make();
        seg.Feed(Concat(Silence(300), Tone(1000)));
        Assert.Empty(sentences);

        seg.Flush();

        Assert.Single(sentences);
    }

    [Fact]
    public void Quiet_Background_Is_Never_Speech()
    {
        var (sentences, _, seg) = Make();
        seg.Feed(Silence(5000, noise: 250));
        seg.Flush();
        Assert.Empty(sentences);
    }
}

public class RtpReceiverTests
{
    [Fact]
    public async Task Receives_Rtp_Over_Udp_And_Delivers_Little_Endian_Pcm()
    {
        var received = new ConcurrentQueue<byte[]>();
        await using var receiver = RtpReceiver.Start(0, received.Enqueue, NullLogger.Instance);
        var audio = Tone(100); // 5 packets

        using var sender = new UdpClient();
        foreach (var packet in ToRtpPackets(audio))
        {
            await sender.SendAsync(packet, new IPEndPoint(IPAddress.Loopback, receiver.Port));
        }
        await sender.SendAsync(new byte[] { 1, 2, 3 }, new IPEndPoint(IPAddress.Loopback, receiver.Port)); // junk

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (received.Count < 5 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(5, received.Count);
        Assert.Equal(audio, received.SelectMany(p => p).ToArray());
    }
}
