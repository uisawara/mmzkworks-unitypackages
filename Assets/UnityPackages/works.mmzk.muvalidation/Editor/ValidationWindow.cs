using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Lists all validation results: assets (FileNameRules and validation attributes) and
    /// GameObjects in open scenes / Prefab Mode. Click a row to ping it, double-click to select it.
    /// Also shows the issues that stopped a build (see <see cref="ShowIssues"/>).
    /// </summary>
    public class ValidationWindow : EditorWindow
    {
        private const float RowHeight = 20f;
        private const float DetailHeight = 90f;

        private sealed class Row
        {
            public ValidationIssue Issue;
            public string FirstLine;

            public ValidationSeverity Severity => Issue.Severity;
            public string Location => Issue.Location;
            public string Message => Issue.Message;
        }

        [SerializeField] private bool includeAssets = true;
        [SerializeField] private bool includeScenes = true;
        [SerializeField] private bool showErrors = true;
        [SerializeField] private bool showWarnings = true;
        [SerializeField] private string search = "";

        private readonly List<Row> _rows = new List<Row>();
        private List<Row> _visible = new List<Row>();
        private Row _selected;
        private Vector2 _scroll;
        private int _errorCount;
        private int _warningCount;

        // Set when showing a given list (e.g. from a failed build) instead of collecting.
        private string _source;
        private static List<ValidationIssue> _pendingIssues;
        private static string _pendingSource;

        // GameObject to select after the next collection (see ShowFor).
        private static GameObject _pendingFocus;

        private GUIContent _errorIcon;
        private GUIContent _warningIcon;

        [MenuItem("Tools/muValidation/Validation", false, 2003)]
        public static void Open()
        {
            GetWindow<ValidationWindow>("Validation").Show();
        }

        /// <summary>
        /// Opens the window with the given issues instead of collecting them. Refresh switches back
        /// to collecting from assets and open scenes.
        /// </summary>
        public static void ShowIssues(List<ValidationIssue> issues, string source)
        {
            _pendingIssues = issues;
            _pendingSource = source;
            var window = GetWindow<ValidationWindow>("Validation");
            window.ApplyPending();
            window.Show();
        }

        /// <summary>
        /// Opens the window, collects results and selects the issue of the GameObject,
        /// or of its first descendant with issues.
        /// </summary>
        /// <remarks>muHierarchy calls this via reflection. Keep its signature stable.</remarks>
        public static void ShowFor(GameObject go)
        {
            _pendingFocus = go;
            var existed = HasOpenInstances<ValidationWindow>();
            var window = GetWindow<ValidationWindow>("Validation");
            window.Show();

            // A new window collects from OnEnable.
            if (existed) window.Refresh();
        }

        private void OnEnable()
        {
            _errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
            _warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (_pendingIssues != null) ApplyPending();
                else Refresh();
            };
        }

        private void ApplyPending()
        {
            if (_pendingIssues == null) return;
            SetIssues(_pendingIssues, _pendingSource);
            _pendingIssues = null;
            _pendingSource = null;
        }

        private void Refresh()
        {
            if (this == null) return;

            var issues = new List<ValidationIssue>();
            if (includeAssets && !ValidationRunner.CollectAssets(issues, true))
            {
                issues.Clear();
            }

            if (includeScenes || _pendingFocus != null) ValidationRunner.CollectOpenScenes(issues);
            SetIssues(issues, null);
            FocusPending();
        }

        private void FocusPending()
        {
            var go = _pendingFocus;
            _pendingFocus = null;
            if (go == null) return;

            var row = _rows.FirstOrDefault(r => r.Issue.SceneObject == go)
                ?? _rows.FirstOrDefault(r => r.Issue.SceneObject != null && r.Issue.SceneObject.transform.IsChildOf(go.transform));
            if (row == null) return;

            // Make sure the row is not filtered out.
            search = "";
            showErrors = true;
            showWarnings = true;
            includeScenes = true;
            ApplyFilter();

            _selected = row;
            _scroll.y = _visible.IndexOf(row) * RowHeight;
            EditorGUIUtility.PingObject(row.Issue.SceneObject);
            Repaint();
        }

        private void SetIssues(List<ValidationIssue> issues, string source)
        {
            _source = source;
            _selected = null;
            _rows.Clear();
            foreach (var issue in issues) _rows.Add(CreateRow(issue));

            _rows.Sort((a, b) =>
            {
                var severity = b.Severity.CompareTo(a.Severity);
                return severity != 0 ? severity : string.CompareOrdinal(a.Location, b.Location);
            });

            _errorCount = _rows.Count(r => r.Severity == ValidationSeverity.Error);
            _warningCount = _rows.Count(r => r.Severity == ValidationSeverity.Warning);
            ApplyFilter();
            Repaint();
        }

        private static Row CreateRow(ValidationIssue issue)
        {
            var message = issue.Message ?? "";
            var newline = message.IndexOf('\n');
            return new Row
            {
                Issue = issue,
                FirstLine = newline < 0 ? message : message.Substring(0, newline) + " …",
            };
        }

        private void ApplyFilter()
        {
            _visible = _rows.Where(r =>
                (r.Severity == ValidationSeverity.Error ? showErrors : showWarnings)
                && (string.IsNullOrEmpty(search)
                    || r.Location.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || r.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (_selected != null && !_visible.Contains(_selected)) _selected = null;
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

                EditorGUI.BeginChangeCheck();
                includeAssets = GUILayout.Toggle(includeAssets, "Assets", EditorStyles.toolbarButton, GUILayout.Width(55));
                includeScenes = GUILayout.Toggle(includeScenes, "Scenes", EditorStyles.toolbarButton, GUILayout.Width(55));
                if (EditorGUI.EndChangeCheck()) EditorApplication.delayCall += Refresh;

                if (_source != null) GUILayout.Label(_source, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MaxWidth(250));
                showErrors = GUILayout.Toggle(showErrors, new GUIContent(_errorCount.ToString(), _errorIcon.image), EditorStyles.toolbarButton);
                showWarnings = GUILayout.Toggle(showWarnings, new GUIContent(_warningCount.ToString(), _warningIcon.image), EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck()) ApplyFilter();
            }
        }

        private void DrawList(Rect rect)
        {
            if (_visible.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20),
                    _rows.Count == 0 ? "No problems found." : "No results match the filter.", EditorStyles.miniLabel);
                return;
            }

            var content = new Rect(0, 0, rect.width - 16, _visible.Count * RowHeight);
            _scroll = GUI.BeginScrollView(rect, _scroll, content);

            // Draw only the visible rows.
            var first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowHeight));
            var last = Mathf.Min(_visible.Count - 1, Mathf.CeilToInt((_scroll.y + rect.height) / RowHeight));
            var locationWidth = Mathf.Max(150f, content.width * 0.4f);

            for (var i = first; i <= last; i++)
            {
                var row = _visible[i];
                var rowRect = new Rect(0, i * RowHeight, content.width, RowHeight);

                if (Event.current.type == EventType.Repaint)
                {
                    if (row == _selected) EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.9f, 0.4f));
                    else if (i % 2 == 1) EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.5f, 0.5f, 0.06f));
                }

                var icon = row.Severity == ValidationSeverity.Error ? _errorIcon : _warningIcon;
                GUI.Label(new Rect(rowRect.x + 4, rowRect.y + 2, 16, 16), icon);
                GUI.Label(new Rect(rowRect.x + 24, rowRect.y, locationWidth - 24, RowHeight), new GUIContent(row.Location, row.Location));
                GUI.Label(new Rect(locationWidth + 4, rowRect.y, content.width - locationWidth - 4, RowHeight),
                    new GUIContent(row.FirstLine, row.Message));

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selected = row;
                    var target = row.Issue.Resolve();
                    if (target != null)
                    {
                        if (Event.current.clickCount >= 2)
                        {
                            Selection.activeObject = target;
                            if (row.Issue.AssetPath != null) AssetDatabase.OpenAsset(target);
                        }
                        else
                        {
                            EditorGUIUtility.PingObject(target);
                        }
                    }

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
                GUI.Label(inner, "Select a row to see the full message.", EditorStyles.miniLabel);
                return;
            }

            EditorGUI.SelectableLabel(inner, _selected.Location + "\n" + _selected.Message, EditorStyles.wordWrappedLabel);
        }
    }
}
