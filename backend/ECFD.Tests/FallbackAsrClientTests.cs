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
