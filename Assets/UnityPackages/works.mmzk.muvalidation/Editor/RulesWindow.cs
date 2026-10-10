using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Lists every configured rule and exports the same JSON as <see cref="ValidationCommandLine.ExportRules"/>.
    /// Click a row to ping its asset or script. Double-click to select it. The bottom shows that row's JSON.
    /// </summary>
    public class RulesWindow : EditorWindow
    {
        private const float RowHeight = 20f;
        private const float DetailHeight = 160f;

        private sealed class Row
        {
            public int Parent = -1;
            public int Indent;
            public string Label;
            public string Detail;
            public string AssetPath;
            public Type ScriptType;
            public bool Header;
        }

        [SerializeField] private bool showFileNames = true;
        [SerializeField] private bool showScenes = true;
        [SerializeField] private bool showAssignments = true;
        [SerializeField] private bool showAttributes = true;
        [SerializeField] private string search = "";

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Row> _visible = new List<Row>();
        private readonly Dictionary<Type, string> _scriptPaths = new Dictionary<Type, string>();

        private RulesCatalog _catalog;
        private Row _selected;
        private Vector2 _scroll;
        private Vector2 _detailScroll;

        [MenuItem("Tools/muValidation/Rules", false, 2002)]
        public static void Open()
        {
            GetWindow<RulesWindow>("Rules").Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(720, 320);
            EditorApplication.delayCall += RefreshWhenReady;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RefreshWhenReady;
        }

        private void RefreshWhenReady()
        {
            if (this != null) Refresh();
        }

        private void Refresh()
        {
            if (this == null) return;
            LoadCatalog();
            RebuildRows();
        }

        private void LoadCatalog()
        {
            try
            {
                EditorUtility.DisplayProgressBar("muValidation", "Collecting rules...", 0.5f);
                _catalog = RulesCatalog.Collect();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void Export()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            var path = EditorUtility.SaveFilePanel("Export muValidation Rules", root, "muvalidation-rules", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                LoadCatalog();
                _catalog.Write(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[muValidation] Failed to export rules: {e.Message}");
                EditorUtility.DisplayDialog("muValidation", "Could not export rules.\n" + e.Message, "OK");
                return;
            }

            RebuildRows();
            Debug.Log($"[muValidation] Exported rules to {path}");
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawList(new Rect(0, EditorStyles.toolbar.fixedHeight, position.width,
                position.height - EditorStyles.toolbar.fixedHeight - DetailHeight));
            DrawDetail(new Rect(0, position.height - DetailHeight, position.width, DetailHeight));
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
                if (GUILayout.Button("Export...", EditorStyles.toolbarButton, GUILayout.Width(70))) Export();

                EditorGUI.BeginChangeCheck();
                showFileNames = GUILayout.Toggle(showFileNames, "File Names", EditorStyles.toolbarButton, GUILayout.Width(80));
                showScenes = GUILayout.Toggle(showScenes, "Scene Rules", EditorStyles.toolbarButton, GUILayout.Width(85));
                showAssignments = GUILayout.Toggle(showAssignments, "Assignments", EditorStyles.toolbarButton, GUILayout.Width(90));
                showAttributes = GUILayout.Toggle(showAttributes, "Attributes", EditorStyles.toolbarButton, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck()) RebuildRows();

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MaxWidth(250));
                if (EditorGUI.EndChangeCheck()) ApplyFilter();
            }
        }

        private void RebuildRows()
        {
            _rows.Clear();
            _selected = null;
            if (_catalog != null)
            {
                if (showFileNames) AddFileNames();
                if (showScenes) AddScenes();
                if (showAssignments) AddAssignments();
                if (showAttributes) AddAttributes();
            }

            ApplyFilter();
        }

        private void AddFileNames()
        {
            var header = AddRow(-1, 0, $"File Name Rules ({_catalog.FileNames.Length})",
                "Each FileNameRules asset: folders and the file name patterns allowed there.", null, null, true);
            foreach (var asset in _catalog.FileNames)
            {
                var assetRow = AddRow(header, 1, FileNameLabel(asset), asset.Json, asset.Path, null, false);
                foreach (var rule in asset.Rules)
                {
                    AddRow(assetRow, 2, FileRuleLabel(rule), rule.Json, asset.Path, null, false);
                }
            }
        }

        private static string FileNameLabel(RulesCatalog.FileNameRuleSet asset)
        {
            var notes = new List<string>();
            if (!asset.AllowOtherFiles) notes.Add("other files not allowed");
            if (asset.ValidateFolders) notes.Add("folders too");
            return notes.Count == 0 ? asset.Path : asset.Path + "  " + string.Join(", ", notes);
        }

        private static string FileRuleLabel(RulesCatalog.FileNamePatternRule rule)
        {
            var patterns = rule.Patterns.Length == 0
                ? "(any file)"
                : string.Join(" | ", rule.Patterns.Select(pattern => pattern.Valid ? pattern.Pattern : pattern.Pattern + " (invalid)"));
            var path = string.IsNullOrEmpty(rule.Path) ? "(this folder)" : rule.Path;
            var label = path + "  " + patterns;
            if (rule.Folder == null) label += "  (outside the project)";
            else if (!rule.FolderExists) label += "  (missing folder)";
            return label;
        }

        private void AddScenes()
        {
            var header = AddRow(-1, 0, $"Scene Rules ({_catalog.Scenes.Length})",
                "Each SceneRules asset, including ones no assignment applies. assignedTo is where it is used.", null, null, true);
            foreach (var asset in _catalog.Scenes)
            {
                var assetRow = AddRow(header, 1, SceneLabel(asset), asset.Json, asset.Path, null, false);
                if (asset.DefaultNames.Length > 0)
                {
                    var forbidden = asset.DefaultNames.Count(entry => !entry.Allowed && !string.IsNullOrEmpty(entry.Name));
                    var allowed = asset.DefaultNames.Count(entry => entry.Allowed);
                    AddRow(assetRow, 2, $"Default names: {forbidden} forbidden, {allowed} allowed", asset.DefaultNamesJson(), asset.Path, null, false);
                }

                if (asset.ForbiddenTags.Length > 0)
                {
                    AddRow(assetRow, 2, "Forbidden tags: " + string.Join(", ", asset.ForbiddenTags), asset.ForbiddenTagsJson(), asset.Path, null, false);
                }

                if (asset.ForbiddenLayers.Length > 0)
                {
                    AddRow(assetRow, 2, "Forbidden layers: " + string.Join(", ", asset.ForbiddenLayers), asset.ForbiddenLayersJson(), asset.Path, null, false);
                }

                foreach (var rule in asset.TagLayers)
                {
                    var layers = rule.AllowedLayers.Length == 0 ? "(none)" : string.Join(", ", rule.AllowedLayers);
                    var tag = string.IsNullOrEmpty(rule.Tag) ? "(none)" : rule.Tag;
                    AddRow(assetRow, 2, $"Tag {tag}  →  {layers}", rule.Json, asset.Path, null, false);
                }

                foreach (var rule in asset.ComponentLayers)
                {
                    AddRow(assetRow, 2, ComponentLayerLabel(rule), rule.Json, asset.Path, null, false);
                }

                AddFlag(assetRow, asset.Path, asset.RequirePrefabInstance, "Require prefab instance", "requirePrefabInstance");
                AddFlag(assetRow, asset.Path, asset.UniqueNames, "Unique root names", "uniqueNames");
                AddFlag(assetRow, asset.Path, asset.MatchPrefabNames, "Match prefab names", "matchPrefabNames");
                AddFlag(assetRow, asset.Path, asset.NoReferencesToParents, "No references to parents", "noReferencesToParents");
            }
        }

        private static string SceneLabel(RulesCatalog.SceneRuleSet asset)
        {
            var assigned = asset.AssignedTo.Length == 0 ? "not assigned" : string.Join(", ", asset.AssignedTo);
            var label = $"{asset.Path}  {asset.Severity}  {assigned}";
            if (asset.Severity == ValidationSeverity.None.ToString()) label += "  (disabled)";
            else if (!asset.HasRules) label += "  (no checks)";
            return label;
        }

        private static string ComponentLayerLabel(RulesCatalog.ComponentLayerEntry rule)
        {
            var typeName = ShortName(rule.ComponentType);
            if (rule.IncludeSubclasses && !string.IsNullOrEmpty(rule.ComponentType)) typeName += " + subclasses";
            var layers = rule.AllowedLayers.Length == 0 ? "(none)" : string.Join(", ", rule.AllowedLayers);
            var label = $"{typeName}  →  {layers}";
            if (!rule.Resolved) label += "  (type not found)";
            return label;
        }

        private void AddFlag(int parent, string assetPath, bool enabled, string label, string property)
        {
            if (!enabled) return;
            AddRow(parent, 2, label, "{\n  \"" + property + "\": true\n}\n", assetPath, null, false);
        }

        private void AddAssignments()
        {
            var header = AddRow(-1, 0, $"Scene Rules Assignments ({_catalog.Assignments.Length})",
                "Each SceneRulesAssignments asset: which SceneRules apply under a folder or scene path.", null, null, true);
            foreach (var asset in _catalog.Assignments)
            {
                var assetRow = AddRow(header, 1, asset.Path, asset.Json, asset.Path, null, false);
                foreach (var entry in asset.Assignments)
                {
                    AddRow(assetRow, 2, AssignmentLabel(entry), entry.Json, asset.Path, null, false);
                }
            }
        }

        private static string AssignmentLabel(RulesCatalog.AssignmentEntry entry)
        {
            var rules = entry.Rules.Length == 0 ? "(none)" : string.Join(", ", entry.Rules);
            var label = entry.Path + "  " + rules;
            if (!entry.Exists) label += "  (missing path)";
            if (entry.MissingRules > 0) label += $"  ({entry.MissingRules} missing)";
            return label;
        }

        private void AddAttributes()
        {
            var header = AddRow(-1, 0, $"Validation Attributes ({_catalog.Attributes.Length})",
                "Attributes declared on Component and ScriptableObject types. Inherited attributes are listed on the type that declares them.",
                null, null, true);
            foreach (var type in _catalog.Attributes)
            {
                var typeRow = AddRow(header, 1, type.TypeName, type.Json, null, type.ScriptType, false);
                AddAttributeRules(typeRow, 2, type.Attributes, type.ScriptType);
            }
        }

        private void AddAttributeRules(int parent, int indent, RulesCatalog.AttributeRule[] rules, Type scriptType)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                var row = AddRow(parent, indent, AttributeLabel(rule), rule.Json, null, scriptType, false);
                if (rule.Children != null && rule.Children.Length > 0) AddAttributeRules(row, indent + 1, rule.Children, scriptType);
            }
        }

        private static string AttributeLabel(RulesCatalog.AttributeRule rule)
        {
            var label = rule.ShortName;
            if (!string.IsNullOrEmpty(rule.Member)) label = rule.Member + "  " + label;
            if (rule.Properties != null && rule.Properties.Length > 0)
            {
                label += "  " + string.Join(", ", rule.Properties.Select(property => property.Name + "=" + FormatProperty(property.Value)));
            }

            if (!string.IsNullOrEmpty(rule.Error)) label += "  (error)";
            if (!string.IsNullOrEmpty(rule.Severity) && rule.Severity != ValidationSeverity.Error.ToString()) label += "  [" + rule.Severity + "]";
            return label;
        }

        private static string FormatProperty(object value)
        {
            if (value == null) return "null";
            if (value is string text) return text;
            if (value is bool flag) return flag ? "true" : "false";
            if (value is object[] items) return string.Join(", ", items.Select(FormatProperty));
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string ShortName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "(none)";
            var dot = fullName.LastIndexOf('.');
            return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
        }

        private int AddRow(int parent, int indent, string label, string detail, string assetPath, Type scriptType, bool header)
        {
            var index = _rows.Count;
            _rows.Add(new Row
            {
                Parent = parent,
                Indent = indent,
                Label = label ?? "",
                Detail = detail ?? "",
                AssetPath = assetPath,
                ScriptType = scriptType,
                Header = header,
            });
            return index;
        }

        private void ApplyFilter()
        {
            _visible.Clear();
            if (string.IsNullOrEmpty(search))
            {
                _visible.AddRange(_rows);
            }
            else
            {
                var visible = new bool[_rows.Count];
                for (var i = 0; i < _rows.Count; i++)
                {
                    if (!SelfOrAncestorMatches(i)) continue;
                    visible[i] = true;
                    for (var parent = _rows[i].Parent; parent >= 0; parent = _rows[parent].Parent) visible[parent] = true;
                }

                for (var i = 0; i < _rows.Count; i++)
                {
                    if (visible[i]) _visible.Add(_rows[i]);
                }
            }

            if (_selected != null && !_visible.Contains(_selected)) _selected = null;
            Repaint();
        }

        private bool SelfOrAncestorMatches(int index)
        {
            for (var current = index; current >= 0; current = _rows[current].Parent)
            {
                var row = _rows[current];
                if (!row.Header && (Contains(row.Label) || Contains(row.Detail))) return true;
            }

            return false;
        }

        private bool Contains(string text)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawList(Rect rect)
        {
            if (_visible.Count == 0)
            {
                var message = _rows.Count == 0 ? "No rules to show." : "No rules match the search.";
                GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), message, EditorStyles.miniLabel);
                return;
            }

            var contentWidth = Mathf.Max(rect.width - 16f, 200f);
            var content = new Rect(0, 0, contentWidth, _visible.Count * RowHeight);
            _scroll = GUI.BeginScrollView(rect, _scroll, content);

            var first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowHeight));
            var last = Mathf.Min(_visible.Count - 1, Mathf.CeilToInt((_scroll.y + rect.height) / RowHeight));
            for (var i = first; i <= last; i++)
            {
                var row = _visible[i];
                var rowRect = new Rect(0, i * RowHeight, content.width, RowHeight);
                if (Event.current.type == EventType.Repaint)
                {
                    if (row == _selected) EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.9f, 0.4f));
                    else if (i % 2 == 1) EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.5f, 0.5f, 0.06f));
                }

                var labelRect = new Rect(8 + row.Indent * 16f, rowRect.y, Mathf.Max(1f, content.width - 12f - row.Indent * 16f), RowHeight);
                GUI.Label(labelRect, new GUIContent(row.Label, row.Label), row.Header ? EditorStyles.boldLabel : EditorStyles.label);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selected = row;
                    _detailScroll = Vector2.zero;
                    OpenRow(row, Event.current.clickCount >= 2);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawDetail(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), new Color(0f, 0f, 0f, 0.3f));
            var inner = new Rect(rect.x + 6, rect.y + 4, rect.width - 12, rect.height - 8);
            if (_selected == null)
            {
                GUI.Label(inner, "Select a row.", EditorStyles.miniLabel);
                return;
            }

            var style = EditorStyles.wordWrappedLabel;
            var width = Mathf.Max(1f, inner.width - 16f);
            var height = Mathf.Max(inner.height, style.CalcHeight(new GUIContent(_selected.Detail ?? ""), width));
            _detailScroll = GUI.BeginScrollView(inner, _detailScroll, new Rect(0, 0, width, height));
            EditorGUI.SelectableLabel(new Rect(0, 0, width, height), _selected.Detail ?? "", style);
            GUI.EndScrollView();
        }

        private void OpenRow(Row row, bool open)
        {
            var path = row.AssetPath;
            if (string.IsNullOrEmpty(path)) path = FindScriptPath(row.ScriptType);
            if (string.IsNullOrEmpty(path)) return;

            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) return;
            if (!open)
            {
                EditorGUIUtility.PingObject(asset);
                return;
            }

            Selection.activeObject = asset;
            AssetDatabase.OpenAsset(asset);
        }

        private string FindScriptPath(Type type)
        {
            if (type == null) return null;
            var owner = type;
            while (owner.DeclaringType != null) owner = owner.DeclaringType;
            if (_scriptPaths.TryGetValue(owner, out var cached)) return cached;

            string found = null;
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript " + owner.Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == owner)
                {
                    found = path;
                    break;
                }
            }

            _scriptPaths[owner] = found;
            return found;
        }
    }
}
