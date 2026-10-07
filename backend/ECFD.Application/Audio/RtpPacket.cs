using System;
using System.Buffers.Binary;

namespace ECFD.Application.Audio;

/// <summary>
/// Minimal RTP (RFC 3550) reader for the audio Asterisk's External Media sends us.
/// Only what we need: find the payload and the sequence number; everything else is skipped.
/// </summary>
public static class RtpPacket
{
    public const int FixedHeaderLength = 12;

    /// <summary>Locates the audio payload inside one RTP packet. False for anything that isn't valid RTP v2.</summary>
    public static bool TryGetPayload(ReadOnlySpan<byte> packet, out int payloadOffset, out int payloadLength,
        out ushort sequenceNumber, out byte payloadType)
    {
        payloadOffset = payloadLength = 0;
        sequenceNumber = 0;
        payloadType = 0;
        if (packet.Length < FixedHeaderLength || packet[0] >> 6 != 2)
        {
            return false;
        }

        bool padding = (packet[0] & 0x20) != 0;
        bool extension = (packet[0] & 0x10) != 0;
        int csrcCount = packet[0] & 0x0F;
        payloadType = (byte)(packet[1] & 0x7F);
        sequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(2, 2));

        int offset = FixedHeaderLength + 4 * csrcCount;
        if (extension)
        {
            if (packet.Length < offset + 4)
            {
                return false;
            }
            int extensionWords = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset + 2, 2));
            offset += 4 + 4 * extensionWords;
        }

        int end = packet.Length;
        if (padding)
        {
            end -= packet[^1];
        }
        if (offset > end)
        {
            return false;
        }

        payloadOffset = offset;
        payloadLength = end - offset;
        return true;
    }

    /// <summary>
    /// RTP carries 16-bit linear audio (slin16 / L16) big-endian ("network order"); our pipeline
    /// and the ASR service use little-endian PCM. Returns the samples as little-endian bytes.
    /// </summary>
    public static byte[] L16ToLittleEndian(ReadOnlySpan<byte> bigEndian)
    {
        var output = new byte[bigEndian.Length & ~1];
        for (int i = 0; i + 1 < bigEndian.Length; i += 2)
        {
            output[i] = bigEndian[i + 1];
            output[i + 1] = bigEndian[i];
        }
        return output;
    }
}
