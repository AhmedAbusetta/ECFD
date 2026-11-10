using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ECFD.Domain.Entities;

namespace ECFD.Api.Pipeline;

/// <summary>What is kept of a finished call: the verdict and why, never the audio.</summary>
public record CallRecord(
    Guid Id, DateTime StartedAt, DateTime? EndedAt, string Caller, string Employee, string? CallerLine,
    int PeakRisk, string FinalStage, float? VoiceScore, List<string> Evidence, List<CallRecordAlert> Alerts,
    List<EmployeeWarningRecord> Warnings, List<CallRecordTurn> Turns);

public record CallRecordAlert(string Title, string Description, DateTime At);
public record CallRecordTurn(int No, string Speaker, string Text);

/// <summary>Was the call judged correctly? Given by a person afterwards; this is how false alarms get counted.</summary>
public record CallFeedback(Guid CallId, bool Correct, string? Note, DateTime At);

/// <summary>
/// History of finished calls as one JSON line per call (CallLog:Path, default call-logs/calls.jsonl, never in
/// git), so a test session can be reviewed afterwards. CallLog:SaveTranscript=false keeps the verdict only.
/// </summary>
public class CallLogStore
{
    // Arabic stays readable in the file instead of being written as escape codes
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly string _calls;
    private readonly string _feedback;
    private readonly bool _saveTranscript;
    private readonly ILogger<CallLogStore> _logger;
    private readonly object _gate = new();

    public CallLogStore(IConfiguration configuration, IHostEnvironment environment, ILogger<CallLogStore> logger)
    {
        var path = configuration["CallLog:Path"] ?? "call-logs/calls.jsonl";
        _calls = Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
        _feedback = Path.Combine(Path.GetDirectoryName(_calls)!, "feedback.jsonl");
        _saveTranscript = configuration.GetValue("CallLog:SaveTranscript", true);
        _logger = logger;
    }

    public void Save(CallSession session, float? voiceScore)
    {
        try
        {
            CallRecord record;
            lock (session)
            {
                if (session.TranscriptSegments.Count == 0)
                {
                    return; // nobody spoke: nothing worth keeping
                }
                record = new CallRecord(
                    session.Id, session.StartedAt, session.EndedAt, session.CallerEndpoint, session.CalleeEndpoint,
                    session.CallerLine, session.PeakRisk, session.CurrentStage.ToString(), voiceScore,
                    session.EvidenceList.Select(e => e.Type.ToString()).Distinct().ToList(),
                    session.Alerts.Select(a => new CallRecordAlert(a.Title, a.Description, a.CreatedAt)).ToList(),
                    session.Warnings.ToList(),
                    _saveTranscript
                        ? session.TranscriptSegments.Select(t => new CallRecordTurn(t.SequenceNo, t.Speaker, t.Text)).ToList()
                        : new List<CallRecordTurn>());
            }
            Append(_calls, record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not save call {Session} to the call log: {Error}", session.Id, ex.Message);
        }
    }

    public void SaveFeedback(CallFeedback feedback) => Append(_feedback, feedback);

    /// <summary>The newest calls first, each with the latest feedback given for it (if any).</summary>
    public List<object> Recent(int take)
    {
        var feedback = ReadAll<CallFeedback>(_feedback).GroupBy(f => f.CallId).ToDictionary(g => g.Key, g => g.Last());
        return ReadAll<CallRecord>(_calls)
            .TakeLast(Math.Clamp(take, 1, 200))
            .Reverse()
            .Select(c => (object)new { call = c, feedback = feedback.GetValueOrDefault(c.Id) })
            .ToList();
    }

    private void Append<T>(string file, T item)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.AppendAllText(file, JsonSerializer.Serialize(item, Json) + Environment.NewLine);
        }
    }

    private List<T> ReadAll<T>(string file)
    {
        lock (_gate)
        {
            if (!File.Exists(file))
            {
                return new List<T>();
            }
            var items = new List<T>();
            foreach (var line in File.ReadLines(file))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(line) && JsonSerializer.Deserialize<T>(line, Json) is { } item)
                    {
                        items.Add(item);
                    }
                }
                catch (JsonException)
                {
                    // a half-written line (e.g. the process was killed) does not hide the rest of the history
                }
            }
            return items;
        }
    }
}
