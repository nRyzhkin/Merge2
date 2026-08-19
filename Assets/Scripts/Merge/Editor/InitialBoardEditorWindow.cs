using System.Collections.Generic;
using SanIsland.Merge;
using UnityEditor;
using UnityEngine;

namespace SanIsland.Merge.Editor
{
    public class InitialBoardEditorWindow : EditorWindow
    {
        const string DragItemIdKey = "SanIsland.Merge.InitialBoardItemId";
        const float CellSize = 72f;
        const float CellGap = 3f;

        InitialBoardDefinition _definition;
        MergeItemDatabase _database;
        BoardVisualConfig _visuals;
        InitialBoardPreviewMode _preview = InitialBoardPreviewMode.Initial;
        int _selectedIndex;
        Vector2 _pickerScroll;
        string _search = string.Empty;
        MergeItemFamily _familyFilter;
        bool _familyFilterEnabled;
        MergeItemKind _kindFilter;
        bool _kindFilterEnabled;
        int _dragItemId = BoardCellState.EmptyItemId;
        readonly List<MergeItemData> _filtered = new List<MergeItemData>(64);

        [MenuItem("Tools/San Island/Initial Board Editor")]
        public static void Open()
        {
            var window = GetWindow<InitialBoardEditorWindow>("Initial Board");
            window.minSize = new Vector2(1100f, 640f);
            window.Show();
        }

