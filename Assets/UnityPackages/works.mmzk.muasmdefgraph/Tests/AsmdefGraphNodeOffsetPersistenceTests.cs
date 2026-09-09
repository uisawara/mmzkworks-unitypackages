using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Mmzkworks.muAsmdefgraph;

namespace Mmzkworks.muAsmdefgraph.Tests
{
    public class AsmdefGraphNodeOffsetPersistenceTests
    {
        private const string TestRootKey = "Mmzkworks.muAsmdefgraph.Tests.Root";

        [TearDown]
        public void TearDown()
        {
            var nodeOffsets = new Dictionary<string, Vector2>();
            var lowInterestNodes = new HashSet<string>();
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(TestRootKey, nodeOffsets, lowInterestNodes);
        }

        [Test]
        public void GetRootKey_Null_ReturnsEmpty()
        {
            var result = AsmdefGraphNodeOffsetPersistence.GetRootKey(null);
            Assert.AreEqual("", result);
        }

        [Test]
        public void GetRootKey_EmptySet_ReturnsEmpty()
        {
            var result = AsmdefGraphNodeOffsetPersistence.GetRootKey(new HashSet<string>());
            Assert.AreEqual("", result);
        }

        [Test]
        public void GetRootKey_SingleElement_ReturnsThatElement()
        {
            var roots = new HashSet<string> { "Root.A" };
            var result = AsmdefGraphNodeOffsetPersistence.GetRootKey(roots);
            Assert.AreEqual("Root.A", result);
        }

        [Test]
        public void GetRootKey_MultipleElements_ReturnsSortedJoined()
        {
            var roots = new HashSet<string> { "C", "A", "B" };
            var result = AsmdefGraphNodeOffsetPersistence.GetRootKey(roots);
            Assert.AreEqual("A,B,C", result);
        }

        [Test]
        public void GetRootKey_DifferentOrder_SameResult()
        {
            var roots1 = new HashSet<string> { "X", "Y", "Z" };
            var roots2 = new HashSet<string> { "Z", "X", "Y" };
            var key1 = AsmdefGraphNodeOffsetPersistence.GetRootKey(roots1);
            var key2 = AsmdefGraphNodeOffsetPersistence.GetRootKey(roots2);
            Assert.AreEqual(key1, key2);
            Assert.AreEqual("X,Y,Z", key1);
        }

        [Test]
        public void SaveNodeOffsets_ThenLoadForRoot_RestoresData()
        {
            var toSave = new Dictionary<string, Vector2>
            {
                { "Node1", new Vector2(10f, 20f) },
                { "Node2", new Vector2(-5f, 30f) }
            };
            var toSaveLow = new HashSet<string> { "LowA", "LowB" };
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(TestRootKey, toSave, toSaveLow);

            var loaded = new Dictionary<string, Vector2>();
            var loadedLow = new HashSet<string>();
            AsmdefGraphNodeOffsetPersistence.LoadForRoot(TestRootKey, loaded, loadedLow);

            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual(10f, loaded["Node1"].x);
            Assert.AreEqual(20f, loaded["Node1"].y);
            Assert.AreEqual(-5f, loaded["Node2"].x);
            Assert.AreEqual(30f, loaded["Node2"].y);
            Assert.AreEqual(2, loadedLow.Count);
            Assert.IsTrue(loadedLow.Contains("LowA"));
            Assert.IsTrue(loadedLow.Contains("LowB"));
        }

        [Test]
        public void LoadForRoot_EmptyKey_DoesNotThrow()
        {
            var nodeOffsets = new Dictionary<string, Vector2>();
            var lowInterestNodes = new HashSet<string>();
            AsmdefGraphNodeOffsetPersistence.LoadForRoot("", nodeOffsets, lowInterestNodes);
            Assert.AreEqual(0, nodeOffsets.Count);
            Assert.AreEqual(0, lowInterestNodes.Count);
        }

        [Test]
        public void GetNodeOffsetsFilePath_ReturnsNonEmptyPathUnderProject()
        {
            var path = AsmdefGraphNodeOffsetPersistence.GetNodeOffsetsFilePath();
            Assert.IsFalse(string.IsNullOrEmpty(path));
            Assert.IsTrue(path.Replace('\\', '/').Contains("ProjectSettings"));
            Assert.IsTrue(path.EndsWith(".json"));
        }

