using ECFD.Application.Voice;
using Xunit;

namespace ECFD.Tests;

public class VoiceSpoofTrackerTests
{
    [Fact]
    public void One_Sentence_Is_Never_Enough()
    {
        var t = new VoiceSpoofTracker();
        t.Add(1.0f, 1.0f);

        Assert.Null(t.CallScore);
        Assert.False(t.IsSuspicious);
    }

    [Fact]
    public void A_Synthetic_Voice_Becomes_Suspicious_After_Two_Sentences()
    {
        var t = new VoiceSpoofTracker();
        t.Add(0.99f, 1.0f);
        t.Add(0.97f, 1.0f);

        Assert.True(t.IsSuspicious);
        Assert.Equal(0.98f, t.CallScore!.Value, 3);
    }

    [Fact]
    public void A_Real_Voice_With_One_Odd_Sentence_Stays_Clear()
    {
        // one noisy clip scored high, the rest of the caller's speech is clearly human
        var t = new VoiceSpoofTracker();
        t.Add(0.95f, 0.5f);
        t.Add(0.02f, 1.0f);
        t.Add(0.01f, 1.0f);

        Assert.False(t.IsSuspicious);
        Assert.True(t.CallScore < VoiceSpoofTracker.SuspicionThreshold);
    }

    [Fact]
    public void Short_Clips_Count_Less_And_Need_More_Evidence()
    {
        var t = new VoiceSpoofTracker();
        t.Add(0.9f, 0.2f);  // two very short clips: not enough evidence yet
        t.Add(0.9f, 0.2f);
        Assert.Null(t.CallScore);

        t.Add(0.1f, 1.0f);  // a long, clearly human sentence outweighs them
        Assert.False(t.IsSuspicious);
    }
}
