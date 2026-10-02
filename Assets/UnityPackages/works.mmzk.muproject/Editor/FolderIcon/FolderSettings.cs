using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muProject.Editor
{
    /// <summary>
    /// Project window settings for folders, matched by path.
    /// Applies only to the folder this asset is in and the folders below it.
    /// Any number of these assets can exist in the project; the one in the deepest folder is used first.
    /// </summary>
    [CreateAssetMenu(menuName = "muProject/Folder Settings", fileName = "FolderSettings")]
    public class FolderSettings : ScriptableObject
    {
        [Tooltip("Rules are checked from top to bottom. The first rule whose pattern matches the folder path is used.")]
        public List<FolderIconRule> folderIcons = new List<FolderIconRule>();

        private void OnValidate()
        {
            FolderIconDrawer.Invalidate();
        }
    }

    [Serializable]
    public class FolderIconRule
    {
        [Tooltip("Regular expression matched against the folder's project path, e.g. ^Assets/Scenes$ or /Editor$")]
        public string pathPattern;

        public Texture2D icon;
    }
}
