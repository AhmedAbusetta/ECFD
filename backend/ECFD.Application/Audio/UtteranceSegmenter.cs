using System;
using System.Collections.Generic;

namespace ECFD.Application.Audio;

/// <summary>Tuning for <see cref="UtteranceSegmenter"/>. Defaults suit one phone leg at 16 kHz.</summary>
public record SegmenterOptions
{
    public int SampleRate { get; init; } = 16000;
    public int FrameMs { get; init; } = 20;
    /// <summary>Lowest RMS (16-bit scale) ever treated as speech; quiet rooms stay below it.</summary>
    public double MinSpeechRms { get; init; } = 300;
    /// <summary>Speech must be this many times louder than the running background noise.</summary>
    public double NoiseMultiplier { get; init; } = 3.0;
    /// <summary>Consecutive loud frames needed to start a sentence (filters clicks).</summary>
    public int StartFrames { get; init; } = 3;
    /// <summary>Audio kept from before speech started, so the first syllable isn't clipped.</summary>
    public int PreRollMs { get; init; } = 600;
    /// <summary>Silence that ends a sentence.</summary>
    public int SilenceToCloseMs { get; init; } = 1000;
    /// <summary>Quiet audio kept after the last loud frame: soft word endings ("يخلص") live here.</summary>
    public int TrailingKeepMs { get; init; } = 400;
    /// <summary>Continuous speech is cut here so a long monologue still gets analysed.</summary>
    public int MaxUtteranceMs { get; init; } = 15000;
    /// <summary>
    /// Detections with less actual speech than this (clicks, line noise) are dropped. Kept low on purpose:
    /// one-word answers ("لأ", "تمام") are among the most important things an employee says.
    /// </summary>
    public int MinSpeechMs { get; init; } = 150;
    /// <summary>How often the sentence-so-far is offered for live (partial) transcription.</summary>
    public int PartialEveryMs { get; init; } = 1000;
}

/// <summary>
/// Cuts one speaker's continuous audio into sentences ("utterances") at pauses, so a real call needs no
/// push-to-talk. Energy-based voice activity detection with an adaptive noise floor: good enough for a
/// phone leg that carries one person; a neural VAD (Silero) can replace <see cref="IsSpeech"/> later.
/// Input and output are 16-bit little-endian mono PCM. Not thread-safe: feed one leg from one thread.
/// </summary>
public sealed class UtteranceSegmenter
{
    private readonly SegmenterOptions _o;
    private readonly int _frameBytes;
    private readonly Action<byte[]> _onUtterance;
    private readonly Action<byte[]>? _onPartial;
    private readonly Action? _onDiscarded;

    private readonly List<byte> _pending = new();      // bytes not yet forming a whole frame
    private readonly Queue<byte[]> _preRoll = new();   // recent silent frames, prepended when speech starts
    private readonly List<byte> _utterance = new();
    private double _noiseRms;
    private int _loudRun;
    private int _silentRun;
    private int _speechFrames;
    private int _sinceLastPartialMs;
    private bool _inSpeech;

    /// <param name="onDiscarded">A detection turned out too short to be speech (e.g. to clear its live words).</param>
    public UtteranceSegmenter(SegmenterOptions options, Action<byte[]> onUtterance, Action<byte[]>? onPartial = null,
        Action? onDiscarded = null)
    {
        _o = options;
        _frameBytes = options.SampleRate * options.FrameMs / 1000 * 2;
        _onUtterance = onUtterance;
        _onPartial = onPartial;
        _onDiscarded = onDiscarded;
        _noiseRms = options.MinSpeechRms / options.NoiseMultiplier;
    }

    public bool InSpeech => _inSpeech;

    /// <summary>Adds audio (any length) and emits every sentence completed by it.</summary>
    public void Feed(ReadOnlySpan<byte> pcm16le)
    {
        foreach (var b in pcm16le)
        {
            _pending.Add(b);
        }
        while (_pending.Count >= _frameBytes)
        {
            var frame = _pending.GetRange(0, _frameBytes).ToArray();
            _pending.RemoveRange(0, _frameBytes);
            ProcessFrame(frame);
        }
    }

    /// <summary>Call end: emits the sentence in progress if it is long enough.</summary>
    public void Flush()
    {
        if (_inSpeech)
        {
            Close();
        }
        _pending.Clear();
        _preRoll.Clear();
    }

    private void ProcessFrame(byte[] frame)
    {
        double rms = Rms(frame);
        bool loud = IsSpeech(rms);

        if (!_inSpeech)
        {
            _loudRun = loud ? _loudRun + 1 : 0;
            _preRoll.Enqueue(frame);
            while (_preRoll.Count > Math.Max(_o.PreRollMs / _o.FrameMs, _o.StartFrames))
            {
                _preRoll.Dequeue();
            }
            if (!loud)
            {
                _noiseRms = 0.95 * _noiseRms + 0.05 * rms; // learn the background only while nobody talks
            }
            if (_loudRun >= _o.StartFrames)
            {
                _inSpeech = true;
                _silentRun = 0;
                _speechFrames = _o.StartFrames;
                _sinceLastPartialMs = 0;
                foreach (var f in _preRoll)
                {
                    _utterance.AddRange(f);
                }
                _preRoll.Clear();
            }
            return;
        }

        _utterance.AddRange(frame);
        _silentRun = loud ? 0 : _silentRun + 1;
        if (loud)
        {
            _speechFrames++;
        }
        _sinceLastPartialMs += _o.FrameMs;

        if (_silentRun * _o.FrameMs >= _o.SilenceToCloseMs || DurationMs(_utterance.Count) >= _o.MaxUtteranceMs)
        {
            Close();
        }
        else if (_onPartial != null && _sinceLastPartialMs >= _o.PartialEveryMs)
        {
            _sinceLastPartialMs = 0;
            _onPartial(_utterance.ToArray());
        }
    }

    private void Close()
    {
        // drop most of the trailing silence but keep some, so a soft last syllable isn't cut
        int keepSilentFrames = Math.Min(_silentRun, _o.TrailingKeepMs / _o.FrameMs);
        int trimBytes = (_silentRun - keepSilentFrames) * _frameBytes;
        int length = Math.Max(0, _utterance.Count - trimBytes);
        var audio = _utterance.GetRange(0, length).ToArray();

        int speechMs = _speechFrames * _o.FrameMs;
        _utterance.Clear();
        _inSpeech = false;
        _loudRun = _silentRun = _speechFrames = 0;

        if (speechMs >= _o.MinSpeechMs)
        {
            _onUtterance(audio);
        }
        else
        {
            _onDiscarded?.Invoke();
        }
    }

    private bool IsSpeech(double rms) => rms >= Math.Max(_o.MinSpeechRms, _noiseRms * _o.NoiseMultiplier);

    private int DurationMs(int bytes) => bytes / 2 * 1000 / _o.SampleRate;

    private static double Rms(byte[] frame)
    {
        double sum = 0;
        int n = frame.Length / 2;
        for (int i = 0; i < n; i++)
        {
            short s = (short)(frame[2 * i] | frame[2 * i + 1] << 8);
            sum += (double)s * s;
        }
        return n == 0 ? 0 : Math.Sqrt(sum / n);
    }
}
