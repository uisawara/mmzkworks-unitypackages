using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Colors for the Tag / Layer label pills in the Hierarchy.
    /// Each Tag / Layer gets its own hue automatically; a HierarchyLabelColorSettings asset overrides them.
    /// </summary>
    public static class HierarchyLabelColors
    {
        internal const string DefaultSettingsPath = "Assets/Settings/muhierarchy/LabelColorSettings.asset";

        // Golden ratio conjugate: consecutive indices land on well-separated hues
        private const float HueStep = 0.61803398875f;
        // Shift tags against layers so Tag #n and Layer #n don't share a hue
        private const float TagHueOffset = 0.33f;

        private const string UntaggedTag = "Untagged";
        private const int DefaultLayer = 0;

        // Auto color parameters used when there is no settings asset
        private const float DefaultSaturation = 0.5f;
        private const float DefaultValue = 0.6f;
        private const float DefaultAlpha = 1f;

        public readonly struct LabelColor
        {
            public readonly Color Background;
            public readonly Color Text;

            public LabelColor(Color background, Color text)
            {
                Background = background;
                Text = text;
            }
        }

        private static HierarchyLabelColorSettings _settings;
        private static bool _settingsLoaded;
        private static readonly Dictionary<string, LabelColor> TagCache = new Dictionary<string, LabelColor>();
        private static readonly Dictionary<int, LabelColor> LayerCache = new Dictionary<int, LabelColor>();

        public static LabelColor GetTagColor(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                tag = UntaggedTag;
            if (TagCache.TryGetValue(tag, out var cached))
                return cached;

            var settings = GetSettings();
            LabelColor color = default;
            bool found = false;
            if (settings != null && settings.tags != null)
            {
                foreach (var entry in settings.tags)
                {
                    if (entry == null || entry.tag != tag)
                        continue;
                    color = new LabelColor(entry.color, entry.overrideTextColor ? entry.textColor : GetContrastTextColor(entry.color));
                    found = true;
                    break;
                }
            }
            if (!found)
                color = CreateAutoColor(settings, tag == UntaggedTag, GetTagHue(tag));

            TagCache[tag] = color;
            return color;
        }

        public static LabelColor GetLayerColor(int layer)
        {
            if (LayerCache.TryGetValue(layer, out var cached))
                return cached;

            var settings = GetSettings();
            LabelColor color = default;
            bool found = false;
            if (settings != null && settings.layers != null)
            {
                foreach (var entry in settings.layers)
                {
                    if (entry == null || entry.layer != layer)
                        continue;
                    color = new LabelColor(entry.color, entry.overrideTextColor ? entry.textColor : GetContrastTextColor(entry.color));
                    found = true;
                    break;
                }
            }
            if (!found)
                color = CreateAutoColor(settings, layer == DefaultLayer, Mathf.Repeat(layer * HueStep, 1f));

            LayerCache[layer] = color;
            return color;
        }

        /// <summary>
        /// Layer name, or "Layer N" for layers without a name in the Tag Manager.
        /// </summary>
        public static string GetLayerDisplayName(int layer)
        {
            string layerName = LayerMask.LayerToName(layer);
            return string.IsNullOrEmpty(layerName) ? $"Layer {layer}" : layerName;
        }

        internal static void Invalidate()
        {
            _settings = null;
            _settingsLoaded = false;
            TagCache.Clear();
            LayerCache.Clear();
        }

        private static HierarchyLabelColorSettings GetSettings()
        {
            // Unity's null check: the asset may have been deleted since it was cached
            if (_settingsLoaded && (_settings != null || ReferenceEquals(_settings, null)))
                return _settings;

            _settingsLoaded = true;
            _settings = null;
            var guids = AssetDatabase.FindAssets("t:" + nameof(HierarchyLabelColorSettings), new[] { "Assets" });
            if (guids.Length == 0)
                return null;

            // Prefer the default location, otherwise the first by path for a stable choice
            var paths = new List<string>(guids.Length);
            foreach (var guid in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            string path = paths.Contains(DefaultSettingsPath) ? DefaultSettingsPath : paths[0];
            _settings = AssetDatabase.LoadAssetAtPath<HierarchyLabelColorSettings>(path);
            return _settings;
        }

        private static float GetTagHue(string tag)
        {
            int index = Array.IndexOf(InternalEditorUtility.tags, tag);
            // Unknown tags (e.g. removed from TagManager) fall back to a name hash
            float seed = index >= 0 ? index : (StableHash(tag) & 0xFFFF);
            return Mathf.Repeat(seed * HueStep + TagHueOffset, 1f);
        }

        private static LabelColor CreateAutoColor(HierarchyLabelColorSettings settings, bool isNeutral, float hue)
        {
            float saturation = settings != null ? settings.saturation : DefaultSaturation;
            float value = settings != null ? settings.value : DefaultValue;
            float alpha = settings != null ? settings.alpha : DefaultAlpha;

            // Untagged / Default are on almost every object; keep them neutral gray
            if (isNeutral)
            {
                saturation = 0f;
                value *= 0.6f;
            }
            var bg = Color.HSVToRGB(hue, Mathf.Clamp01(saturation), Mathf.Clamp01(value));
            bg.a = Mathf.Clamp01(alpha);
            return new LabelColor(bg, GetContrastTextColor(bg));
        }

        private static Color GetContrastTextColor(Color bg)
        {
            float luminance = 0.2126f * bg.r + 0.7152f * bg.g + 0.0722f * bg.b;
            return luminance > 0.5f ? new Color(0.1f, 0.1f, 0.1f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f);
        }

        // string.GetHashCode is not stable across sessions; use FNV-1a
        private static int StableHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in s)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return (int)hash;
            }
        }

        [MenuItem("Tools/muHierarchy/Label Background/Create Color Settings", false, 41)]
        private static void CreateSettings()
        {
            Invalidate();
            var settings = GetSettings();
            if (settings == null)
            {
                // Fill with the current (auto) colors so they can be edited as a starting point
                settings = ScriptableObject.CreateInstance<HierarchyLabelColorSettings>();
                foreach (var tag in InternalEditorUtility.tags)
                    settings.tags.Add(new HierarchyLabelColorSettings.TagColor { tag = tag, color = GetTagColor(tag).Background });
                for (int layer = 0; layer < 32; layer++)
                {
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                        continue;
                    settings.layers.Add(new HierarchyLabelColorSettings.LayerColor { layer = layer, color = GetLayerColor(layer).Background });
                }

                Directory.CreateDirectory(Path.GetDirectoryName(DefaultSettingsPath) ?? string.Empty);
                AssetDatabase.CreateAsset(settings, DefaultSettingsPath);
                AssetDatabase.SaveAssets();
                Invalidate();
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private sealed class SettingsPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                // Settings may be created, deleted or moved; cheap to just reload on any .asset change
                if (HasAsset(imported) || HasAsset(deleted) || HasAsset(moved) || HasAsset(movedFrom))
                {
                    Invalidate();
                    EditorApplication.RepaintHierarchyWindow();
                }
            }

            private static bool HasAsset(string[] paths)
            {
                foreach (var p in paths)
                {
                    if (p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }
    }
}