        [InitializeOnLoadMethod]
        static void EnsureDefaultAssetOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                EnsureDefaultDefinition();
            };
        }

        [MenuItem("Tools/San Island/Reset To Initial Board")]
        public static void ResetToInitialBoardMenu()
        {
            LoadInitialBoardMenu();
        }

        [MenuItem("Tools/San Island/Load Initial Board")]
        public static void LoadInitialBoardMenu()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Initial Board", "Enter Play Mode first.", "OK");
                return;
            }

            var controller = Object.FindAnyObjectByType<BoardController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Initial Board", "BoardController was not found in the open scenes.", "OK");
                return;
            }

            controller.LoadInitialBoard();
        }

        void OnEnable()
        {
            EnsureAssets();
        }

        void OnGUI()
        {
            EnsureAssets();
            DrawToolbar();
            if (_definition == null || _database == null)
            {
                EditorGUILayout.HelpBox("Assign InitialBoardDefinition and MergeItemDatabase.", MessageType.Error);
                return;
            }

            _definition.EnsureCells();
            EditorGUILayout.BeginHorizontal();
            DrawGrid();
            DrawInspector();
            DrawPicker();
            EditorGUILayout.EndHorizontal();
            HandleDragEvents();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var next = (InitialBoardDefinition)EditorGUILayout.ObjectField(_definition, typeof(InitialBoardDefinition), false);
            if (next != _definition)
            {
                _definition = next;
                _selectedIndex = 0;
            }

            _preview = (InitialBoardPreviewMode)EditorGUILayout.EnumPopup(_preview, GUILayout.Width(180f));
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                ShowValidation();
            }

            if (GUILayout.Button("Reset Layout", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Layout",
                    "Discard unsaved Initial Board edits and reload InitialBoardDefinition from disk?",
                    "Reload",
                    "Cancel"))
                {
                    ReloadDefinitionFromDisk();
                }
            }

            if (GUILayout.Button("Validate Early Progression", EditorStyles.toolbarButton, GUILayout.Width(180f)))
            {
                ShowEarlyProgression();
            }

            if (GUILayout.Button("Clear Board", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                if (EditorUtility.DisplayDialog("Clear Board", "Clear all 60 cells?", "Clear", "Cancel"))
                {
                    Undo.RecordObject(_definition, "Clear Initial Board");
                    _definition.ClearAll();
                    EditorUtility.SetDirty(_definition);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawGrid()
        {
            var width = BoardState.Columns * (CellSize + CellGap) + 24f;
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            EditorGUILayout.LabelField("Board 10×6", EditorStyles.boldLabel);
            var grid = GUILayoutUtility.GetRect(width, BoardState.Rows * (CellSize + CellGap) + 8f);
            for (var row = 0; row < BoardState.Rows; row++)
            {
                for (var col = 0; col < BoardState.Columns; col++)
                {
                    var index = row * BoardState.Columns + col;
                    var rect = new Rect(
                        grid.x + col * (CellSize + CellGap),
                        grid.y + row * (CellSize + CellGap),
                        CellSize,
                        CellSize);
                    DrawCell(rect, index);
                }
            }

            EditorGUILayout.EndVertical();
        }

        void DrawCell(Rect rect, int index)
        {
            var cell = _definition.GetCell(index);
            var preview = InitialBoardPreview.Resolve(cell, _preview);
            var selected = index == _selectedIndex;
            EditorGUI.DrawRect(rect, selected ? new Color(0.25f, 0.42f, 0.62f) : new Color(0.18f, 0.18f, 0.18f));
            GUI.Box(rect, GUIContent.none);

            if (preview.IsBox)
            {
                var box = _visuals != null ? _visuals.GetBoxSprite(preview.BoxVisualIndex) : null;
                DrawSprite(Inset(rect, 6f), box);
                GUI.Label(new Rect(rect.x + 4f, rect.yMax - 18f, rect.width - 8f, 16f), "Box", EditorStyles.miniLabel);
            }
            else if (preview.IsPlaceholder)
            {
                GUI.Label(rect, "Part?", EditorStyles.centeredGreyMiniLabel);
            }
            else if (preview.VisibleItemId != BoardCellState.EmptyItemId &&
                     _database.TryGetById(preview.VisibleItemId, out var item) &&
                     item != null)
            {
                DrawSprite(Inset(rect, preview.IsCobweb ? 10f : 6f), item.Icon);
                if (preview.IsCobweb)
                {
                    var cobweb = _visuals != null ? _visuals.CobwebSprite : null;
                    DrawSprite(rect, cobweb);
                }
            }

            GUI.Label(new Rect(rect.x + 3f, rect.y + 2f, 40f, 16f), index.ToString(), EditorStyles.miniBoldLabel);

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition) && evt.button == 0)
            {
                _selectedIndex = index;
                evt.Use();
                Repaint();
            }

            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && rect.Contains(evt.mousePosition))
            {
                var itemId = GetDragItemId();
                if (itemId != BoardCellState.EmptyItemId)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        ApplyItemToCell(index, itemId);
                    }

                    evt.Use();
                }
            }
        }

        void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280f));
            EditorGUILayout.LabelField("Cell", EditorStyles.boldLabel);
            var cell = _definition.GetCell(_selectedIndex);
            if (cell == null)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("Index", _selectedIndex.ToString());
            cell.SyncAuthoredFlagsFromState();
            EditorGUI.BeginChangeCheck();
            var state = (CellInitialState)EditorGUILayout.EnumPopup("State", cell.state);
            var blockType = (CellBlockType)EditorGUILayout.EnumPopup("Block Type", cell.ResolvedBlockType);
            var locked = EditorGUILayout.Toggle("Locked", cell.IsLocked);
            var cobweb = EditorGUILayout.Toggle("Cobweb", cell.IsCobweb);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_definition, "Change Cell State");
                cell.state = state;
                cell.blockType = blockType;
                cell.locked = locked;
                cell.cobweb = cobweb;
                if (blockType == CellBlockType.Box)
                {
                    cell.state = CellInitialState.Box;
                }

                cell.ApplyAuthoredFlagsToState();
                NormalizeCell(cell);
                cell.SyncAuthoredFlagsFromState();
                EditorUtility.SetDirty(_definition);
            }

            if (cell.state == CellInitialState.Item || cell.state == CellInitialState.Generator || cell.state == CellInitialState.CobwebItem)
            {
                DrawItemField("Content", cell.itemId);
            }

            if (cell.state == CellInitialState.Box)
            {
                EditorGUI.BeginChangeCheck();
                var visual = EditorGUILayout.IntField("Box Visual", cell.boxVisualIndex);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_definition, "Change Box Visual");
                    cell.boxVisualIndex = Mathf.Max(0, visual);
                    EditorUtility.SetDirty(_definition);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Visible: Box", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Hidden", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                var reveal = (BoxRevealType)EditorGUILayout.EnumPopup("Reveal", cell.Hidden.type);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_definition, "Change Box Reveal");
                    cell.Hidden.type = reveal;
                    if (reveal == BoxRevealType.Empty || reveal == BoxRevealType.GeneratorPartPlaceholder)
                    {
                        cell.Hidden.itemId = BoardCellState.EmptyItemId;
                    }

                    EditorUtility.SetDirty(_definition);
                }

                if (reveal == BoxRevealType.Item || reveal == BoxRevealType.CobwebItem || reveal == BoxRevealType.Generator)
                {
                    DrawItemField("Hidden Item", cell.Hidden.itemId);
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Clear Cell"))
            {
                Undo.RecordObject(_definition, "Clear Cell");
                var empty = _definition.GetCell(_selectedIndex);
                empty.state = CellInitialState.Empty;
                empty.itemId = BoardCellState.EmptyItemId;
                empty.blockType = CellBlockType.None;
                empty.locked = false;
                empty.cobweb = false;
                empty.Hidden.type = BoxRevealType.Empty;
                empty.Hidden.itemId = BoardCellState.EmptyItemId;
                empty.SyncAuthoredFlagsFromState();
                EditorUtility.SetDirty(_definition);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawPicker()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Items", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField("Search", _search);
            EditorGUILayout.BeginHorizontal();
            _familyFilterEnabled = EditorGUILayout.ToggleLeft("Family", _familyFilterEnabled, GUILayout.Width(70f));
            using (new EditorGUI.DisabledScope(!_familyFilterEnabled))
            {
                _familyFilter = (MergeItemFamily)EditorGUILayout.EnumPopup(_familyFilter);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            _kindFilterEnabled = EditorGUILayout.ToggleLeft("Kind", _kindFilterEnabled, GUILayout.Width(70f));
            using (new EditorGUI.DisabledScope(!_kindFilterEnabled))
            {
                _kindFilter = (MergeItemKind)EditorGUILayout.EnumPopup(_kindFilter);
            }

            EditorGUILayout.EndHorizontal();
            RebuildFiltered();
            _pickerScroll = EditorGUILayout.BeginScrollView(_pickerScroll);
            for (var i = 0; i < _filtered.Count; i++)
            {
                DrawPickerRow(_filtered[i]);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawPickerRow(MergeItemData item)
        {
            var rect = EditorGUILayout.GetControlRect(false, 36f);
            EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.22f));
            var iconRect = new Rect(rect.x + 4f, rect.y + 2f, 32f, 32f);
            DrawSprite(iconRect, item.Icon);
            GUI.Label(
                new Rect(rect.x + 42f, rect.y + 2f, rect.width - 48f, 16f),
                item.InternalKey,
                EditorStyles.miniBoldLabel);
            GUI.Label(
                new Rect(rect.x + 42f, rect.y + 18f, rect.width - 48f, 16f),
                $"{item.Family}  {item.Kind}  L{item.Level}",
                EditorStyles.miniLabel);

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition) && evt.button == 0)
            {
                if (evt.clickCount >= 2)
                {
                    ApplyItemToCell(_selectedIndex, item.Id);
                    evt.Use();
                }
                else
                {
                    _dragItemId = item.Id;
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData(DragItemIdKey, item.Id);
                    DragAndDrop.objectReferences = new Object[0];
                    DragAndDrop.StartDrag(item.InternalKey);
                    evt.Use();
                }
            }
        }

        void DrawItemField(string label, int itemId)
        {
            MergeItemData item = null;
            if (itemId != BoardCellState.EmptyItemId)
            {
                _database.TryGetById(itemId, out item);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            var iconRect = GUILayoutUtility.GetRect(32f, 32f, GUILayout.Width(32f));
            DrawSprite(iconRect, item != null ? item.Icon : null);
            EditorGUILayout.LabelField(item != null ? item.InternalKey : "(none)");
            EditorGUILayout.EndHorizontal();
            if (item == null && itemId != BoardCellState.EmptyItemId)
            {
                EditorGUILayout.HelpBox($"Unknown id {itemId}. Pick an item from the list.", MessageType.Error);
            }

            GUILayout.Label("Double-click or drag an item from the list.", EditorStyles.miniLabel);
        }

        void ApplyItemToCell(int index, int itemId)
        {
            if (!_database.TryGetById(itemId, out var item) || item == null)
            {
                return;
            }

            var cell = _definition.GetCell(index);
            Undo.RecordObject(_definition, "Place Board Item");
            if (cell.state == CellInitialState.Box)
            {
                cell.Hidden.itemId = itemId;
                cell.Hidden.type = item.Kind == MergeItemKind.Generator
                    ? BoxRevealType.Generator
                    : cell.Hidden.type == BoxRevealType.CobwebItem
                        ? BoxRevealType.CobwebItem
                        : BoxRevealType.Item;
            }
            else
            {
                cell.itemId = itemId;
                cell.state = item.Kind == MergeItemKind.Generator
                    ? CellInitialState.Generator
                    : cell.state == CellInitialState.CobwebItem
                        ? CellInitialState.CobwebItem
                        : CellInitialState.Item;
            }

            EditorUtility.SetDirty(_definition);
            Repaint();
        }

        void ReloadDefinitionFromDisk()
        {
            if (_definition == null)
            {
                _definition = EnsureDefaultDefinition();
                return;
            }

            var path = AssetDatabase.GetAssetPath(_definition);
            if (string.IsNullOrEmpty(path))
            {
                path = InitialBoardDefinition.DefaultAssetPath;
            }

            var fullPath = System.IO.Path.GetFullPath(path);
            if (!System.IO.File.Exists(fullPath))
            {
                _definition = EnsureDefaultDefinition();
                return;
            }

            const string tempPath = "Assets/Data/Boards/_InitialBoardResetTemp.asset";
            var tempFull = System.IO.Path.GetFullPath(tempPath);
            System.IO.File.Copy(fullPath, tempFull, true);
            AssetDatabase.ImportAsset(tempPath, ImportAssetOptions.ForceUpdate);
            var source = AssetDatabase.LoadAssetAtPath<InitialBoardDefinition>(tempPath);
            if (source != null)
            {
                Undo.RegisterCompleteObjectUndo(_definition, "Reset Initial Board Layout");
                EditorUtility.CopySerialized(source, _definition);
                _definition.name = "InitialBoardDefinition";
            }

            AssetDatabase.DeleteAsset(tempPath);
            _definition.EnsureCells();
            _selectedIndex = 0;
            GUI.FocusControl(null);
            Repaint();
        }

        static void NormalizeCell(InitialBoardCellData cell)
        {
            if (cell.state != CellInitialState.Box)
            {
                cell.Hidden.type = BoxRevealType.Empty;
                cell.Hidden.itemId = BoardCellState.EmptyItemId;
            }

            if (cell.state == CellInitialState.Empty || cell.state == CellInitialState.Box)
            {
                cell.itemId = BoardCellState.EmptyItemId;
            }

            cell.SyncAuthoredFlagsFromState();
        }

        void RebuildFiltered()
        {
            _filtered.Clear();
            var items = _database.Items;
            var query = _search != null ? _search.Trim() : string.Empty;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (_familyFilterEnabled && item.Family != _familyFilter)
                {
                    continue;
                }

                if (_kindFilterEnabled && item.Kind != _kindFilter)
                {
                    continue;
                }

                if (query.Length > 0 &&
                    (item.InternalKey == null || item.InternalKey.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                _filtered.Add(item);
            }
        }

        void ShowValidation()
        {
            var result = InitialBoardValidator.Validate(_definition, _database);
            ShowValidationDialog("Initial Board", result);
        }

        void ShowEarlyProgression()
        {
            var production = AssetDatabase.LoadAssetAtPath<GeneratorProductionDatabase>(
                MergeBoardSetupTool.GeneratorProductionDatabasePath);
            var result = InitialBoardEarlyProgression.Validate(_definition, _database, production);
            ShowValidationDialog("Early Progression", result);
        }

        static void ShowValidationDialog(string title, InitialBoardValidationResult result)
        {
            if (result.IsValid && result.Warnings.Count == 0)
            {
                EditorUtility.DisplayDialog(title, "Board is valid.", "OK");
                return;
            }

            EditorUtility.DisplayDialog(title, InitialBoardEarlyProgression.FormatReport(result), "OK");
        }

        void HandleDragEvents()
        {
            if (Event.current.type == EventType.DragExited)
            {
                _dragItemId = BoardCellState.EmptyItemId;
            }
        }

        static int GetDragItemId()
        {
            var data = DragAndDrop.GetGenericData(DragItemIdKey);
            return data is int id ? id : BoardCellState.EmptyItemId;
        }

        static Rect Inset(Rect rect, float pad)
        {
            return new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);
        }

        static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            var tex = sprite.texture;
            var coords = sprite.textureRect;
            var uv = new Rect(coords.x / tex.width, coords.y / tex.height, coords.width / tex.width, coords.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }

        void EnsureAssets()
        {
            if (_database == null)
            {
                _database = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(MergeBoardSetupTool.DatabasePath);
            }

            if (_visuals == null)
            {
                _visuals = AssetDatabase.LoadAssetAtPath<BoardVisualConfig>(MergeBoardSetupTool.VisualConfigPath);
            }

            if (_definition == null)
            {
                _definition = EnsureDefaultDefinition();
            }
        }

        public static InitialBoardDefinition EnsureDefaultDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InitialBoardDefinition>(InitialBoardDefinition.DefaultAssetPath);
            if (existing != null)
            {
                existing.EnsureCells();
                return existing;
            }

            var database = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(MergeBoardSetupTool.DatabasePath);
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Boards");
            var created = CreateInstance<InitialBoardDefinition>();
            created.EnsureCells();
            InitialBoardPlaceholder.Apply(created, database);
            AssetDatabase.CreateAsset(created, InitialBoardDefinition.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
