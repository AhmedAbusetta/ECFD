using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ECFD.Application.Audio;
using ECFD.Application.Interfaces;
using ECFD.Domain.Entities;
using ECFD.Infrastructure.Asterisk;

namespace ECFD.Api.Pipeline;

/// <summary>Connects the PBX taps to the call pipeline: each sentence is transcribed, then analysed.</summary>
public class PipelineCallSink : ICallSink
{
    // what the Media Gateway delivers: 16 kHz mono 16-bit little-endian PCM (the ASR contract's default)
    private const string PcmFormat = "pcm_s16le";

    private readonly CallPipeline _pipeline;
    private readonly ILogger<PipelineCallSink> _logger;

    public PipelineCallSink(CallPipeline pipeline, ILogger<PipelineCallSink> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task<Guid> StartCallAsync(string externalCallId, string callerEndpoint, string employeeEndpoint, string? callerLine, CancellationToken ct)
    {
        var session = await _pipeline.StartCallAsync(externalCallId, $"{callerEndpoint} (caller)", $"{employeeEndpoint} (employee)", callerLine);
        return session.Id;
    }

    public async Task OnUtteranceAsync(Guid sessionId, string speaker, string utteranceId, byte[] pcm16le, DateTime speechEndedAtUtc, CancellationToken ct)
    {
        var session = _pipeline.Get(sessionId);
        if (session == null)
        {
            return;
        }
        _pipeline.MarkFinalized(utteranceId);
        if (speaker == "CALLER")
        {
            _pipeline.AnalyzeVoiceInBackground(session, pcm16le); // is this a real human voice?
        }
        AsrResult asr;
        var waited = DateTime.UtcNow - speechEndedAtUtc; // closing silence + time behind earlier sentences
        var started = DateTime.UtcNow;
        try
        {
            asr = await _pipeline.TranscribeAsync(session, pcm16le, PcmFormat, ct);
        }
        catch
        {
            await _pipeline.ClearPartialAsync(session, utteranceId, speaker);
            throw;
        }
        // a click or line noise: the speech model invents words for those, and invented words must not be analysed
        if (TranscriptGuard.LooksInvented(asr.Text, asr.Confidence, pcm16le.Length / 32))
        {
            await _pipeline.ClearPartialAsync(session, utteranceId, speaker);
            return;
        }
        _pipeline.ForgetLiveText(utteranceId);
        // where the time between "stopped talking" and "text on screen" goes, per sentence
        _logger.LogInformation("{Speaker} sentence ({Audio:F1} s): waited {Waited} ms, speech-to-text {Asr} ms.",
            speaker, pcm16le.Length / 32000.0, (int)waited.TotalMilliseconds, (int)(DateTime.UtcNow - started).TotalMilliseconds);

        await _pipeline.AnalyzeSegmentAsync(session, new TranscriptSegment
        {
            CallSessionId = session.Id,
            Text = asr.Text,
            Confidence = asr.Confidence,
            IsFinal = true,
            StartMs = asr.StartMs,
            EndMs = asr.EndMs,
            ModelVersion = asr.ModelVersion,
            Speaker = speaker,
            SpeechEndedAt = speechEndedAtUtc
        });
    }

    public async Task OnPartialAsync(Guid sessionId, string speaker, string utteranceId, byte[] pcm16le, CancellationToken ct)
    {
        var session = _pipeline.Get(sessionId);
        if (session == null || _pipeline.IsFinalized(utteranceId))
        {
            return;
        }
        var asr = await _pipeline.TranscribeAsync(session, pcm16le, PcmFormat, ct);
        await _pipeline.PushPartialAsync(session, utteranceId, speaker, asr.Text);
    }

    public async Task OnDiscardedAsync(Guid sessionId, string speaker, string utteranceId, CancellationToken ct)
    {
        var session = _pipeline.Get(sessionId);
        if (session == null)
        {
            return;
        }
        _pipeline.MarkFinalized(utteranceId); // a live result still in flight must not bring the words back
        await _pipeline.ClearPartialAsync(session, utteranceId, speaker);
    }

    public Task EndCallAsync(Guid sessionId, CancellationToken ct) => _pipeline.EndCallAsync(sessionId);
}
