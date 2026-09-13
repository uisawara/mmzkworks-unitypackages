using System;
using Cysharp.Threading.Tasks;

namespace StorageSharp.Packs
{
    public interface IPacks
    {
        UniTask Clear();
        UniTask<ArchiveScheme[]> ListAll();
        UniTask<ArchiveScheme> Add(string directoryPath);
        UniTask Delete(ArchiveScheme scheme);
        UniTask<string> Load(ArchiveScheme archiveScheme);
        UniTask Unload(ArchiveScheme archiveScheme);

        public class ArchiveScheme
        {
            public ArchiveScheme(string directoryPath)
            {
                DirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            }

            public string DirectoryPath { get; }
        }
    }
}