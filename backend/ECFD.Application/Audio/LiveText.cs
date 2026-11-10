using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECFD.Application.Audio;

/// <summary>One run of live words: confirmed (will not change) or still a guess.</summary>
public record LiveTextPart(string Text, bool Stable);

/// <summary>
/// Makes the live transcript of one sentence stop flickering. The sentence-so-far is re-transcribed about
/// once a second from audio that ends mid-word, so every pass can spell earlier words differently. A word is
/// confirmed once two passes in a row agree on it at the same position; after that it is shown as confirmed
/// and never changes again, while unconfirmed words are shown as guesses. The finished sentence still
/// replaces the live text, so a wrongly confirmed word lasts only until then. Not thread-safe.
/// </summary>
public sealed class LiveTextStabilizer
{
    private readonly List<string?> _confirmed = new(); // word per position once confirmed
    private string[] _previous = Array.Empty<string>();

    public IReadOnlyList<LiveTextPart> Update(string text)
    {
        var words = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length && i < _previous.Length; i++)
        {
            while (_confirmed.Count <= i)
            {
                _confirmed.Add(null);
            }
            if (_confirmed[i] == null && Key(words[i]) == Key(_previous[i]) && Key(words[i]).Length > 0)
            {
                _confirmed[i] = words[i];
            }
        }
        _previous = words;

        // confirmed words keep their place even if this pass came back shorter
        int lastConfirmed = _confirmed.FindLastIndex(w => w != null);
        int length = Math.Max(words.Length, lastConfirmed + 1);
        var parts = new List<LiveTextPart>();
        for (int i = 0; i < length; i++)
        {
            var confirmed = i < _confirmed.Count ? _confirmed[i] : null;
            var word = confirmed ?? words[i];
            bool stable = confirmed != null;
            if (parts.Count > 0 && parts[^1].Stable == stable)
            {
                parts[^1] = parts[^1] with { Text = parts[^1].Text + " " + word };
            }
            else
            {
                parts.Add(new LiveTextPart(word, stable));
            }
        }
        return parts;
    }

    /// <summary>The word without punctuation, vowel marks and alef/ya spelling variants, lower-cased.</summary>
    internal static string Key(string word)
    {
        var sb = new StringBuilder(word.Length);
        foreach (var ch in word)
        {
            if (char.IsPunctuation(ch) || char.IsSymbol(ch) || (ch >= 'ً' && ch <= 'ْ') || ch == 'ٰ' || ch == 'ـ')
            {
                continue;
            }
            sb.Append(ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                _ => char.ToLowerInvariant(ch)
            });
        }
        return sb.ToString();
    }
}

/// <summary>
/// Spots transcripts that are not speech: the speech model invents words when it is handed a click, a
/// breath or line noise, and invented words must not reach the rules or the analyst.
/// </summary>
public static class TranscriptGuard
{
    /// <param name="audioMs">Length of the audio that produced <paramref name="text"/>.</param>
    public static bool LooksInvented(string? text, float confidence, long audioMs)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        // a very short sound the model is unsure about (confidence 0 = the service did not report one)
        if (audioMs > 0 && audioMs < 500 && confidence > 0f && confidence < 0.45f)
        {
            return true;
        }
        // far more text than anyone can say in that time
        if (audioMs > 0 && text.Length > 15 + audioMs * 35 / 1000)
        {
            return true;
        }
        // the same word over and over
        int run = 1;
        for (int i = 1; i < words.Length; i++)
        {
            run = LiveTextStabilizer.Key(words[i]) == LiveTextStabilizer.Key(words[i - 1]) ? run + 1 : 1;
            if (run >= 4)
            {
                return true;
            }
        }
        return false;
    }
}
