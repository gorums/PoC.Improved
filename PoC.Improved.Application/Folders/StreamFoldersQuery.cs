using PoC.Improved.Application.Cqrs;

namespace PoC.Improved.Application.Folders;

/// <summary>
/// Streams folder paths one at a time. Useful when the upstream returns large lists
/// or when you want to push items to the client as they arrive (SSE, NDJSON, etc.).
/// </summary>
public sealed record StreamFoldersQuery(string Feature, int DelayMs = 200)
    : IStreamRequest<string>;
