using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StorageSharp.Storages
{
    public interface IStorage
    {
        UniTask<string[]> ListAll(CancellationToken cancellationToken = default);
        UniTask<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default);
        UniTask WriteAsync(string key, byte[] data, CancellationToken cancellationToken = default);
        UniTask<Stream> ReadToStreamAsync(string key, CancellationToken cancellationToken = default);
        UniTask WriteAsync(string key, Stream stream, CancellationToken cancellationToken = default);
    }
}