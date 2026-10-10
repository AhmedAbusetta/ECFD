using System;
using System.Threading;
using System.Threading.Tasks;
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

    public PipelineCallSink(CallPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task<Guid> StartCallAsync(string externalCallId, string callerEndpoint, string employeeEndpoint, CancellationToken ct)
    {
        var session = await _pipeline.StartCallAsync(externalCallId, $"{callerEndpoint} (caller)", $"{employeeEndpoint} (employee)");
        return session.Id;
    }

    public async Task OnUtteranceAsync(Guid sessionId, string speaker, string utteranceId, byte[] pcm16le, CancellationToken ct)
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
        try
        {
            asr = await _pipeline.TranscribeAsync(session, pcm16le, PcmFormat, ct);
        }
        catch
        {
            await _pipeline.ClearPartialAsync(session, utteranceId, speaker);
            throw;
        }
        if (string.IsNullOrWhiteSpace(asr.Text))
        {
            await _pipeline.ClearPartialAsync(session, utteranceId, speaker);
            return;
        }

        await _pipeline.AnalyzeSegmentAsync(session, new TranscriptSegment
        {
            CallSessionId = session.Id,
            Text = asr.Text,
            Confidence = asr.Confidence,
            IsFinal = true,
            StartMs = asr.StartMs,
            EndMs = asr.EndMs,
            ModelVersion = asr.ModelVersion,
            Speaker = speaker
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
