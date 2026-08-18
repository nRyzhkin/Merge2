using System.Collections.Generic;
using System.IO;
using SanIsland.Merge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge.Editor
{
    public static class MergeBoardSetupTool
    {
        public const string VisualConfigPath = "Assets/Data/BoardVisualConfig.asset";
        public const string DatabasePath = "Assets/Data/MergeItemDatabase.asset";
        public const string AnimationConfigPath = "Assets/Data/BoardItemAnimationConfig.asset";
        public const string DragAnimationConfigPath = "Assets/Data/BoardDragAnimationConfig.asset";
        public const string MergeAnimationConfigPath = "Assets/Data/BoardMergeAnimationConfig.asset";
        public const string CobwebAnimationConfigPath = "Assets/Data/BoardCobwebAnimationConfig.asset";
        public const string BoxAnimationConfigPath = "Assets/Data/BoardBoxAnimationConfig.asset";
        public const string GeneratorAnimationConfigPath = "Assets/Data/BoardGeneratorAnimationConfig.asset";
        public const string GeneratorProductionDatabasePath = "Assets/Data/GeneratorProductionDatabase.asset";
        public const string EconomyConfigPath = "Assets/Data/EconomyConfig.asset";
        public const string IconCatalogPath = "Assets/Data/IconCatalog.asset";
        public const string OrderDatabasePath = "Assets/Data/OrderDatabase.asset";
        public const string CoinPopupPrefabPath = "Assets/Prefabs/Merge/CoinPopup.prefab";
        public const string CoinIconPath = "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Sprites/icon_money_bundle_128.png";
        public const string EnergyIconPath = "Assets/Layer Lab/GUI-LifeGame/ResourcesData/Sprites/icon_energy_lightning_128.png";
        public const string BaseSpritesFolder = "Assets/Sprites/Base";

        [MenuItem("Tools/San Island/Setup Merge Board")]
        public static void SetupFromMenu()
        {
            Setup();
        }

        public static void Setup()
        {
            var controller = FindOrCreateController();
            if (controller.BoardRoot == null)
            {
                var center = FindBoardRootCandidate();
                if (center == null)
                {
                    Debug.LogError("[MergeBoardSetup] Assign BoardController.boardRoot (the Center/GridLayoutGroup with 60 cells) and run again.");
                    return;
                }

                controller.SetBoardRoot(center);
                EditorUtility.SetDirty(controller);
            }

            var visualConfig = EnsureVisualConfig();
            var database = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(DatabasePath);
            if (database == null)
            {
                var guids = AssetDatabase.FindAssets("t:MergeItemDatabase");
                if (guids.Length > 0)
                {
                    database = AssetDatabase.LoadAssetAtPath<MergeItemDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (database == null)
            {
                Debug.LogError("[MergeBoardSetup] MergeItemDatabase asset not found. Run Rebuild Merge Item Database first.");
                return;
            }

            var cells = CollectCellImages(controller.BoardRoot);
            if (cells.Count != BoardState.CellCount)
            {
                Debug.LogError($"[MergeBoardSetup] Expected {BoardState.CellCount} cell Images under '{controller.BoardRoot.name}', found {cells.Count}. Exclude IgnoreLayout backgrounds.");
                return;
            }

            SortCellsVisually(controller.BoardRoot, cells);

            var cellViews = new List<BoardCellView>(BoardState.CellCount);
            var seenIndices = new HashSet<int>();

            for (var i = 0; i < cells.Count; i++)
            {
                var cellImage = cells[i];
                var cellView = cellImage.GetComponent<BoardCellView>();
                if (cellView == null)
                {
                    cellView = Undo.AddComponent<BoardCellView>(cellImage.gameObject);
                }

                EnsureCellLayers(cellView);
                EnsureGeneratorChargeSlider(cellView, i);
                cellView.SetIndex(i);
                EditorUtility.SetDirty(cellView);

                if (!seenIndices.Add(i))
                {
                    Debug.LogError($"[MergeBoardSetup] Duplicate index assignment {i}.");
                }

                ValidateCellView(cellView);
                cellViews.Add(cellView);
            }

            for (var missing = 0; missing < BoardState.CellCount; missing++)
            {
                if (!seenIndices.Contains(missing))
                {
                    Debug.LogError($"[MergeBoardSetup] Missing index {missing}.");
                }
            }

            var boardView = controller.BoardView;
            if (boardView == null)
            {
                boardView = controller.GetComponent<BoardView>();
                if (boardView == null)
                {
                    boardView = Undo.AddComponent<BoardView>(controller.gameObject);
                }

                controller.SetBoardView(boardView);
            }

            MergeItemLocalizationImporter.EnsureUiStringTable();

            boardView.SetCells(cellViews);
            controller.SetItemDatabase(database);
            controller.SetVisualConfig(visualConfig);
            controller.SetAnimationConfig(EnsureAnimationConfig());
            controller.SetUiFeedbackConfig(UiInteractionFeedbackSetupTool.EnsureConfig());
            controller.SetDragAnimationConfig(EnsureDragAnimationConfig());
            controller.SetMergeAnimationConfig(EnsureMergeAnimationConfig());
            controller.SetCobwebAnimationConfig(EnsureCobwebAnimationConfig());
            controller.SetBoxAnimationConfig(EnsureBoxAnimationConfig());
            controller.SetGeneratorAnimationConfig(EnsureGeneratorAnimationConfig());
            controller.SetGeneratorProductionDatabase(EnsureGeneratorProductionDatabase(database));
            WireCellInteraction(cellViews, controller);
            WireHud(controller);
            WireDrag(controller);
            if (controller.BoardView != null && controller.AnimationConfig != null)
            {
                controller.BoardView.BindInteraction(controller, controller.AnimationConfig, controller.UiFeedbackConfig);
            }

            EditorUtility.SetDirty(boardView);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

            Selection.activeObject = controller.gameObject;
            Debug.Log($"[MergeBoardSetup] Setup complete. Wired {cellViews.Count} cells on '{controller.BoardRoot.name}'.");
        }

        static BoardController FindOrCreateController()
        {
            var existing = Object.FindObjectsByType<BoardController>(FindObjectsInactive.Include);
            if (existing.Length > 0)
            {
                return existing[0];
            }

            var root = FindBoardRootCandidate();
            var host = root != null ? root.gameObject : new GameObject("BoardController");
            if (root == null)
            {
                Undo.RegisterCreatedObjectUndo(host, "Create BoardController");
            }

            var controller = host.GetComponent<BoardController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<BoardController>(host);
            }

            if (root != null)
            {
                controller.SetBoardRoot(root);
            }

            return controller;
        }

        static RectTransform FindBoardRootCandidate()
        {
            var grids = Object.FindObjectsByType<GridLayoutGroup>(FindObjectsInactive.Include);
            GridLayoutGroup best = null;
            var bestCount = -1;
            for (var i = 0; i < grids.Length; i++)
            {
                var count = CountParticipatingCellImages(grids[i].transform as RectTransform);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = grids[i];
                }
            }

            if (best != null && bestCount == BoardState.CellCount)
            {
                return best.transform as RectTransform;
            }

            var named = GameObject.Find("Center");
            return named != null ? named.transform as RectTransform : null;
        }

        static List<Image> CollectCellImages(RectTransform boardRoot)
        {
            var result = new List<Image>();
            for (var i = 0; i < boardRoot.childCount; i++)
            {
                var child = boardRoot.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null && layoutElement.ignoreLayout)
                {
                    continue;
                }

                var image = child.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                result.Add(image);
            }

            return result;
        }

        static int CountParticipatingCellImages(RectTransform boardRoot)
        {
            return boardRoot == null ? 0 : CollectCellImages(boardRoot).Count;
        }

        static void SortCellsVisually(RectTransform boardRoot, List<Image> cells)
        {
            if (boardRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(boardRoot);
            }

            Canvas.ForceUpdateCanvases();

            cells.Sort((a, b) =>
            {
                var pa = a.rectTransform.anchoredPosition;
                var pb = b.rectTransform.anchoredPosition;
                var row = pb.y.CompareTo(pa.y);
                if (row != 0)
                {
                    return row;
                }

                var col = pa.x.CompareTo(pb.x);
                if (col != 0)
                {
                    return col;
                }

                return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
            });
        }

        static void EnsureCellLayers(BoardCellView cellView)
        {
            var item = EnsureImageChild(cellView.transform, BoardCellView.ItemImageName, 0);
            var blocker = EnsureImageChild(cellView.transform, BoardCellView.BlockerImageName, 1);
            var lockOverlay = EnsureImageChild(cellView.transform, BoardCellView.LockOverlayImageName, 2);
            var fx = EnsureRectChild(cellView.transform, BoardCellView.FxRootName, 3);

            item.raycastTarget = false;
            item.preserveAspect = false;
            blocker.raycastTarget = false;
            blocker.preserveAspect = false;
            lockOverlay.raycastTarget = false;
            lockOverlay.preserveAspect = false;

            item.gameObject.SetActive(false);
            blocker.gameObject.SetActive(false);
            lockOverlay.gameObject.SetActive(false);

            var animator = cellView.GetComponent<BoardItemAnimator>();
            if (animator == null)
            {
                animator = Undo.AddComponent<BoardItemAnimator>(cellView.gameObject);
            }

            cellView.BindAnimator(animator);

            var pointer = cellView.GetComponent<BoardCellPointer>();
            if (pointer == null)
            {
                Undo.AddComponent<BoardCellPointer>(cellView.gameObject);
            }

            var image = cellView.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }

            cellView.BindLayers(item, blocker, lockOverlay, fx);
        }

        static void EnsureGeneratorChargeSlider(BoardCellView cellView, int cellIndex)
        {
            if (cellView == null)
            {
                return;
            }

            var existing = cellView.transform.Find(GeneratorChargeIndicator.SliderObjectName);
            if (existing == null)
            {
                Debug.LogWarning($"[MergeBoardSetup] Slider_02_Orange is missing on cell {cellIndex}.");
                return;
            }

            var slider = existing.GetComponent<Slider>();
            if (slider == null)
            {
                Debug.LogWarning($"[MergeBoardSetup] Slider_02_Orange on cell {cellIndex} has no Slider component.");
                return;
            }

            var indicator = cellView.GetComponent<GeneratorChargeIndicator>();
            if (indicator == null)
            {
                indicator = Undo.AddComponent<GeneratorChargeIndicator>(cellView.gameObject);
            }

            indicator.Bind(slider);
            cellView.BindGeneratorChargeIndicator(indicator);
        }

        static void WireCellInteraction(List<BoardCellView> cellViews, BoardController controller)
        {
            var config = controller.AnimationConfig;
            var hoverConfig = controller.UiFeedbackConfig;
            for (var i = 0; i < cellViews.Count; i++)
            {
                var cell = cellViews[i];
                var animator = cell.ItemAnimator != null ? cell.ItemAnimator : cell.GetComponent<BoardItemAnimator>();
                if (animator != null && cell.ItemImage != null)
                {
                    animator.Configure(config, cell.ItemImage.rectTransform, cell.transform as RectTransform, hoverConfig);
                    cell.BindAnimator(animator);
                }

                var pointer = cell.GetComponent<BoardCellPointer>();
                if (pointer != null)
                {
                    pointer.Configure(cell, controller, animator);
                }
            }
        }

        static void WireHud(BoardController controller)
        {
            var selectionView = controller.GetComponent<BoardSelectionView>();
            if (selectionView == null)
            {
                selectionView = Undo.AddComponent<BoardSelectionView>(controller.gameObject);
            }

            var selectionBack = FindNamedTransform("SelectionBack");
            var selectionFront = FindNamedTransform("SelectionFront");
            if (selectionBack == null || selectionFront == null)
            {
                Debug.LogError("[MergeBoardSetup] SelectionBack/SelectionFront were not found in the scene.");
            }
            else
            {
                selectionView.Bind(selectionBack, selectionFront);
            }

            controller.SetSelectionView(selectionView);

            var itemInfoRoot = FindNamedTransform("Item Info");
            if (itemInfoRoot == null)
            {
                Debug.LogError("[MergeBoardSetup] Item Info was not found in the scene.");
                return;
            }

            var itemInfoView = itemInfoRoot.GetComponent<ItemInfoView>();
            if (itemInfoView == null)
            {
                itemInfoView = Undo.AddComponent<ItemInfoView>(itemInfoRoot.gameObject);
            }

            var chainRoot = itemInfoRoot.Find("Chain Info") as RectTransform;
            MergeChainInfoView chainView = null;
            if (chainRoot != null)
            {
                chainView = chainRoot.GetComponent<MergeChainInfoView>();
                if (chainView == null)
                {
                    chainView = Undo.AddComponent<MergeChainInfoView>(chainRoot.gameObject);
                }

                WireChainTemplates(chainRoot, chainView);
            }

            var generatorIcon = FindChildImage(itemInfoRoot, "Generator/Icon");
            var itemIcon = FindChildImage(itemInfoRoot, "Item");
            var nameText = itemInfoRoot.Find("Name") != null
                ? itemInfoRoot.Find("Name").GetComponent<TMPro.TMP_Text>()
                : null;
            var levelLabel = itemInfoRoot.Find("Lvl Label");
            var levelText = levelLabel != null ? levelLabel.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            itemInfoView.Bind(generatorIcon, itemIcon, nameText, levelText, chainView);
            itemInfoView.gameObject.SetActive(true);
            controller.SetItemInfoView(itemInfoView);
            EditorUtility.SetDirty(itemInfoView);
            if (chainView != null)
            {
                EditorUtility.SetDirty(chainView);
            }

            EditorUtility.SetDirty(selectionView);
            WireEnergy(controller);
            WireIcons(controller);
            WireEconomy(controller);
            WireOrders(controller);
        }

        static void WireEnergy(BoardController controller)
        {
            var energySystem = controller.GetComponent<EnergySystem>();
            if (energySystem == null)
            {
                energySystem = Undo.AddComponent<EnergySystem>(controller.gameObject);
            }

            EditorUtility.SetDirty(energySystem);

            var roots = FindAllHudEnergyRoots();
            if (roots.Count == 0)
            {
                Debug.LogWarning("[MergeBoardSetup] No Resource_Energy widgets found. Add EnergyHudView on each energy widget so it binds its own children.");
                return;
            }

            for (var i = 0; i < roots.Count; i++)
            {
                BindEnergyHud(roots[i]);
            }
        }

        static void BindEnergyHud(RectTransform energyRoot)
        {
            if (energyRoot == null)
            {
                return;
            }

            var hud = energyRoot.GetComponent<EnergyHudView>();
            if (hud == null)
            {
                hud = Undo.AddComponent<EnergyHudView>(energyRoot.gameObject);
            }

            hud.BindLocal();
            EditorUtility.SetDirty(hud);
        }

        static List<RectTransform> FindAllHudEnergyRoots()
        {
            var transforms = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            var roots = new List<RectTransform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == EnergyHudView.EnergyRootName)
                {
                    roots.Add(transforms[i]);
                }
            }

            return roots;
        }

        static void WireEconomy(BoardController controller)
        {
            var economy = EnsureEconomyConfig();
            var currency = controller.GetComponent<CurrencySystem>();
            if (currency == null)
            {
                currency = Undo.AddComponent<CurrencySystem>(controller.gameObject);
            }

            EditorUtility.SetDirty(currency);

            var sell = controller.GetComponent<SellSystem>();
            if (sell == null)
            {
                sell = Undo.AddComponent<SellSystem>(controller.gameObject);
            }

            sell.Configure(controller, economy);
            EditorUtility.SetDirty(sell);

            var presenter = controller.GetComponent<BoardSellPresenter>();
            if (presenter == null)
            {
                presenter = Undo.AddComponent<BoardSellPresenter>(controller.gameObject);
            }

            presenter.Configure(controller, controller.DragView, economy, LoadCoinPopupPrefab());
            EditorUtility.SetDirty(presenter);

            var coinRoots = FindNamedHudRoots(CoinHudView.CoinRootName);
            if (coinRoots.Count == 0)
            {
                Debug.LogWarning("[MergeBoardSetup] No Resource_Coin widgets found. Add CoinHudView on each coin widget so it binds its own children.");
            }
            else
            {
                for (var i = 0; i < coinRoots.Count; i++)
                {
                    var hud = coinRoots[i].GetComponent<CoinHudView>();
                    if (hud == null)
                    {
                        hud = Undo.AddComponent<CoinHudView>(coinRoots[i].gameObject);
                    }

                    hud.BindLocal();
                    EditorUtility.SetDirty(hud);
                }
            }

            var sellRoot = FindNamedTransform(SellButtonView.SellRootName);
            if (sellRoot == null)
            {
                Debug.LogWarning("[MergeBoardSetup] Sell button was not found. Scene already expected to contain it.");
                return;
            }

            var sellButton = sellRoot.GetComponent<SellButtonView>();
            if (sellButton == null)
            {
                sellButton = Undo.AddComponent<SellButtonView>(sellRoot.gameObject);
            }

            sellButton.BindLocal();
            EditorUtility.SetDirty(sellButton);
        }

        static void WireOrders(BoardController controller)
        {
            var database = EnsureOrderDatabase(controller.ItemDatabase);
            var orders = controller.GetComponent<OrderSystem>();
            if (orders == null)
            {
                orders = Undo.AddComponent<OrderSystem>(controller.gameObject);
            }

            orders.Configure(controller, database);
            EditorUtility.SetDirty(orders);

            var hudRoot = FindNamedTransform(OrdersHudView.OrdersRootName);
            OrdersHudView hud = null;
            if (hudRoot == null)
            {
                Debug.LogWarning("[MergeBoardSetup] Group_Orders was not found. Scene already expected to contain the designer Order UI.");
            }
            else
            {
                hud = hudRoot.GetComponent<OrdersHudView>();
                if (hud == null)
                {
                    hud = Undo.AddComponent<OrdersHudView>(hudRoot.gameObject);
                }

                for (var i = 0; i < hudRoot.childCount; i++)
                {
                    var child = hudRoot.GetChild(i);
                    if (child == null || !child.name.StartsWith("Order"))
                    {
                        continue;
                    }

                    var card = child.GetComponent<OrderCardView>();
                    if (card == null)
                    {
                        card = Undo.AddComponent<OrderCardView>(child.gameObject);
                    }

                    if (child.GetComponent<Button>() == null)
                    {
                        var button = Undo.AddComponent<Button>(child.gameObject);
                        button.transition = Selectable.Transition.None;
                    }

                    var nestedComplete = child.GetComponentsInChildren<Button>(true);
                    for (var b = 0; b < nestedComplete.Length; b++)
                    {
                        if (nestedComplete[b] != null && nestedComplete[b].gameObject != child.gameObject)
                        {
                            nestedComplete[b].enabled = false;
                        }
                    }

                    card.BindLocal();
                    EditorUtility.SetDirty(card);
                }

                hud.BindLocal();
                EditorUtility.SetDirty(hud);
            }

            var presenter = controller.GetComponent<BoardOrderPresenter>();
            if (presenter == null)
            {
                presenter = Undo.AddComponent<BoardOrderPresenter>(controller.gameObject);
            }

            presenter.Configure(controller, controller.DragView, database, LoadCoinPopupPrefab(), hud);
            EditorUtility.SetDirty(presenter);

            var markers = controller.GetComponent<BoardOrderMarkerView>();
            if (markers == null)
            {
                markers = Undo.AddComponent<BoardOrderMarkerView>(controller.gameObject);
            }

            var ordered = FindNamedTransform(BoardOrderMarkerView.MarkerName);
            if (ordered == null)
            {
                Debug.LogWarning("[MergeBoardSetup] Ordered was not found. Ready-order cell markers will be skipped until the designer object exists.");
            }
            else
            {
                markers.Bind(ordered);
                EditorUtility.SetDirty(markers);
            }
        }

        static OrderDatabase EnsureOrderDatabase(MergeItemDatabase items)
        {
            EnsureFolder("Assets/Data");
            var database = AssetDatabase.LoadAssetAtPath<OrderDatabase>(OrderDatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<OrderDatabase>();
                AssetDatabase.CreateAsset(database, OrderDatabasePath);
            }

            database.EnsureDevelopmentOrders(items);
            EditorUtility.SetDirty(database);
            return database;
        }

        static List<RectTransform> FindNamedHudRoots(string objectName)
        {
            var transforms = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            var roots = new List<RectTransform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                {
                    roots.Add(transforms[i]);
                }
            }

            return roots;
        }

        static void WireIcons(BoardController controller)
        {
            var catalog = EnsureIconCatalog();
            var icons = controller.GetComponent<IconSystem>();
            if (icons == null)
            {
                icons = Undo.AddComponent<IconSystem>(controller.gameObject);
            }

            icons.Configure(catalog);
            EditorUtility.SetDirty(icons);
        }

        static EconomyConfig EnsureEconomyConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EconomyConfig>();
                AssetDatabase.CreateAsset(config, EconomyConfigPath);
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        static IconCatalog EnsureIconCatalog()
        {
            EnsureFolder("Assets/Data");
            var catalog = AssetDatabase.LoadAssetAtPath<IconCatalog>(IconCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<IconCatalog>();
                AssetDatabase.CreateAsset(catalog, IconCatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            EnsureIconEntry(entries, IconTokens.Coin, CoinIconPath);
            EnsureIconEntry(entries, IconTokens.Energy, EnergyIconPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.RebuildLookups();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static void EnsureIconEntry(SerializedProperty entries, string token, string spritePath)
        {
            if (entries == null || string.IsNullOrEmpty(token))
            {
                return;
            }

            for (var i = 0; i < entries.arraySize; i++)
            {
                var existing = entries.GetArrayElementAtIndex(i).FindPropertyRelative("token");
                if (existing != null && existing.stringValue == token)
                {
                    return;
                }
            }

            var index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            var entry = entries.GetArrayElementAtIndex(index);
            var tokenProperty = entry.FindPropertyRelative("token");
            var spriteProperty = entry.FindPropertyRelative("sprite");
            if (tokenProperty != null)
            {
                tokenProperty.stringValue = token;
            }

            if (spriteProperty != null)
            {
                spriteProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }
        }

        static CoinPopupView LoadCoinPopupPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<CoinPopupView>(CoinPopupPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[MergeBoardSetup] CoinPopup prefab was not found at '{CoinPopupPrefabPath}'.");
            }

            return prefab;
        }

        static void WireChainTemplates(RectTransform chainRoot, MergeChainInfoView chainView)
        {
            MergeChainItemView firstItem = null;
            RectTransform firstArrow = null;
            for (var i = 0; i < chainRoot.childCount; i++)
            {
                var child = chainRoot.GetChild(i);
                if (child.name.IndexOf("arrow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (firstArrow == null)
                    {
                        firstArrow = child as RectTransform;
                    }

                    continue;
                }

                var itemView = child.GetComponent<MergeChainItemView>();
                if (itemView == null)
                {
                    itemView = Undo.AddComponent<MergeChainItemView>(child.gameObject);
                }

                var icon = child.Find("Icon") != null ? child.Find("Icon").GetComponent<Image>() : null;
                var selection = child.Find("Selected") != null ? child.Find("Selected").gameObject : child.Find("Selection") != null ? child.Find("Selection").gameObject : null;
                var locked = child.Find("Locked") != null ? child.Find("Locked").gameObject : null;
                itemView.BindReferences(icon, selection, locked);
                EditorUtility.SetDirty(itemView);
                if (firstItem == null)
                {
                    firstItem = itemView;
                }
            }

            var layout = chainRoot.GetComponent<HorizontalLayoutGroup>();
            chainView.Bind(chainRoot, firstItem, firstArrow, layout);
        }

        static void WireDrag(BoardController controller)
        {
            var canvas = controller.BoardRoot != null
                ? controller.BoardRoot.GetComponentInParent<Canvas>()
                : controller.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[MergeBoardSetup] Canvas was not found for BoardDragLayer.");
                return;
            }

            var dragView = canvas.GetComponentInChildren<BoardDragView>(true);
            if (dragView == null)
            {
                dragView = BoardDragView.Ensure(canvas);
                Undo.RegisterCreatedObjectUndo(dragView.gameObject, "Create BoardDragLayer");
            }
            else
            {
                dragView.EnsureChildren();
            }

            controller.SetDragView(dragView);
            controller.SetDragAnimationConfig(EnsureDragAnimationConfig());
            controller.SetMergeAnimationConfig(EnsureMergeAnimationConfig());
            controller.SetCobwebAnimationConfig(EnsureCobwebAnimationConfig());
            controller.SetBoxAnimationConfig(EnsureBoxAnimationConfig());
            controller.SetGeneratorAnimationConfig(EnsureGeneratorAnimationConfig());
            controller.SetGeneratorProductionDatabase(EnsureGeneratorProductionDatabase(controller.ItemDatabase));

            var presenter = controller.GetComponent<BoardMergePresenter>();
            if (presenter == null)
            {
                presenter = Undo.AddComponent<BoardMergePresenter>(controller.gameObject);
            }

            presenter.Configure(controller, dragView, controller.MergeAnimationConfig);

            var cobwebPresenter = controller.GetComponent<BoardCobwebPresenter>();
            if (cobwebPresenter == null)
            {
                cobwebPresenter = Undo.AddComponent<BoardCobwebPresenter>(controller.gameObject);
            }

            cobwebPresenter.Configure(
                controller,
                dragView,
                controller.CobwebAnimationConfig,
                controller.DragAnimationConfig);

            var boxRevealPresenter = controller.GetComponent<BoardBoxRevealPresenter>();
            if (boxRevealPresenter == null)
            {
                boxRevealPresenter = Undo.AddComponent<BoardBoxRevealPresenter>(controller.gameObject);
            }

            boxRevealPresenter.Configure(controller, controller.BoxAnimationConfig);

            var generatorPresenter = controller.GetComponent<BoardGeneratorPresenter>();
            if (generatorPresenter == null)
            {
                generatorPresenter = Undo.AddComponent<BoardGeneratorPresenter>(controller.gameObject);
            }

            generatorPresenter.Configure(
                controller,
                dragView,
                controller.GeneratorAnimationConfig,
                controller.MergeAnimationConfig);

            var displacePresenter = controller.GetComponent<BoardDisplacePresenter>();
            if (displacePresenter == null)
            {
                displacePresenter = Undo.AddComponent<BoardDisplacePresenter>(controller.gameObject);
            }

            displacePresenter.Configure(controller, dragView, controller.DragAnimationConfig);

            WireMessages(controller);

            var dragController = controller.GetComponent<BoardDragController>();
            if (dragController == null)
            {
                dragController = Undo.AddComponent<BoardDragController>(controller.gameObject);
            }

            dragController.Configure(controller, controller.DragAnimationConfig, dragView);
            EditorUtility.SetDirty(dragView);
            EditorUtility.SetDirty(dragController);
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(cobwebPresenter);
            EditorUtility.SetDirty(boxRevealPresenter);
            EditorUtility.SetDirty(generatorPresenter);
            EditorUtility.SetDirty(displacePresenter);
            EditorUtility.SetDirty(controller);
        }

        static void WireMessages(BoardController controller)
        {
            var canvas = controller.BoardRoot != null
                ? controller.BoardRoot.GetComponentInParent<Canvas>()
                : controller.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[MergeBoardSetup] Canvas not found for Messages Layer.");
                return;
            }

            var layer = FindNamedTransform("Messages Layer");
            if (layer == null)
            {
                Debug.LogWarning("[MergeBoardSetup] Messages Layer not found.");
                return;
            }

            var messagePresenter = layer.GetComponent<MessagePresenter>();
            if (messagePresenter == null)
            {
                messagePresenter = Undo.AddComponent<MessagePresenter>(layer.gameObject);
            }

            var white = layer.Find("ToastMessage_White") as RectTransform;
            var rose = layer.Find("ToastMessage_Rose") as RectTransform;
            messagePresenter.Configure(
                layer,
                white,
                rose,
                controller.GeneratorAnimationConfig,
                canvas);
            controller.SetMessagePresenter(messagePresenter);
            EditorUtility.SetDirty(messagePresenter);
        }

        static RectTransform FindNamedTransform(string objectName)
        {
            var transforms = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        static Image FindChildImage(Transform root, string path)
        {
            var child = root.Find(path);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static BoardItemAnimationConfig EnsureAnimationConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<BoardItemAnimationConfig>(AnimationConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardItemAnimationConfig>();
                AssetDatabase.CreateAsset(config, AnimationConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        static BoardDragAnimationConfig EnsureDragAnimationConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<BoardDragAnimationConfig>(DragAnimationConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardDragAnimationConfig>();
                AssetDatabase.CreateAsset(config, DragAnimationConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        static BoardMergeAnimationConfig EnsureMergeAnimationConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<BoardMergeAnimationConfig>(MergeAnimationConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardMergeAnimationConfig>();
                AssetDatabase.CreateAsset(config, MergeAnimationConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        static BoardCobwebAnimationConfig EnsureCobwebAnimationConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<BoardCobwebAnimationConfig>(CobwebAnimationConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardCobwebAnimationConfig>();
                AssetDatabase.CreateAsset(config, CobwebAnimationConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        static BoardBoxAnimationConfig EnsureBoxAnimationConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<BoardBoxAnimationConfig>(BoxAnimationConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardBoxAnimationConfig>();
                AssetDatabase.CreateAsset(config, BoxAnimationConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        static BoardGeneratorAnimationConfig EnsureGeneratorAnimationConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<BoardGeneratorAnimationConfig>(GeneratorAnimationConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardGeneratorAnimationConfig>();
                AssetDatabase.CreateAsset(config, GeneratorAnimationConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        static GeneratorProductionDatabase EnsureGeneratorProductionDatabase(MergeItemDatabase itemDatabase)
        {
            var database = GeneratorProductionDatabaseTools.Rebuild();
            if (database == null)
            {
                EnsureFolder("Assets/Data");
                database = AssetDatabase.LoadAssetAtPath<GeneratorProductionDatabase>(GeneratorProductionDatabasePath);
            }

            return database;
        }

        static Image EnsureImageChild(Transform parent, string childName, int siblingIndex)
        {
            var child = parent.Find(childName) as RectTransform;
            Image image;
            if (child == null)
            {
                var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
                child = go.GetComponent<RectTransform>();
                child.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            else
            {
                image = child.GetComponent<Image>();
                if (image == null)
                {
                    image = Undo.AddComponent<Image>(child.gameObject);
                }
            }

            StretchFull(child);
            child.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
            return image;
        }

        static RectTransform EnsureRectChild(Transform parent, string childName, int siblingIndex)
        {
            var child = parent.Find(childName) as RectTransform;
            if (child == null)
            {
                var go = new GameObject(childName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
                child = go.GetComponent<RectTransform>();
                child.SetParent(parent, false);
            }

            StretchFull(child);
            child.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
            return child;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        static void ValidateCellView(BoardCellView cellView)
        {
            if (cellView.ItemImage == null || cellView.BlockerImage == null || cellView.LockOverlayImage == null || cellView.FxRoot == null)
            {
                Debug.LogError($"[MergeBoardSetup] Missing layers on '{cellView.name}'.");
                return;
            }

            if (cellView.ItemImage.raycastTarget || cellView.BlockerImage.raycastTarget || cellView.LockOverlayImage.raycastTarget)
            {
                Debug.LogError($"[MergeBoardSetup] Overlay images on '{cellView.name}' must have raycastTarget=false.");
            }
        }

        static BoardVisualConfig EnsureVisualConfig()
        {
            EnsureFolder("Assets/Data");
            var config = AssetDatabase.LoadAssetAtPath<BoardVisualConfig>(VisualConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardVisualConfig>();
                AssetDatabase.CreateAsset(config, VisualConfigPath);
            }

            var boxes = new List<Sprite>();
            TryLoadSprite($"{BaseSpritesFolder}/Blocker_Box_A.png", boxes);
            TryLoadSprite($"{BaseSpritesFolder}/Blocker_Box_B.png", boxes);
            var cobweb = AssetDatabase.LoadAssetAtPath<Sprite>($"{BaseSpritesFolder}/Cobweb_A.png");

            if (boxes.Count == 0)
            {
                Debug.LogWarning("[MergeBoardSetup] No box sprites found in Assets/Sprites/Base. Assign them on BoardVisualConfig.");
            }

            if (cobweb == null)
            {
                Debug.LogWarning("[MergeBoardSetup] Cobweb_A sprite not found. Assign cobwebSprite on BoardVisualConfig.");
            }

            var needsWrite = false;
            var nextBoxes = config.BoxSprites.Count > 0
                ? new List<Sprite>(config.BoxSprites)
                : boxes;
            if (config.BoxSprites.Count == 0 && boxes.Count > 0)
            {
                needsWrite = true;
            }

            var nextCobweb = config.CobwebSprite != null ? config.CobwebSprite : cobweb;
            if (config.CobwebSprite == null && cobweb != null)
            {
                needsWrite = true;
            }

            if (needsWrite)
            {
                config.EditorSetVisuals(nextBoxes, nextCobweb);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        static void TryLoadSprite(string path, List<Sprite> target)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                target.Add(sprite);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
