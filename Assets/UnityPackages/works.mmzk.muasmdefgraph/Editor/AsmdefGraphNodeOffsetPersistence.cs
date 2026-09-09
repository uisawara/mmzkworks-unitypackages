using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Load/save node offsets, low-interest nodes and comment blocks per root key.</summary>
    public static class AsmdefGraphNodeOffsetPersistence
    {
        private const string NodeOffsetsFileName = "ProjectSettings/muAsmdefgraph.json";

        public static string GetRootKey(HashSet<string> roots)
        {
            if (roots == null || roots.Count == 0)
                return "";
            var list = new List<string>(roots);
            list.Sort(StringComparer.Ordinal);
            return string.Join(",", list);
        }

        public static string GetNodeOffsetsFilePath()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? "", NodeOffsetsFileName);
        }

        /// <summary>Load offsets, low-interest nodes and comment blocks for the given root key. Fills the provided collections.</summary>
        public static void LoadForRoot(
            string rootKey,
            Dictionary<string, Vector2> nodeOffsets,
            HashSet<string> lowInterestNodes,
            List<AsmdefGraphCommentBlock> commentBlocks = null,
            CommentColorState commentColor = null)
        {
            if (string.IsNullOrEmpty(rootKey) || nodeOffsets == null || lowInterestNodes == null)
                return;
            nodeOffsets.Clear();
            lowInterestNodes.Clear();
            commentBlocks?.Clear();
            if (commentColor != null)
            {
                commentColor.HasColor = false;
                commentColor.Hue = AsmdefGraphCommentPalette.DefaultHue;
            }
            var path = GetNodeOffsetsFilePath();
            if (!File.Exists(path))
                return;
            try
            {
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<RootSaveFile>(json);
                if (file?.roots == null)
                    return;
                foreach (var rootData in file.roots)
                {
                    if (rootData == null || rootData.rootKey != rootKey)
                        continue;
                    if (rootData.entries != null)
                    {
                        foreach (var e in rootData.entries)
                        {
                            if (string.IsNullOrEmpty(e.name))
                                continue;
                            nodeOffsets[e.name] = new Vector2(e.x, e.y);
                        }
                    }
                    if (rootData.lowInterestNodes != null)
                    {
                        foreach (var name in rootData.lowInterestNodes)
                        {
                            if (!string.IsNullOrEmpty(name))
                                lowInterestNodes.Add(name);
                        }
                    }
                    if (commentBlocks != null && rootData.commentBlocks != null)
                    {
                        foreach (var entry in rootData.commentBlocks)
                        {
                            var block = AsmdefGraphCommentBlock.FromEntry(entry);
                            if (block != null)
                                commentBlocks.Add(block);
                        }
                    }
                    if (commentColor != null)
                    {
                        commentColor.HasColor = rootData.hasCommentColor;
                        commentColor.Hue = rootData.hasCommentColor
                            ? rootData.commentHue
                            : AsmdefGraphCommentPalette.DefaultHue;
                    }
                    break;
                }
            }
            catch { }
        }

        public static void SaveNodeOffsets(
            string rootKey,
            Dictionary<string, Vector2> nodeOffsets,
            HashSet<string> lowInterestNodes,
            List<AsmdefGraphCommentBlock> commentBlocks = null,
            CommentColorState commentColor = null)
        {
            if (string.IsNullOrEmpty(rootKey) || nodeOffsets == null)
                return;
            var path = GetNodeOffsetsFilePath();
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var allRoots = new List<RootSaveData>();
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var file = JsonUtility.FromJson<RootSaveFile>(json);
                        if (file?.roots != null)
                        {
                            foreach (var r in file.roots)
                            {
                                if (r != null && r.rootKey != rootKey)
                                    allRoots.Add(r);
                            }
                        }
                    }
                    catch { }
                }

                var entriesList = new List<NodeOffsetEntry>(nodeOffsets.Count);
                foreach (var kv in nodeOffsets)
                    entriesList.Add(new NodeOffsetEntry { name = kv.Key, x = kv.Value.x, y = kv.Value.y });
                entriesList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                var lowList = lowInterestNodes != null ? new List<string>(lowInterestNodes) : new List<string>();
                lowList.Sort(StringComparer.Ordinal);

                var commentList = new List<CommentBlockEntry>();
                if (commentBlocks != null)
                {
                    foreach (var block in commentBlocks)
                    {
                        if (block == null || string.IsNullOrEmpty(block.Id))
                            continue;
                        commentList.Add(block.ToEntry());
                    }
                    commentList.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
                }

                allRoots.Add(new RootSaveData
                {
                    rootKey = rootKey,
                    entries = entriesList.ToArray(),
                    lowInterestNodes = lowList.ToArray(),
                    commentBlocks = commentList.ToArray(),
                    hasCommentColor = commentColor != null && commentColor.HasColor,
                    commentHue = commentColor != null ? commentColor.Hue : AsmdefGraphCommentPalette.DefaultHue
                });
                var fileToSave = new RootSaveFile { formatVersion = RootSaveFile.CurrentFormatVersion, roots = allRoots.ToArray() };
                var jsonToWrite = JsonUtility.ToJson(fileToSave, true);
                File.WriteAllText(path, jsonToWrite);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AsmdefGraph] Failed to save node offsets: {ex.Message}");
            }
        }
    }
}
