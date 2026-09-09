using System;
using System.Collections.Generic;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Raw asmdef data read from file.</summary>
    public sealed class AsmdefRawData
    {
        public string Guid;
        public string Path;
        public string Name;
        public string PackageName;
        public string DisplayName;
        public List<string> RawReferences = new List<string>();
        public string Json;
    }

    /// <summary>Normalized asmdef info (name, references, precompiled refs).</summary>
    public sealed class AsmdefInfo
    {
        public string Name;
        public List<string> References = new List<string>();
        public List<string> PrecompiledReferences = new List<string>();
    }

    [Serializable]
    public sealed class NodeOffsetEntry
    {
        public string name;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class NodeOffsetsSave
    {
        public NodeOffsetEntry[] entries;
    }

    [Serializable]
    public sealed class CommentBlockEntry
    {
        public string id;
        public string label;
        public float x;
        public float y;
        public float width;
        public float height;
        public float hue;
        public bool hasColor;
    }

    [Serializable]
    public sealed class RootSaveData
    {
        public string rootKey;
        public NodeOffsetEntry[] entries;
        public string[] lowInterestNodes;
        public CommentBlockEntry[] commentBlocks;
        public bool hasCommentColor;
        public float commentHue;
    }

    [Serializable]
    public sealed class RootSaveFile
    {
        public const int CurrentFormatVersion = 2;
        public int formatVersion;
        public RootSaveData[] roots;
    }
}
