using System;
using System.Collections.Generic;
using System.Linq;

namespace ECFD.Tests;

/// <summary>Synthetic 16 kHz 16-bit little-endian PCM for the telephony tests.</summary>
internal static class TelephonyTestAudio
{
    public const int Rate = 16000;

    public static byte[] Tone(int ms, short amplitude = 4000, double hz = 300) =>
        Samples(ms, i => (short)(amplitude * Math.Sin(2 * Math.PI * hz * i / Rate)));

    /// <summary>Quiet background noise, well below speech level.</summary>
    public static byte[] Silence(int ms, int noise = 60)
    {
        var rng = new Random(42);
        return Samples(ms, _ => (short)rng.Next(-noise, noise + 1));
    }

    public static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    public static int DurationMs(byte[] pcm) => pcm.Length / 2 * 1000 / Rate;

    /// <summary>Splits PCM into RTP packets the way Asterisk sends slin16: 20 ms, big-endian samples.</summary>
    public static IEnumerable<byte[]> ToRtpPackets(byte[] pcmLittleEndian, int frameMs = 20)
    {
        int frameBytes = Rate * frameMs / 1000 * 2;
        ushort seq = 1000;
        uint timestamp = 0;
        for (int offset = 0; offset < pcmLittleEndian.Length; offset += frameBytes)
        {
            int n = Math.Min(frameBytes, pcmLittleEndian.Length - offset);
            var packet = new byte[12 + n];
            packet[0] = 0x80;          // RTP version 2
            packet[1] = 118;           // dynamic payload type, as Asterisk uses for slin16
            packet[2] = (byte)(seq >> 8);
            packet[3] = (byte)seq;
            packet[4] = (byte)(timestamp >> 24);
            packet[5] = (byte)(timestamp >> 16);
            packet[6] = (byte)(timestamp >> 8);
            packet[7] = (byte)timestamp;
            for (int i = 0; i + 1 < n; i += 2)
            {
                packet[12 + i] = pcmLittleEndian[offset + i + 1]; // big-endian on the wire
                packet[12 + i + 1] = pcmLittleEndian[offset + i];
            }
            seq++;
            timestamp += (uint)(n / 2);
            yield return packet;
        }
    }

    private static byte[] Samples(int ms, Func<int, short> sample)
    {
        int count = Rate * ms / 1000;
        var bytes = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            short s = sample(i);
            bytes[2 * i] = (byte)s;
            bytes[2 * i + 1] = (byte)(s >> 8);
        }
        return bytes;
    }
}
