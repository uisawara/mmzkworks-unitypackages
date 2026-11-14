using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Mmzkworks.muShortcutPanel.Editor
{
    internal readonly struct DropEntry
    {
        public readonly string Kind;
        public readonly string Value; // asset path or URL

        public DropEntry(string kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        public override string ToString() => $"{Kind}:{Value}";
    }

    /// <summary>
    /// Turns the contents of a drag and drop into shortcut entries.
    /// Handles Project window assets, URLs (as text) and internet shortcut files (.url / .webloc).
    /// </summary>
    internal static class ShortcutPanelDropParser
    {
        /// <summary>Drop entry kind for an image file outside the project (copied into it on drop).</summary>
        public const string ExternalImage = "external-image";

        private static readonly string[] ImageExtensions =
            { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".psd", ".tif", ".tiff", ".exr", ".hdr" };

        public static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path ?? "");
            return Array.Exists(ImageExtensions, e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
        }

        private static readonly Regex UrlLine = new Regex(@"^\s*URL\s*=\s*(?<url>\S+)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex WeblocString = new Regex(@"<string>\s*(?<url>[^<\s]+)\s*</string>",
            RegexOptions.IgnoreCase);

        /// <summary>True for absolute http(s) URLs. Returns the trimmed URL (first line only).</summary>
        public static bool TryGetWebUrl(string text, out string url)
        {
            url = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var first = text.Trim();
            var nl = first.IndexOfAny(new[] { '\r', '\n' });
            if (nl >= 0) first = first.Substring(0, nl).Trim();

            if (!Uri.TryCreate(first, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

            url = first;
            return true;
        }

        /// <summary>Reads the URL out of a Windows .url (INI) or macOS .webloc (plist) file's text.</summary>
        public static bool TryParseInternetShortcut(string content, out string url)
        {
            url = null;
            if (string.IsNullOrEmpty(content)) return false;

            var m = UrlLine.Match(content);
            if (!m.Success) m = WeblocString.Match(content);
            return m.Success && TryGetWebUrl(m.Groups["url"].Value, out url);
        }

        /// <param name="objectAssetPaths">Asset paths of DragAndDrop.objectReferences (empty for non-assets).</param>
        /// <param name="paths">DragAndDrop.paths</param>
        /// <param name="isProjectAsset">Whether a path is an asset in this project.</param>
        /// <param name="readExternalFile">Reads an external file's text, or returns null.</param>
        /// <param name="assetImagesAsPictures">
        /// True (Alt held): image assets from the project become <see cref="ShortcutItemKind.Image"/> entries instead of shortcuts.
        /// Image files from outside the project always become <see cref="ExternalImage"/> entries.
        /// </param>
        public static List<DropEntry> Collect(
            IEnumerable<string> objectAssetPaths,
            IEnumerable<string> paths,
            Func<string, bool> isProjectAsset,
            Func<string, string> readExternalFile,
            bool assetImagesAsPictures = false)
        {
            var result = new List<DropEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(string kind, string value)
            {
                if (seen.Add(kind + "|" + value)) result.Add(new DropEntry(kind, value));
            }

            string AssetKind(string p) =>
                assetImagesAsPictures && IsImageFile(p) ? ShortcutItemKind.Image : ShortcutItemKind.Asset;

            if (objectAssetPaths != null)
            {
                foreach (var p in objectAssetPaths)
                {
                    if (!string.IsNullOrEmpty(p)) Add(AssetKind(p), p);
                }
            }

            if (paths != null)
            {
                foreach (var p in paths)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;

                    if (TryGetWebUrl(p, out var url))
                    {
                        Add(ShortcutItemKind.Url, url);
                    }
                    else if (isProjectAsset != null && isProjectAsset(p))
                    {
                        Add(AssetKind(p), p);
                    }
                    else if (IsImageFile(p) && Path.IsPathRooted(p))
                    {
                        Add(ExternalImage, p);
                    }
                    else if (IsInternetShortcutFile(p) && readExternalFile != null
                             && TryParseInternetShortcut(readExternalFile(p), out var fileUrl))
                    {
                        Add(ShortcutItemKind.Url, fileUrl);
                    }
                }
            }

            return result;
        }

        private static bool IsInternetShortcutFile(string path)
        {
            var ext = Path.GetExtension(path);
            return string.Equals(ext, ".url", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(ext, ".webloc", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Host part used as the default caption of URL shortcuts.</summary>
        public static string DefaultUrlTitle(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            {
                var path = uri.AbsolutePath.TrimEnd('/');
                return string.IsNullOrEmpty(path) ? uri.Host : uri.Host + path;
            }
            return url ?? "";
        }
    }
}
