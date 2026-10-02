using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Draws a mark over the icon of assets in the Project window that violate their FileNameRule
    /// or fail attribute validation, and over folders containing such assets:
    /// a red ✗ for errors, a yellow ! for warnings only. Hovering the icon shows the reasons.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectWindowOverlay
    {
        // Mark size relative to the asset icon.
        private const float ListMarkScale = 1f;
        private const float GridMarkScale = 0.7f;

        private static Texture2D _errorMark;
        private static Texture2D _warningMark;

        static ProjectWindowOverlay()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect rect)
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseMove) return;

            var summary = GetSummary(guid);
            if (summary.IsValid) return;

            // Drawn over the icon, centered: as large as the icon in list rows, a bit smaller in the grid
            // so large thumbnails stay recognizable. The white outline keeps it readable on any icon.
            var iconRect = GetIconRect(rect);
            var markScale = rect.height <= 20f ? ListMarkScale : GridMarkScale;
            var markSize = Mathf.Round(iconRect.width * markScale);
            var markRect = new Rect(iconRect.center.x - markSize * 0.5f, iconRect.center.y - markSize * 0.5f, markSize, markSize);

            if (Event.current.type == EventType.Repaint)
            {
                var mark = summary.Severity == ValidationSeverity.Error ? ErrorMark : WarningMark;
                GUI.DrawTexture(markRect, mark, ScaleMode.ScaleToFit, true);
            }

            GUI.Label(iconRect, new GUIContent(string.Empty, summary.Message));
        }

        private static ValidationSummary GetSummary(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return ValidationSummary.Valid;

            var summary = ValidationStatus.GetSummary(guid);
            var counts = ValidationStatus.GetFolderCounts(AssetDatabase.GUIDToAssetPath(guid));
            if (counts.Errors > 0)
            {
                summary = ValidationSummary.Combine(summary, ValidationSummary.Error($"{counts.Errors} asset(s) inside have errors"));
            }

            if (counts.Warnings > 0)
            {
                var message = $"{counts.Warnings} asset(s) inside have warnings";
                summary = ValidationSummary.Combine(summary, new ValidationSummary(ValidationSeverity.Warning, message));
            }

            return summary;
        }

        private static Rect GetIconRect(Rect rect)
        {
            // List view (one line)
            if (rect.height <= 20f) return new Rect(rect.x, rect.y, rect.height, rect.height);

            // Grid view: square icon above the label
            var size = Mathf.Min(rect.width, rect.height - 14f);
            return new Rect(rect.x + (rect.width - size) * 0.5f, rect.y, size, size);
        }

        private static Texture2D ErrorMark
        {
            get
            {
                if (_errorMark == null) _errorMark = CreateErrorMark(64);
                return _errorMark;
            }
        }

        private static Texture2D WarningMark
        {
            get
            {
                if (_warningMark == null) _warningMark = CreateWarningMark(64);
                return _warningMark;
            }
        }

        // Red ✗ with a white outline, generated at runtime so the package has no image assets.
        private static Texture2D CreateErrorMark(int size)
        {
            var margin = size * 0.18f;
            var max = size - 1 - margin;
            return CreateMark(size, new Color(0.9f, 0.15f, 0.15f, 1f), (px, py) => Mathf.Min(
                DistanceToSegment(px, py, margin, margin, max, max),
                DistanceToSegment(px, py, margin, max, max, margin)));
        }

        // Yellow ! with a white outline. Texture rows start at the bottom.
        private static Texture2D CreateWarningMark(int size)
        {
            var center = size * 0.5f;
            var top = size - size * 0.14f;
            var barBottom = size * 0.42f;
            var dotY = size * 0.18f;
            return CreateMark(size, new Color(1f, 0.72f, 0.1f, 1f), (px, py) => Mathf.Min(
                DistanceToSegment(px, py, center, barBottom, center, top),
                DistanceToSegment(px, py, center, dotY, center, dotY)));
        }

        // Fills pixels within the stroke width of the shape, given its distance function.
        // Stroke widths are designed for 32 px and scaled to the texture size.
        private static Texture2D CreateMark(int size, Color fill, System.Func<float, float, float> distance)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var white = Color.white;
            var stroke = 2.6f * size / 32f;
            var outline = 1.6f * size / 32f;
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = distance(x + 0.5f, y + 0.5f);
                    var fillAlpha = Mathf.Clamp01(stroke + 0.5f - d);
                    var outlineAlpha = Mathf.Clamp01(stroke + outline + 0.5f - d);
                    var color = Color.Lerp(white, fill, fillAlpha);
                    color.a = outlineAlpha;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            var abx = bx - ax;
            var aby = by - ay;
            var lengthSq = abx * abx + aby * aby;
            var t = lengthSq > 0f ? Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / lengthSq) : 0f;
            var dx = px - (ax + abx * t);
            var dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
