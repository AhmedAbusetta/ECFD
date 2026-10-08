using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ECFD.Application.Interfaces;
using ECFD.Infrastructure.MLClients;

namespace ECFD.Tests;

public class FallbackAsrClientTests
{
    private sealed class FakeAsr : IAsrClient
    {
        private readonly string _model;
        private readonly Func<CancellationToken, Task>? _behaviour;
        public int Calls { get; private set; }

        public FakeAsr(string model, Func<CancellationToken, Task>? behaviour = null)
        {
            _model = model;
            _behaviour = behaviour;
        }

        public async Task<AsrResult> AnalyzeAudioAsync(Guid sessionId, Guid segmentId, byte[] audio, string audioFormat = "pcm_s16le", CancellationToken cancellationToken = default)
        {
            Calls++;
            if (_behaviour != null)
                await _behaviour(cancellationToken);
            return new AsrResult(segmentId, "نص", 0.9f, true, 0, 1000, _model);
        }
    }

    private static FallbackAsrClient Create(IAsrClient primary, IAsrClient fallback, double budgetSeconds = 5) =>
        new(primary, fallback, TimeSpan.FromSeconds(budgetSeconds), NullLogger<FallbackAsrClient>.Instance);

    [Fact]
    public async Task Uses_Primary_When_It_Answers()
    {
        var primary = new FakeAsr("cohere");
        var fallback = new FakeAsr("whisper");

        var result = await Create(primary, fallback).AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[10]);

        Assert.Equal("cohere", result.ModelVersion);
        Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task Falls_Back_When_Primary_Errors()
    {
        var primary = new FakeAsr("cohere", _ => throw new MlServiceException("ASR service returned 500"));
        var fallback = new FakeAsr("whisper");

        var result = await Create(primary, fallback).AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[10]);

        Assert.Equal("whisper", result.ModelVersion);
    }

    [Fact]
    public async Task Falls_Back_When_Primary_Exceeds_Budget()
    {
        // Simulates a cloud cold start: the primary would answer, but too late.
        var primary = new FakeAsr("cohere", ct => Task.Delay(TimeSpan.FromSeconds(30), ct));
        var fallback = new FakeAsr("whisper");

        var result = await Create(primary, fallback, budgetSeconds: 0.2).AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[10]);

        Assert.Equal("whisper", result.ModelVersion);
    }

    [Fact]
    public async Task After_A_Failure_Next_Sentences_Skip_The_Primary_Until_The_Cooldown_Ends()
    {
        // A slow primary must not make every sentence of a live call wait out the budget again.
        var primary = new FakeAsr("cohere", ct => Task.Delay(TimeSpan.FromSeconds(30), ct));
        var fallback = new FakeAsr("whisper");
        var client = new FallbackAsrClient(primary, fallback, TimeSpan.FromSeconds(0.2),
            NullLogger<FallbackAsrClient>.Instance, cooldown: TimeSpan.FromMilliseconds(400));

        await client.AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[10]);
        var started = DateTime.UtcNow;
        var second = await client.AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[10]);

        Assert.Equal("whisper", second.ModelVersion);
        Assert.Equal(1, primary.Calls);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(150)); // no budget wait

        await Task.Delay(500);
        await client.AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[10]);
        Assert.Equal(2, primary.Calls); // the primary is tried again once the cooldown is over
    }

    [Fact]
    public async Task Does_Not_Fall_Back_When_Caller_Cancels()
    {
        var primary = new FakeAsr("cohere", ct => Task.Delay(TimeSpan.FromSeconds(30), ct));
        var fallback = new FakeAsr("whisper");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(primary, fallback).AnalyzeAudioAsync(Guid.NewGuid(), Guid.NewGuid(), new byte[10], cancellationToken: cts.Token));
        Assert.Equal(0, fallback.Calls);
    }
}
