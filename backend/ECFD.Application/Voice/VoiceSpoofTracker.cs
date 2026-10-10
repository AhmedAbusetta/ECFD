namespace ECFD.Application.Voice;

/// <summary>
/// Combines the anti-spoofing scores of one caller's sentences into a call-level score. A single sentence is
/// never enough (short clips and line noise make single scores unreliable), so the score is a mean weighted by
/// each clip's quality (≈ its length) and only exists after <see cref="MinSentences"/> sentences.
/// Thread-safe: sentences can be scored concurrently.
/// </summary>
public sealed class VoiceSpoofTracker
{
    /// <summary>Call-level score from which the caller's voice counts as suspected synthetic.</summary>
    public const float SuspicionThreshold = 0.7f;
    public const int MinSentences = 2;
    /// <summary>At least this much evidence (sum of quality, ≈ seconds / 3) before scoring.</summary>
    public const float MinTotalQuality = 1.0f;

    private readonly object _lock = new();
    private float _weightedSum;
    private float _totalQuality;
    private int _count;

    public int Count
    {
        get { lock (_lock) { return _count; } }
    }

    public void Add(float spoofProbability, float quality)
    {
        var p = Math.Clamp(spoofProbability, 0f, 1f);
        var q = Math.Clamp(quality, 0.05f, 1f);
        lock (_lock)
        {
            _weightedSum += p * q;
            _totalQuality += q;
            _count++;
        }
    }

    /// <summary>The caller's call-level spoof probability, or null while there is not enough evidence.</summary>
    public float? CallScore
    {
        get
        {
            lock (_lock)
            {
                if (_count < MinSentences || _totalQuality < MinTotalQuality)
                {
                    return null;
                }
                return _weightedSum / _totalQuality;
            }
        }
    }

    public bool IsSuspicious => CallScore is { } s && s >= SuspicionThreshold;
}
