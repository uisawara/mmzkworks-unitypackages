using UnityEngine;

namespace Mmzkworks.muProject.Editor
{
    /// <summary>
    /// Chooses a custom icon for a folder in the Project window.
    /// Implementations are found automatically and need a parameterless constructor.
    /// </summary>
    public interface IFolderIconProvider
    {
        /// <summary>
        /// Providers are asked in ascending order. The first non-null icon is used.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Returns the icon for the folder, or null to leave it to other providers.
        /// Results are cached until an asset changes, so this may touch the file system.
        /// </summary>
        /// <param name="folderPath">Project path of the folder, e.g. Assets/Foo or Packages/com.example.foo.</param>
        Texture GetIcon(string folderPath);
    }
}
