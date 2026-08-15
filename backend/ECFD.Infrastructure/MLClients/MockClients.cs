using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECFD.Application.Interfaces;

namespace ECFD.Infrastructure.MLClients;

public class MockAsrClient : IAsrClient
{
    private static readonly string[] SamplePhrases = new[]
    {
        "أهلاً، معاك أحمد من الدعم الفني للـ IT",
        "عندنا مشكلة في حسابك ولازم نصلحها حالاً",
        "هيوصلك كود تأكيد على الموبايل في رسالة",
        "قولي الكود اللي وصلك عشان نوقف الاختراق"
    };

    private static int _phraseIdx = 0;

    public Task<AsrResult> AnalyzeAudioAsync(Guid sessionId, Guid segmentId, byte[] pcmAudio, CancellationToken cancellationToken = default)
    {
        string text = SamplePhrases[_phraseIdx % SamplePhrases.Length];
        _phraseIdx++;

        return Task.FromResult(new AsrResult(
            segmentId,
            text,
            0.94f,
            true,
            1000,
            3000,
            "faster-whisper-mock-v1"
        ));
    }
}

public class MockNlpClient : INlpClient
{
    public Task<NlpResult> AnalyzeTextAsync(Guid sessionId, Guid segmentId, string text, CancellationToken cancellationToken = default)
    {
        var tactics = new List<TacticMatch>();

        if (text.Contains("IT") || text.Contains("الدعم الفني") || text.Contains("البنك"))
        {
            tactics.Add(new TacticMatch("IMPERSONATION", 0.95f));
        }

        if (text.Contains("حالاً") || text.Contains("مشكلة") || text.Contains("لازم"))
        {
            tactics.Add(new TacticMatch("URGENCY", 0.88f));
            tactics.Add(new TacticMatch("AUTHORITY", 0.82f));
        }

        if (text.Contains("كود") || text.Contains("OTP") || text.Contains("رسالة"))
        {
            tactics.Add(new TacticMatch("OTP_REQUEST", 0.97f));
            tactics.Add(new TacticMatch("SENSITIVE_ACTION", 0.85f));
        }

        return Task.FromResult(new NlpResult(
            segmentId,
            tactics,
            "marbert-nlp-mock-v1"
        ));
    }
}

public class MockAntiSpoofClient : IAntiSpoofClient
{
    public Task<VoiceAnalysisResult> AnalyzeVoiceAsync(Guid sessionId, Guid windowId, byte[] pcmAudio, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new VoiceAnalysisResult(
            windowId,
            0.15f, // Baseline genuine speech
            0.92f, // High audio quality
            "aasist-antispoof-mock-v1"
        ));
    }
}
