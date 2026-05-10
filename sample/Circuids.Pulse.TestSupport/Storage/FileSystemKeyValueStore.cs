using System.Security.Cryptography;
using System.Text;

namespace Circuids.Pulse.TestSupport.Storage;

public sealed class FileSystemKeyValueStore : IConformanceKeyValueStore
{
    private readonly string _rootDirectory;

    public FileSystemKeyValueStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);
    }

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = PathFor(key);
        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken)
            : null;
    }

    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await File.WriteAllTextAsync(PathFor(key), value, cancellationToken);
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(PathFor(key));
        return ValueTask.CompletedTask;
    }

    private string PathFor(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Path.Combine(_rootDirectory, $"{Convert.ToHexString(bytes)}.txt");
    }
}