        [Test]
        public void SaveNodeOffsets_ThenLoadForRoot_RestoresCommentBlocks()
        {
            var comments = new List<AsmdefGraphCommentBlock>
            {
                new AsmdefGraphCommentBlock
                {
                    Id = "comment-b",
                    Label = "Beta",
                    GraphRect = new Rect(30f, 40f, 120f, 60f),
                    Hue = 0.8f
                },
                new AsmdefGraphCommentBlock
                {
                    Id = "comment-a",
                    Label = "Alpha",
                    GraphRect = new Rect(1f, 2f, 100f, 80f),
                    Hue = 0.2f
                }
            };
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(
                TestRootKey, new Dictionary<string, Vector2>(), new HashSet<string>(), comments);

            var loadedOffsets = new Dictionary<string, Vector2>();
            var loadedLow = new HashSet<string>();
            var loadedComments = new List<AsmdefGraphCommentBlock>();
            AsmdefGraphNodeOffsetPersistence.LoadForRoot(TestRootKey, loadedOffsets, loadedLow, loadedComments);

            Assert.AreEqual(2, loadedComments.Count);
            AsmdefGraphCommentBlock a = null;
            AsmdefGraphCommentBlock b = null;
            foreach (var c in loadedComments)
            {
                if (c.Id == "comment-a") a = c;
                if (c.Id == "comment-b") b = c;
            }
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual("Alpha", a.Label);
            Assert.AreEqual(1f, a.GraphRect.x);
            Assert.AreEqual(2f, a.GraphRect.y);
            Assert.AreEqual(100f, a.GraphRect.width);
            Assert.AreEqual(80f, a.GraphRect.height);
            Assert.AreEqual("Beta", b.Label);
            Assert.AreEqual(30f, b.GraphRect.x);
            Assert.AreEqual(40f, b.GraphRect.y);
            Assert.AreEqual(0.2f, a.Hue, 0.0001f);
            Assert.AreEqual(0.8f, b.Hue, 0.0001f);
        }

        [Test]
        public void SaveNodeOffsets_CommentBlocksWrittenInSortedIdOrder()
        {
            var comments = new List<AsmdefGraphCommentBlock>
            {
                new AsmdefGraphCommentBlock { Id = "z-id", Label = "Z", GraphRect = new Rect(0, 0, 80, 40) },
                new AsmdefGraphCommentBlock { Id = "a-id", Label = "A", GraphRect = new Rect(0, 0, 80, 40) },
                new AsmdefGraphCommentBlock { Id = "m-id", Label = "M", GraphRect = new Rect(0, 0, 80, 40) }
            };
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(
                TestRootKey, new Dictionary<string, Vector2>(), new HashSet<string>(), comments);
            var path = AsmdefGraphNodeOffsetPersistence.GetNodeOffsetsFilePath();
            var json = File.ReadAllText(path);
            var file = JsonUtility.FromJson<RootSaveFile>(json);
            Assert.IsNotNull(file?.roots);
            RootSaveData root = null;
            foreach (var r in file.roots)
            {
                if (r != null && r.rootKey == TestRootKey) { root = r; break; }
            }
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.commentBlocks);
            Assert.AreEqual(3, root.commentBlocks.Length);
            Assert.AreEqual("a-id", root.commentBlocks[0].id);
            Assert.AreEqual("m-id", root.commentBlocks[1].id);
            Assert.AreEqual("z-id", root.commentBlocks[2].id);
        }

        [Test]
        public void SaveNodeOffsets_WritesFormatVersionOne()
        {
            var toSave = new Dictionary<string, Vector2> { { "Any", new Vector2(0, 0) } };
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(TestRootKey, toSave, new HashSet<string>());
            var path = AsmdefGraphNodeOffsetPersistence.GetNodeOffsetsFilePath();
            Assert.IsTrue(File.Exists(path));
            var json = File.ReadAllText(path);
            var file = JsonUtility.FromJson<RootSaveFile>(json);
            Assert.IsNotNull(file);
            Assert.AreEqual(RootSaveFile.CurrentFormatVersion, file.formatVersion);
            Assert.AreEqual(2, file.formatVersion);
        }

        [Test]
        public void SaveNodeOffsets_EntriesWrittenInSortedOrder()
        {
            var toSave = new Dictionary<string, Vector2>
            {
                { "Z.Assembly", new Vector2(1, 2) },
                { "A.Assembly", new Vector2(3, 4) },
                { "M.Assembly", new Vector2(5, 6) }
            };
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(TestRootKey, toSave, new HashSet<string>());
            var path = AsmdefGraphNodeOffsetPersistence.GetNodeOffsetsFilePath();
            var json = File.ReadAllText(path);
            var file = JsonUtility.FromJson<RootSaveFile>(json);
            Assert.IsNotNull(file?.roots);
            RootSaveData root = null;
            foreach (var r in file.roots)
            {
                if (r != null && r.rootKey == TestRootKey) { root = r; break; }
            }
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.entries);
            Assert.AreEqual(3, root.entries.Length);
            Assert.AreEqual("A.Assembly", root.entries[0].name);
            Assert.AreEqual("M.Assembly", root.entries[1].name);
            Assert.AreEqual("Z.Assembly", root.entries[2].name);
        }

        [Test]
        public void SaveNodeOffsets_LowInterestNodesWrittenInSortedOrder()
        {
            var toSaveLow = new HashSet<string> { "zNode", "aNode", "mNode" };
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(TestRootKey, new Dictionary<string, Vector2>(), toSaveLow);
            var path = AsmdefGraphNodeOffsetPersistence.GetNodeOffsetsFilePath();
            var json = File.ReadAllText(path);
            var file = JsonUtility.FromJson<RootSaveFile>(json);
            Assert.IsNotNull(file?.roots);
            RootSaveData root = null;
            foreach (var r in file.roots)
            {
                if (r != null && r.rootKey == TestRootKey) { root = r; break; }
            }
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.lowInterestNodes);
            Assert.AreEqual(3, root.lowInterestNodes.Length);
            Assert.AreEqual("aNode", root.lowInterestNodes[0]);
            Assert.AreEqual("mNode", root.lowInterestNodes[1]);
            Assert.AreEqual("zNode", root.lowInterestNodes[2]);
        }
    }
}
