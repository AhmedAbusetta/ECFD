using System;
using ECFD.Application.Audio;

namespace ECFD.Infrastructure.Asterisk;

/// <summary>Bound from the "Asterisk" configuration section.</summary>
public class AsteriskOptions
{
    public const string SectionName = "Asterisk";

    /// <summary>false = no telephony; the dashboard's simulated calls still work.</summary>
    public bool Enabled { get; set; }
    /// <summary>ARI base URL, e.g. http://192.168.1.20:8088/ari (the PBX machine).</summary>
    public string AriUrl { get; set; } = "http://localhost:8088/ari";
    public string AriUser { get; set; } = "ecfd_ari_admin";
    public string AriPassword { get; set; } = "dev_ari_password";
    /// <summary>The Stasis application our snoop and External Media channels live in.</summary>
    public string AppName { get; set; } = "ecfd-stasis";

    /// <summary>
    /// This backend's IP address as the PBX can reach it; Asterisk sends call audio here.
    /// Required when Enabled (e.g. the laptop's address on the same hotspot as the PBX).
    /// </summary>
    public string MediaHost { get; set; } = "";
    /// <summary>First UDP port for call audio; each call leg uses one port from the range.</summary>
    public int MediaPortStart { get; set; } = 40000;
    public int MediaPortCount { get; set; } = 20;
    /// <summary>16 kHz signed linear: what the ASR expects. Asterisk converts from the phones' codec.</summary>
    public string MediaFormat { get; set; } = "slin16";

    /// <summary>Sentence detection on call audio ("Asterisk:Segmenter:SilenceToCloseMs" etc.), tunable without a rebuild.</summary>
    public SegmenterOptions Segmenter { get; set; } = new();

    /// <summary>
    /// Extensions that belong to employees; the other party on a call is the caller. Empty here on purpose:
    /// configuration binding appends to a non-empty default instead of replacing it. Read it through
    /// <see cref="Employees"/>, which falls back to the lab's employee phone (1001).
    /// </summary>
    public string[] EmployeeExtensions { get; set; } = Array.Empty<string>();

    public string[] Employees => EmployeeExtensions.Length > 0 ? EmployeeExtensions : new[] { "1001" };

    public Uri EventsUri()
    {
        var b = new UriBuilder(AriUrl.TrimEnd('/') + "/events")
        {
            Scheme = AriUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Query = $"app={Uri.EscapeDataString(AppName)}&subscribeAll=true",
        };
        return b.Uri;
    }
}
