namespace PoC.Improved.Application.Providers;

/// <summary>
/// Abstraction over the storage backend. Lives in Application so handlers can depend
/// on it without pulling in Infrastructure (Clean Architecture dependency rule).
/// </summary>
public interface IStorageProvider
{
    Task<List<string>> GetFoldersAsync(string feature, CancellationToken ct = default);
    Task<string> GetFileUrlAsync(string filePath, CancellationToken ct = default);
}
