// Save to Assets/Editor/PrefabOverrideIconInHierarchy.cs etc.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Mmzkworks.muHierarchy.Editor
{
    [InitializeOnLoad]
    public static class PrefabOverrideIconInHierarchy
    {
        private static readonly Dictionary<HierarchyViewMode, IHierarchyViewModeHandler> ViewModeHandlers =
            new Dictionary<HierarchyViewMode, IHierarchyViewModeHandler>
            {
                { HierarchyViewMode.None, HierarchyViewModeNoneHandler.Instance },
                { HierarchyViewMode.ComponentView, HierarchyViewModeComponentViewHandler.Instance },
                { HierarchyViewMode.ComponentNameView, HierarchyViewModeComponentNameViewHandler.Instance },
                { HierarchyViewMode.AsmdefView, HierarchyViewModeAsmdefViewHandler.Instance },
                { HierarchyViewMode.PrefabPathView, HierarchyViewModePrefabPathViewHandler.Instance },
                { HierarchyViewMode.MeshInfoView, HierarchyViewModeMeshInfoViewHandler.Instance },
                { HierarchyViewMode.MaterialInfoView, HierarchyViewModeMaterialInfoViewHandler.Instance },
                { HierarchyViewMode.ShaderInfoView, HierarchyViewModeShaderInfoViewHandler.Instance },
                { HierarchyViewMode.ReferenceView, HierarchyViewModeReferenceViewHandler.Instance },
            };

        // Icons for ComponentView (lazy init)
        private static Texture2D _iconPrefab;
        private static Texture2D _iconPrefabApplyWarning;
        private static Texture2D _iconPrefabEmpty;
        private static Texture2D _iconError;

        private static bool _iconsInitialized = false;
        private static readonly object _iconsInitLock = new object();

        // Preference keys
        private const string PrefsKeyViewMode = "PrefabOverrideIconInHierarchy.ViewMode";
        private const string PrefsKeyShowLayerName = "PrefabOverrideIconInHierarchy.ShowLayerName";
        private const string PrefsKeyShowTagName = "PrefabOverrideIconInHierarchy.ShowTagName";
        private const string PrefsKeyShowPrefabIcon = "PrefabOverrideIconInHierarchy.ShowPrefabIcon";
        private const string PrefsKeyShowComponentIcons = "PrefabOverrideIconInHierarchy.ShowComponentIcons";
        private const string PrefsKeyDedupeComponentIcons = "PrefabOverrideIconInHierarchy.DedupeComponentIcons";
        private const string PrefsKeyShowTagBackground = "PrefabOverrideIconInHierarchy.ShowTagBackground";
        private const string PrefsKeyShowReferenceLines = "PrefabOverrideIconInHierarchy.ShowReferenceLines";
        private const string PrefsKeyShowAlternatingRowColors = "PrefabOverrideIconInHierarchy.ShowAlternatingRowColors";
        private const string PrefsKeyTagColorPrefix = "PrefabOverrideIconInHierarchy.TagColor.";
        private const string PrefsKeyTagTextColorPrefix = "PrefabOverrideIconInHierarchy.TagTextColor.";
        private const string PrefsKeyTagSectionMarkerPrefix = "PrefabOverrideIconInHierarchy.TagSectionMarker.";

        // Display settings (loaded from EditorPrefs)
        private static bool ShowLayerName
        {
            get => EditorPrefs.GetBool(PrefsKeyShowLayerName, true);
            set => EditorPrefs.SetBool(PrefsKeyShowLayerName, value);
        }

        private static bool ShowTagName
        {
            get => EditorPrefs.GetBool(PrefsKeyShowTagName, true);
            set => EditorPrefs.SetBool(PrefsKeyShowTagName, value);
        }

        private static bool ShowPrefabIcon
        {
            get => EditorPrefs.GetBool(PrefsKeyShowPrefabIcon, true);
            set => EditorPrefs.SetBool(PrefsKeyShowPrefabIcon, value);
        }

        private static bool ShowComponentIcons
        {
            get => EditorPrefs.GetBool(PrefsKeyShowComponentIcons, true);
            set => EditorPrefs.SetBool(PrefsKeyShowComponentIcons, value);
        }

        private static bool DedupeComponentIcons
        {
            get => EditorPrefs.GetBool(PrefsKeyDedupeComponentIcons, false);
            set => EditorPrefs.SetBool(PrefsKeyDedupeComponentIcons, value);
        }

        private static bool ShowTagBackground
        {
            get => EditorPrefs.GetBool(PrefsKeyShowTagBackground, true);
            set => EditorPrefs.SetBool(PrefsKeyShowTagBackground, value);
        }

        private static bool ShowReferenceLines
        {
            get => EditorPrefs.GetBool(PrefsKeyShowReferenceLines, true);
            set => EditorPrefs.SetBool(PrefsKeyShowReferenceLines, value);
        }

        private static bool ShowAlternatingRowColors
        {
            get => EditorPrefs.GetBool(PrefsKeyShowAlternatingRowColors, true);
            set => EditorPrefs.SetBool(PrefsKeyShowAlternatingRowColors, value);
        }

        private static HierarchyViewMode CurrentViewMode
        {
            get => (HierarchyViewMode)EditorPrefs.GetInt(PrefsKeyViewMode, (int)HierarchyViewMode.ComponentView);
            set => EditorPrefs.SetInt(PrefsKeyViewMode, (int)value);
        }

        static PrefabOverrideIconInHierarchy()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.hierarchyChanged += HierarchyViewModeComponentViewHandler.ClearComponentIconCache;
            Undo.undoRedoPerformed += HierarchyViewModeComponentViewHandler.ClearComponentIconCache;
            EditorApplication.hierarchyChanged += AsmdefResolver.ClearCaches;
            Undo.undoRedoPerformed += AsmdefResolver.ClearCaches;
            EditorApplication.hierarchyChanged += () => { if (CurrentViewMode == HierarchyViewMode.ReferenceView) HierarchyViewModeReferenceViewHandler.ClearReferenceInfoCache(); };
            Undo.undoRedoPerformed += () => { if (CurrentViewMode == HierarchyViewMode.ReferenceView) HierarchyViewModeReferenceViewHandler.ClearReferenceInfoCache(); };
            EditorApplication.hierarchyChanged += () => { if (CurrentViewMode == HierarchyViewMode.ReferenceView) HierarchyViewModeReferenceViewHandler.ClearGameObjectPositions(); };
            Undo.undoRedoPerformed += () => { if (CurrentViewMode == HierarchyViewMode.ReferenceView) HierarchyViewModeReferenceViewHandler.ClearGameObjectPositions(); };
            
            // Icon init is done in OnHierarchyGUI (safe as it runs on main thread)
            // Do not use EditorApplication.delayCall (may be registered multiple times)
        }

        // Icon initialization (assumed to run on main thread)
        // Called from OnHierarchyGUI, so runs on main thread
        private static void InitializeIcons()
        {
            // Prevent double init (thread-safe)
            if (_iconsInitialized)
                return;

            lock (_iconsInitLock)
            {
                // Re-check inside lock (double-checked locking)
                if (_iconsInitialized)
                    return;

                try
                {
                    // Reuse an Editor icon (can be changed later)
                    _iconPrefab = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
                    _iconPrefabApplyWarning = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;
                    // Empty Prefab icon (for Prefab instance children)
                    _iconPrefabEmpty = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
                    _iconError = EditorGUIUtility.IconContent("console.erroricon").image as Texture2D;

                    _iconsInitialized = true;
                }
                catch (System.Exception)
                {
                    // Retry init on next call if failed
                    // Ignore error and continue (icon may stay null; null checks handle it)
                }
            }
        }

        // Ensure icons are initialized (call before use)
        // OnHierarchyGUI runs on main thread, so direct init here is safe
        private static void EnsureIconsInitialized()
        {
            // Do nothing if already initialized
            if (_iconsInitialized)
                return;

            // Initialize after confirming we are on main thread
            // OnHierarchyGUI is called from main thread, so direct init is safe
            InitializeIcons();
        }

        // Add to Hierarchy window menu
        [MenuItem("Tools/muHierarchy/View Mode/Component View", false, 12)]
        private static void SetViewModeComponentView()
        {
            CurrentViewMode = HierarchyViewMode.ComponentView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Component View", true)]
        private static bool SetViewModeComponentViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Component View", CurrentViewMode == HierarchyViewMode.ComponentView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/None", false, 11)]
        private static void SetViewModeNone()
        {
            CurrentViewMode = HierarchyViewMode.None;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/None", true)]
        private static bool SetViewModeNoneValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/None", CurrentViewMode == HierarchyViewMode.None);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/Component Name View", false, 13)]
        private static void SetViewModeComponentNameView()
        {
            CurrentViewMode = HierarchyViewMode.ComponentNameView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Component Name View", true)]
        private static bool SetViewModeComponentNameViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Component Name View",
                CurrentViewMode == HierarchyViewMode.ComponentNameView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/Asmdef View", false, 14)]
        private static void SetViewModeAsmdefView()
        {
            CurrentViewMode = HierarchyViewMode.AsmdefView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Asmdef View", true)]
        private static bool SetViewModeAsmdefViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Asmdef View", CurrentViewMode == HierarchyViewMode.AsmdefView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/Prefab Path View", false, 15)]
        private static void SetViewModePrefabPathView()
        {
            CurrentViewMode = HierarchyViewMode.PrefabPathView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Prefab Path View", true)]
        private static bool SetViewModePrefabPathViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Prefab Path View", CurrentViewMode == HierarchyViewMode.PrefabPathView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/Mesh Info View", false, 16)]
        private static void SetViewModeMeshInfoView()
        {
            CurrentViewMode = HierarchyViewMode.MeshInfoView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Mesh Info View", true)]
        private static bool SetViewModeMeshInfoViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Mesh Info View", CurrentViewMode == HierarchyViewMode.MeshInfoView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/Material Info View", false, 17)]
        private static void SetViewModeMaterialInfoView()
        {
            CurrentViewMode = HierarchyViewMode.MaterialInfoView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Material Info View", true)]
        private static bool SetViewModeMaterialInfoViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Material Info View",
                CurrentViewMode == HierarchyViewMode.MaterialInfoView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/Shader Info View", false, 18)]
        private static void SetViewModeShaderInfoView()
        {
            CurrentViewMode = HierarchyViewMode.ShaderInfoView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Shader Info View", true)]
        private static bool SetViewModeShaderInfoViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Shader Info View", CurrentViewMode == HierarchyViewMode.ShaderInfoView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/View Mode/Reference View", false, 19)]
        private static void SetViewModeReferenceView()
        {
            CurrentViewMode = HierarchyViewMode.ReferenceView;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/View Mode/Reference View", true)]
        private static bool SetViewModeReferenceViewValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/View Mode/Reference View", CurrentViewMode == HierarchyViewMode.ReferenceView);
            return true;
        }

        // GameObject context menu: mu 系を Unity 標準と離して下部にまとめる（const + 相対値で位置指定）
        private const int GameObjectMenuPriorityBase = 21100;

        [MenuItem("GameObject/muHierarchy/None", false, GameObjectMenuPriorityBase + 0)]
        private static void SetViewModeNoneContext() => SetViewModeNone();
        [MenuItem("GameObject/muHierarchy/None", true)]
        private static bool SetViewModeNoneContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/None", CurrentViewMode == HierarchyViewMode.None);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Component View", false, GameObjectMenuPriorityBase + 1)]
        private static void SetViewModeComponentViewContext() => SetViewModeComponentView();
        [MenuItem("GameObject/muHierarchy/Component View", true)]
        private static bool SetViewModeComponentViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Component View", CurrentViewMode == HierarchyViewMode.ComponentView);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Component Name View", false, GameObjectMenuPriorityBase + 2)]
        private static void SetViewModeComponentNameViewContext() => SetViewModeComponentNameView();
        [MenuItem("GameObject/muHierarchy/Component Name View", true)]
        private static bool SetViewModeComponentNameViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Component Name View", CurrentViewMode == HierarchyViewMode.ComponentNameView);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Asmdef View", false, GameObjectMenuPriorityBase + 3)]
        private static void SetViewModeAsmdefViewContext() => SetViewModeAsmdefView();
        [MenuItem("GameObject/muHierarchy/Asmdef View", true)]
        private static bool SetViewModeAsmdefViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Asmdef View", CurrentViewMode == HierarchyViewMode.AsmdefView);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Prefab Path View", false, GameObjectMenuPriorityBase + 4)]
        private static void SetViewModePrefabPathViewContext() => SetViewModePrefabPathView();
        [MenuItem("GameObject/muHierarchy/Prefab Path View", true)]
        private static bool SetViewModePrefabPathViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Prefab Path View", CurrentViewMode == HierarchyViewMode.PrefabPathView);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Mesh Info View", false, GameObjectMenuPriorityBase + 5)]
        private static void SetViewModeMeshInfoViewContext() => SetViewModeMeshInfoView();
        [MenuItem("GameObject/muHierarchy/Mesh Info View", true)]
        private static bool SetViewModeMeshInfoViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Mesh Info View", CurrentViewMode == HierarchyViewMode.MeshInfoView);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Material Info View", false, GameObjectMenuPriorityBase + 6)]
        private static void SetViewModeMaterialInfoViewContext() => SetViewModeMaterialInfoView();
        [MenuItem("GameObject/muHierarchy/Material Info View", true)]
        private static bool SetViewModeMaterialInfoViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Material Info View", CurrentViewMode == HierarchyViewMode.MaterialInfoView);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Shader Info View", false, GameObjectMenuPriorityBase + 7)]
        private static void SetViewModeShaderInfoViewContext() => SetViewModeShaderInfoView();
        [MenuItem("GameObject/muHierarchy/Shader Info View", true)]
        private static bool SetViewModeShaderInfoViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Shader Info View", CurrentViewMode == HierarchyViewMode.ShaderInfoView);
            return true;
        }

        [MenuItem("GameObject/muHierarchy/Reference View", false, GameObjectMenuPriorityBase + 8)]
        private static void SetViewModeReferenceViewContext() => SetViewModeReferenceView();
        [MenuItem("GameObject/muHierarchy/Reference View", true)]
        private static bool SetViewModeReferenceViewContextValidate()
        {
            Menu.SetChecked("GameObject/muHierarchy/Reference View", CurrentViewMode == HierarchyViewMode.ReferenceView);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Show Layer Name", false, 20)]
        private static void ToggleShowLayerName()
        {
            ShowLayerName = !ShowLayerName;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Show Layer Name", true)]
        private static bool ToggleShowLayerNameValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Show Layer Name", ShowLayerName);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Show Tag Name", false, 21)]
        private static void ToggleShowTagName()
        {
            ShowTagName = !ShowTagName;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Show Tag Name", true)]
        private static bool ToggleShowTagNameValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Show Tag Name", ShowTagName);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Show Prefab Icon", false, 22)]
        private static void ToggleShowPrefabIcon()
        {
            ShowPrefabIcon = !ShowPrefabIcon;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Show Prefab Icon", true)]
        private static bool ToggleShowPrefabIconValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Show Prefab Icon", ShowPrefabIcon);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Show Component Icons", false, 23)]
        private static void ToggleShowComponentIcons()
        {
            ShowComponentIcons = !ShowComponentIcons;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Show Component Icons", true)]
        private static bool ToggleShowComponentIconsValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Show Component Icons", ShowComponentIcons);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Dedupe Component Icons", false, 24)]
        private static void ToggleDedupeComponentIcons()
        {
            DedupeComponentIcons = !DedupeComponentIcons;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Dedupe Component Icons", true)]
        private static bool ToggleDedupeComponentIconsValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Dedupe Component Icons", DedupeComponentIcons);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Show Reference Lines", false, 25)]
        private static void ToggleShowReferenceLines()
        {
            ShowReferenceLines = !ShowReferenceLines;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Show Reference Lines", true)]
        private static bool ToggleShowReferenceLinesValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Show Reference Lines", ShowReferenceLines);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Show Alternating Row Colors", false, 26)]
        private static void ToggleShowAlternatingRowColors()
        {
            ShowAlternatingRowColors = !ShowAlternatingRowColors;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Show Alternating Row Colors", true)]
        private static bool ToggleShowAlternatingRowColorsValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Show Alternating Row Colors", ShowAlternatingRowColors);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Tag Background/Enable", false, 30)]
        private static void ToggleShowTagBackground()
        {
            ShowTagBackground = !ShowTagBackground;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("Tools/muHierarchy/Tag Background/Enable", true)]
        private static bool ToggleShowTagBackgroundValidate()
        {
            Menu.SetChecked("Tools/muHierarchy/Tag Background/Enable", ShowTagBackground);
            return true;
        }

        [MenuItem("Tools/muHierarchy/Tag Background/Set Color For Selected Tag...", false, 31)]
        private static void SetTagBackgroundColorForSelectedTag()
        {
            if (!TryGetSelectedTag(out var tag))
            {
                EditorUtility.DisplayDialog("Tag Background", "Please select a GameObject.", "OK");
                return;
            }

            TagBackgroundColorWindow.OpenForTag(tag);
        }

        [MenuItem("Tools/muHierarchy/Tag Background/Set Color For Selected Tag...", true)]
        private static bool SetTagBackgroundColorForSelectedTagValidate()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem("Tools/muHierarchy/Tag Background/Clear Color For Selected Tag", false, 32)]
        private static void ClearTagBackgroundColorForSelectedTag()
        {
            if (!TryGetSelectedTag(out var tag))
            {
                EditorUtility.DisplayDialog("Tag Background", "Please select a GameObject.", "OK");
                return;
            }

            ClearTagBackgroundColor(tag);
        }

        [MenuItem("Tools/muHierarchy/Tag Background/Clear Color For Selected Tag", true)]
        private static bool ClearTagBackgroundColorForSelectedTagValidate()
        {
            if (!TryGetSelectedTag(out var tag))
                return false;
            return EditorPrefs.HasKey(GetTagColorKey(tag));
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            // Ensure icons are initialized (assumed to run on main thread)
            EnsureIconsInitialized();
            
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go == null)
                return;

            // Only scene objects (exclude Prefabs in Project view)
            if (!go.scene.IsValid() || !go.scene.isLoaded)
                return;

            var viewMode = CurrentViewMode;
            int currentFrame = Time.frameCount;
            if (viewMode == HierarchyViewMode.ReferenceView)
                HierarchyViewModeReferenceViewHandler.ProcessInput(instanceID, selectionRect, currentFrame);

            var e = Event.current;
            // Alt + left-click to open pinned Inspector (reserved)
            if (e != null
                && e.type == EventType.MouseDown
                && e.button == 0
                && e.alt
                && selectionRect.Contains(e.mousePosition))
            {
                // reserved
            }

            // Alternating row background (unselected only)
            if (ShowAlternatingRowColors && !HierarchyDrawUtils.IsSelectedInstanceId(instanceID))
            {
                // Compute row index (Y position / row height)
                const float rowHeight = 16f; // Standard Hierarchy row height
                int rowIndex = Mathf.FloorToInt(selectionRect.y / rowHeight);
                
                // Alternate color for even/odd rows
                if (rowIndex % 2 == 1)
                {
                    // Draw background on odd rows (light gray)
                    Color alternatingColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
                    // `selectionRect` can start after the left icons area; expand to full width.
                    const float iconSizeForBg = 16f + 28f;
                    var alternatingRect = new Rect(
                        selectionRect.xMin - 28f,        // Keep the original left alignment
                        selectionRect.y,
                        // +1 icon width to cover the right-side area omitted from `selectionRect`.
                        Mathf.Max(0f, selectionRect.width + iconSizeForBg),
                        selectionRect.height
                    );
                    EditorGUI.DrawRect(alternatingRect, alternatingColor);
                }
            }

            // Tag background (unselected only, or when editing Tag show even when selected)
            bool isEditingThisTag = TagBackgroundColorWindow.IsEditingTag(go.tag);
            if (ShowTagBackground && (!HierarchyDrawUtils.IsSelectedInstanceId(instanceID) || isEditingThisTag))
            {
                if (TryGetTagBackgroundColor(go.tag, out var bgColor))
                {
                    // Shift left by 2 icon widths, right by 1 icon width
                    const float iconSizeForBg = 16f;
                    var bgRect = new Rect(
                        iconSizeForBg * 2f, // Left: 2 icon widths
                        selectionRect.y + 1f,
                        selectionRect.xMax,
                        selectionRect.height - 2f
                    );
                    EditorGUI.DrawRect(bgRect, bgColor);
                }
            }

            // For section marker Tag hide all extra info (show only background and text color)
            if (IsTagSectionMarker(go.tag))
            {
                // Stop custom draw when selected or editing
                bool isSelected = HierarchyDrawUtils.IsSelectedInstanceId(instanceID);
                bool isEditingName = Selection.activeGameObject == go && EditorGUIUtility.editingTextField;

                // Draw GameObject name with text color (only when not selected/editing)
                if (!isSelected && !isEditingName && TryGetTagTextColor(go.tag, out var textColor))
                {
                    // Hierarchy item name draw position (right of expand icon)
                    //const float nameOffsetX = 18f;
                    const float nameOffsetX = -18f;
                    var nameRect = new Rect(
                        selectionRect.x + nameOffsetX,
                        selectionRect.y,
                        selectionRect.width - nameOffsetX,
                        selectionRect.height
                    );

                    var nameStyle = new GUIStyle(EditorStyles.label);
                    nameStyle.normal.textColor = textColor;
                    nameStyle.fontSize = EditorStyles.label.fontSize - 1; // Slightly smaller font
                    GUI.Label(nameRect, go.name, nameStyle);
                }

                return;
            }

            const float iconSize = 16f;
            const float spacing = 4f;
            const float componentIconSpacing = 1f;
            const float textSpacing = 2f;
            const float fixedRightMargin = iconSize * 5f;
            bool showLayer = viewMode == HierarchyViewMode.ComponentView && ShowLayerName;
            bool showTag = viewMode == HierarchyViewMode.ComponentView && ShowTagName;
            bool showPrefabIcon = viewMode == HierarchyViewMode.ComponentView && ShowPrefabIcon;
            bool showComponentIcons = viewMode == HierarchyViewMode.ComponentView && ShowComponentIcons;
            float fixedRightPosition = viewMode == HierarchyViewMode.ComponentView
                ? selectionRect.xMax - fixedRightMargin
                : selectionRect.xMax - 6f;
            GUIStyle smallLabelStyle = new GUIStyle(EditorStyles.label);
            smallLabelStyle.fontSize = 10;
            smallLabelStyle.normal.textColor = Color.gray;

            var ctx = new HierarchyDrawContext
            {
                InstanceId = instanceID,
                SelectionRect = selectionRect,
                GameObject = go,
                FixedRightPosition = fixedRightPosition,
                SmallLabelStyle = smallLabelStyle,
                IconSize = iconSize,
                Spacing = spacing,
                ComponentIconSpacing = componentIconSpacing,
                TextSpacing = textSpacing,
                FixedRightMargin = fixedRightMargin,
                ShowLayerName = showLayer,
                ShowTagName = showTag,
                ShowPrefabIcon = showPrefabIcon,
                ShowComponentIcons = showComponentIcons,
                DedupeComponentIcons = DedupeComponentIcons,
                ShowReferenceLines = ShowReferenceLines,
                IconError = _iconError,
                IconPrefab = _iconPrefab,
                IconPrefabApplyWarning = _iconPrefabApplyWarning,
                IconPrefabEmpty = _iconPrefabEmpty
            };
            if (ViewModeHandlers.TryGetValue(viewMode, out var handler))
                handler.Draw(instanceID, selectionRect, go, ctx);
        }

        private static string GetTagColorKey(string tag) => $"{PrefsKeyTagColorPrefix}{tag}";

        private static bool TryGetTagBackgroundColor(string tag, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(tag))
                return false;

            string key = GetTagColorKey(tag);
            if (!EditorPrefs.HasKey(key))
                return false;

            string html = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(html))
                return false;

            // Expect "#RRGGBBAA" format
            if (!html.StartsWith("#", StringComparison.Ordinal))
                html = "#" + html;

            return ColorUtility.TryParseHtmlString(html, out color);
        }

        private static void SetTagBackgroundColor(string tag, Color color)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            string key = GetTagColorKey(tag);
            string html = "#" + ColorUtility.ToHtmlStringRGBA(color);
            EditorPrefs.SetString(key, html);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void ClearTagBackgroundColor(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            string key = GetTagColorKey(tag);
            if (EditorPrefs.HasKey(key))
                EditorPrefs.DeleteKey(key);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static string GetTagTextColorKey(string tag) => $"{PrefsKeyTagTextColorPrefix}{tag}";

        private static bool TryGetTagTextColor(string tag, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(tag))
                return false;

            string key = GetTagTextColorKey(tag);
            if (!EditorPrefs.HasKey(key))
                return false;

            string html = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(html))
                return false;

            // Expect "#RRGGBBAA" format
            if (!html.StartsWith("#", StringComparison.Ordinal))
                html = "#" + html;

            return ColorUtility.TryParseHtmlString(html, out color);
        }

        private static void SetTagTextColor(string tag, Color color)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            string key = GetTagTextColorKey(tag);
            string html = "#" + ColorUtility.ToHtmlStringRGBA(color);
            EditorPrefs.SetString(key, html);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void ClearTagTextColor(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            string key = GetTagTextColorKey(tag);
            if (EditorPrefs.HasKey(key))
                EditorPrefs.DeleteKey(key);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static bool TryGetSelectedTag(out string tag)
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                tag = null;
                return false;
            }

            tag = go.tag;
            return !string.IsNullOrEmpty(tag);
        }

        private static string GetTagSectionMarkerKey(string tag) => $"{PrefsKeyTagSectionMarkerPrefix}{tag}";

        private static bool IsTagSectionMarker(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;
            string key = GetTagSectionMarkerKey(tag);
            return EditorPrefs.GetBool(key, false);
        }

        private static void SetTagSectionMarker(string tag, bool value)
        {
            if (string.IsNullOrEmpty(tag))
                return;
            string key = GetTagSectionMarkerKey(tag);
            EditorPrefs.SetBool(key, value);
            EditorApplication.RepaintHierarchyWindow();
        }

        private sealed class TagBackgroundColorWindow : EditorWindow
        {
            private static string _editingTag = null; // Track tag being edited

            private string _tag;
            private Color _color;
            private Color _lastColor; // Save last color (for change detection)
            private Color _textColor;
            private Color _lastTextColor; // Save last text color (for change detection)
            private bool _isSectionMarker;

            public static bool IsEditingTag(string tag)
            {
                return _editingTag != null && _editingTag == tag;
            }

            public static void OpenForTag(string tag)
            {
                var w = CreateInstance<TagBackgroundColorWindow>();
                w.titleContent = new GUIContent("Tag Background");
                w._tag = tag;
                _editingTag = tag;

                if (!TryGetTagBackgroundColor(tag, out w._color))
                    w._color = new Color(0.2f, 0.6f, 1f, 0.15f); // Initial color (light)

                w._lastColor = w._color;

                if (!TryGetTagTextColor(tag, out w._textColor))
                    w._textColor = EditorStyles.label.normal.textColor; // Default text color

                w._lastTextColor = w._textColor;
                w._isSectionMarker = IsTagSectionMarker(tag);

                w.minSize = new Vector2(280, 160);
                w.ShowUtility();
            }

            private void OnDestroy()
            {
                _editingTag = null;
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField($"Tag: {_tag}");

                // Background color (alpha disabled)
                var bgColor = _color;
                bgColor.a = 1f; // Set alpha to 1
                bgColor = EditorGUILayout.ColorField(new GUIContent("Background Color"), bgColor, true, true, false);
                bgColor.a = 1f; // Set alpha to 1
                _color = bgColor;

                // Apply color change immediately
                if (_color != _lastColor)
                {
                    SetTagBackgroundColor(_tag, _color);
                    _lastColor = _color;
                }

                // Text color
                _textColor = EditorGUILayout.ColorField(new GUIContent("Text Color"), _textColor, true, true, true);

                // Apply text color change immediately
                if (_textColor != _lastTextColor)
                {
                    SetTagTextColor(_tag, _textColor);
                    _lastTextColor = _textColor;
                }

                _isSectionMarker = EditorGUILayout.Toggle(new GUIContent("Use as section marker (hide extra info)"), _isSectionMarker);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save"))
                    {
                        SetTagBackgroundColor(_tag, _color);
                        SetTagTextColor(_tag, _textColor);
                        SetTagSectionMarker(_tag, _isSectionMarker);
                        _editingTag = null;
                        Close();
                    }

                    if (GUILayout.Button("Clear"))
                    {
                        ClearTagBackgroundColor(_tag);
                        ClearTagTextColor(_tag);
                        _editingTag = null;
                        Close();
                    }

                    if (GUILayout.Button("Cancel"))
                    {
                        // Revert changes made while editing
                        if (TryGetTagBackgroundColor(_tag, out var originalColor))
                        {
                            SetTagBackgroundColor(_tag, originalColor);
                        }

                        if (TryGetTagTextColor(_tag, out var originalTextColor))
                        {
                            SetTagTextColor(_tag, originalTextColor);
                        }

                        _editingTag = null;
                        Close();
                    }
                }
            }
        }

        // Check if child hierarchy has unapplied Prefab instance
        private static bool HasChildPrefabOverrides(GameObject go)
        {
            if (go == null)
                return false;

            // Check direct children
            foreach (Transform child in go.transform)
            {
                var childGo = child.gameObject;

                // Only scene objects
                if (!childGo.scene.IsValid() || !childGo.scene.isLoaded)
                    continue;

                // Check if Prefab instance has unapplied changes
                if (PrefabUtility.IsPartOfPrefabInstance(childGo))
                {
                    if (PrefabUtility.HasPrefabInstanceAnyOverrides(childGo, false))
                    {
                        return true;
                    }
                }

                // Recursively check child hierarchy
                if (HasChildPrefabOverrides(childGo))
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawPrefabIcon(Rect selectionRect, bool hasOverrides, float rightOffset, float iconSize)
        {
            // For Prefab instance, right-align icon only when no override (applied)
            // When override (unapplied), do nothing to keep Unity default <!> display
            // Icon draw position (from right, vertically centered)
            var iconRect = new Rect(
                selectionRect.xMax - rightOffset,
                selectionRect.y + (selectionRect.height - iconSize) * 0.5f, // Vertically centered
                iconSize,
                iconSize
            );

            DrawPrefabIconAtRect(iconRect, hasOverrides, false,
                hasOverrides ? "Prefab (has overrides)" : "Prefab (no overrides)");
        }

        private static void DrawPrefabIconAtRect(Rect iconRect, bool hasOverrides, bool isEmpty, string tooltip)
        {
            if (isEmpty)
            {
                // Show empty Prefab icon for Prefab instance children (semi-transparent)
                if (_iconPrefabEmpty != null)
                {
                    var originalColor = GUI.color;
                    GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.4f); // Semi-transparent
                    var iconContent = new GUIContent(_iconPrefabEmpty, tooltip);
                    GUI.Label(iconRect, iconContent);
                    GUI.color = originalColor;
                }
                else
                {
                    // Fallback to text when icon unavailable
                    var originalColor2 = GUI.color;
                    GUI.color = new Color(Color.gray.r, Color.gray.g, Color.gray.b, 0.4f);
                    EditorGUI.LabelField(iconRect, "○");
                    GUI.color = originalColor2;
                }
            }
            else if (hasOverrides)
            {
                // Use warning icon for unapplied Prefab
                if (_iconPrefabApplyWarning != null)
                {
                    // Draw icon with GUIContent
                    var iconContent = new GUIContent(_iconPrefabApplyWarning, tooltip);
                    GUI.Label(iconRect, iconContent);
                }
                else
                {
                    // Fallback to text when icon unavailable
                    var originalColor2 = GUI.color;
                    GUI.color = Color.yellow;
                    EditorGUI.LabelField(iconRect, "★");
                    GUI.color = originalColor2;
                }
            }
            else
            {
                // Use _iconPrefab for applied Prefab
                if (_iconPrefab != null)
                {
                    // Draw icon with GUIContent
                    var iconContent = new GUIContent(_iconPrefab, tooltip);
                    GUI.Label(iconRect, iconContent);
                }
                else
                {
                    // Fallback to text when icon unavailable
                    var originalColor2 = GUI.color;
                    GUI.color = Color.yellow;
                    EditorGUI.LabelField(iconRect, "★");
                    GUI.color = originalColor2;
                }
            }
        }
    }
}
