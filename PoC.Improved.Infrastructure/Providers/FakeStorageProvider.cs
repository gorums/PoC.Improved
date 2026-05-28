using PoC.Improved.Application.Providers;
using PoC.Improved.Infrastructure.Resilience;

namespace PoC.Improved.Infrastructure.Providers;

/// <summary>
/// Fake S3 provider for local testing.
/// Special feature names trigger different failure modes:
///   "photos" -> happy path
///   "flaky"  -> fails 2 times, succeeds on the 3rd (shows Polly retry working)
///   "down"   -> always fails with HttpRequestException (shows 503 mapping)
///   "slow"   -> takes 20s (shows Polly timeout -> 408 mapping)
/// </summary>
public sealed class FakeStorageProvider : IStorageProvider
{
    private readonly IServiceCallHandler _handler;
    private static int _flakyCounter;

    public FakeStorageProvider(IServiceCallHandler handler) => _handler = handler;

    public Task<List<string>> GetFoldersAsync(string feature, CancellationToken ct = default)
        => _handler.ExecuteAsync(
            operation: async token =>
            {
                await Task.Delay(30, token);

                switch (feature)
                {
                    case "down":
                        throw new HttpRequestException("S3 endpoint unreachable");

                    case "slow":
                        await Task.Delay(20_000, token); // Polly timeout fires at 5s
                        break;

                    case "flaky":
                        var n = Interlocked.Increment(ref _flakyCounter);
                        if (n % 3 != 0)
                            throw new HttpRequestException(
                                $"transient network blip (attempt {n})");
                        break;
                }

                return new List<string>
                {
                    $"{feature}/2024",
                    $"{feature}/2025",
                    $"{feature}/2026",
                };
            },
            operationDescription: $"listing folders for feature '{feature}'",
            serviceName: "S3",
            cancellationToken: ct);

    public Task<string> GetFileUrlAsync(string filePath, CancellationToken ct = default)
        => _handler.ExecuteAsync(
            operation: async _ =>
            {
                await Task.Delay(15, ct);

                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("filePath is required");

                return $"https://fake-s3.local/{filePath}?sig=demo&exp=3600";
            },
            operationDescription: $"signing url for '{filePath}'",
            serviceName: "S3",
            cancellationToken: ct);
}
