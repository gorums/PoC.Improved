using System.Runtime.CompilerServices;
using PoC.Improved.Application.Cqrs;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Application.Folders;

/// <summary>
/// Demonstrates IStreamRequestHandler. Pulls the folder list from storage and yields
/// each path one at a time with a configurable delay so the streaming behaviour is
/// observable from the client.
/// </summary>
public sealed class StreamFoldersHandler : IStreamRequestHandler<StreamFoldersQuery, string>
{
    private readonly IStorageProvider _storage;

    public StreamFoldersHandler(IStorageProvider storage) => _storage = storage;

    public async IAsyncEnumerable<string> Handle(
        StreamFoldersQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var folders = await _storage.GetFoldersAsync(request.Feature, cancellationToken);

        foreach (var path in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return path;

            if (request.DelayMs > 0)
                await Task.Delay(request.DelayMs, cancellationToken);
        }
    }
}
