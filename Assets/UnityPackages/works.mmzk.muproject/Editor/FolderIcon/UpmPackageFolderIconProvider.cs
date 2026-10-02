using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muProject.Editor
{
    /// <summary>
    /// Shows a package icon for folders containing package.json (UPM package roots).
    /// </summary>
    public class UpmPackageFolderIconProvider : IFolderIconProvider
    {
        private static Texture _icon;

        public int Order => 0;

        public Texture GetIcon(string folderPath)
        {
            return File.Exists(Path.Combine(folderPath, "package.json")) ? Icon : null;
        }

        private static Texture Icon
        {
            get
            {
                if (_icon != null) return _icon;

                var name = EditorGUIUtility.isProSkin ? "d_package manager" : "package manager";
                _icon = EditorGUIUtility.Load($"icons/{name}@2x.png") as Texture
                        ?? EditorGUIUtility.Load($"icons/{name}.png") as Texture;
                return _icon;
            }
        }
    }
}
