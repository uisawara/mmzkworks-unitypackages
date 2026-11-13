using System.Linq;
using Mmzkworks.muShortcutPanel.Editor;
using NUnit.Framework;

namespace Mmzkworks.muShortcutPanel.Tests
{
    public sealed class ShortcutPanelDropParserTests
    {
        [TestCase("https://example.com", "https://example.com")]
        [TestCase("  http://example.com/a?b=c  ", "http://example.com/a?b=c")]
        [TestCase("https://example.com/x\r\nSecond line", "https://example.com/x")]
        public void TryGetWebUrl_Accepts(string input, string expected)
        {
            Assert.That(ShortcutPanelDropParser.TryGetWebUrl(input, out var url), Is.True);
            Assert.That(url, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Assets/Foo.prefab")]
        [TestCase("C:/Users/me/file.txt")]
        [TestCase("file:///C:/x.txt")]
        [TestCase("javascript:alert(1)")]
        [TestCase("example.com")]
        public void TryGetWebUrl_Rejects(string input)
        {
            Assert.That(ShortcutPanelDropParser.TryGetWebUrl(input, out _), Is.False);
        }

        [Test]
        public void TryParseInternetShortcut_WindowsUrlFile()
        {
            const string content = "[{000214A0-0000-0000-C000-000000000046}]\r\nProp3=19,11\r\n[InternetShortcut]\r\nIDList=\r\nURL=https://unity.com/\r\n";
            Assert.That(ShortcutPanelDropParser.TryParseInternetShortcut(content, out var url), Is.True);
            Assert.That(url, Is.EqualTo("https://unity.com/"));
        }

        [Test]
        public void TryParseInternetShortcut_Webloc()
        {
            const string content = "<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>URL</key><string>https://unity.com/</string></dict></plist>";
            Assert.That(ShortcutPanelDropParser.TryParseInternetShortcut(content, out var url), Is.True);
            Assert.That(url, Is.EqualTo("https://unity.com/"));
        }

        [Test]
        public void Collect_MixesAssetsUrlsAndShortcutFiles_WithoutDuplicates()
        {
            var entries = ShortcutPanelDropParser.Collect(
                new[] { "Assets/A.prefab", "Assets/Folder" },
                new[]
                {
                    "Assets/A.prefab", // same as object reference
                    "https://example.com/",
                    "C:/Users/me/Desktop/Unity.url",
                    "C:/Users/me/Desktop/notes.txt"
                },
                p => p.StartsWith("Assets/"),
                p => p.EndsWith(".url") ? "[InternetShortcut]\nURL=https://unity.com/\n" : "nothing");

            Assert.That(entries.Select(e => e.ToString()), Is.EqualTo(new[]
            {
                "asset:Assets/A.prefab",
                "asset:Assets/Folder",
                "url:https://example.com/",
                "url:https://unity.com/"
            }));
        }

        [TestCase("https://example.com/", "example.com")]
        [TestCase("https://docs.unity3d.com/Manual/index.html", "docs.unity3d.com/Manual/index.html")]
        [TestCase("not a url", "not a url")]
        public void DefaultUrlTitle(string url, string expected)
        {
            Assert.That(ShortcutPanelDropParser.DefaultUrlTitle(url), Is.EqualTo(expected));
        }
    }
}
