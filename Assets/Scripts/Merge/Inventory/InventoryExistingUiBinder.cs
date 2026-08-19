using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public static class InventoryExistingUiBinder
    {
        const string InventoryWindowName = "Inventory";
        const string InventorySlotsGroupName = "Group_Equipment";
        const string HudBagParentName = "Group_Left";
        const string WindowBagParentName = "Panel_Left";
        const string ItemDetailName = "ItemDetail";
        const string ItemDetailButtonName = "ItemDetailButton";
        const string OrdersRootName = "Group_Orders";
        const string SellName = "Sell";

        public static void Bind(BoardController controller, ItemInfoView itemInfoView, bool force = false)
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (controller == null)
            {
                Debug.LogError("[TASK17C] Binding failed: BoardController is missing.");
                return;
            }

            var inventoryRoot = FindInventoryWindow();
            var itemDetailRoot = FindNamed(ItemDetailName);
            var hudBag = FindHudBagButton();
            var windowBag = FindWindowBagButton(inventoryRoot);
            var slotsGroup = inventoryRoot != null
                ? FindDescendant(inventoryRoot, InventorySlotsGroupName)
                : null;
            var detailsButton = FindNamed(ItemDetailButtonName);
            var sell = FindNamed(SellName);
            var orders = FindNamed(OrdersRootName);

            ReportMissing("Inventory", inventoryRoot);
            ReportMissing("ItemDetail", itemDetailRoot);
            ReportMissing("HUD Bag button (Group_Left/Inventory)", hudBag);
            ReportMissing("Inventory close Bag (Panel_Left/Inventory)", windowBag);
            ReportMissing("Inventory slots Group_Equipment", slotsGroup);
            ReportMissing("Sell", sell);
            ReportMissing("Orders Group_Orders", orders);

            if (detailsButton == null)
            {
                Debug.LogError("Existing ItemInfo Details button not found. Designer placement required.");
            }

            var slotViews = WireInventorySlots(slotsGroup);
            var chainNodes = 0;
            UIWindow inventoryWindow = null;
            InventoryView inventoryView = null;

                if (inventoryRoot != null)
            {
                EnsureCanvasGroup(inventoryRoot.gameObject);
                inventoryWindow = EnsureComponent<UIWindow>(inventoryRoot.gameObject);
                inventoryWindow.BindRoots(inventoryRoot, inventoryRoot.GetComponent<CanvasGroup>());
                inventoryView = EnsureComponent<InventoryView>(inventoryRoot.gameObject);
                var panel = FindChild(inventoryRoot, WindowBagParentName);
                if (panel != null)
                {
                    var panelGroup = EnsureCanvasGroup(panel.gameObject);
                    panelGroup.ignoreParentGroups = true;
                    EnsureAnimated(panel.gameObject, UIAnimationRole.Left, 0);
                }

                if (slotsGroup != null)
                {
                    EnsureAnimated(slotsGroup.gameObject, UIAnimationRole.ListContainer, 30)
                        .Configure(UIAnimationRole.ListContainer, 30, slotsGroup);
                }

                inventoryWindow.EnsureOwnDimmer(Object.FindAnyObjectByType<UIManager>()?.DefaultWindowChoreographyConfig);
                inventoryWindow.Choreography.Invalidate();
            }

            var inventory = EnsureComponent<InventoryController>(controller.gameObject);
            inventory.Configure(controller.ItemDatabase);

            var flight = EnsureComponent<InventoryFlightPresenter>(controller.gameObject);
            flight.Configure(controller, controller.DragView, controller.GeneratorAnimationConfig, controller.GeneratorPresenter);

            var bridge = EnsureComponent<InventoryBoardBridge>(controller.gameObject);
            bridge.Configure(inventory, controller, inventoryView, flight);

            if (inventoryView != null)
            {
                inventoryView.Bind(inventory, slotViews, inventoryWindow);
                RebindSlotOwners(slotViews, inventoryView);
            }

            if (hudBag != null && inventoryWindow != null)
            {
                var open = EnsureComponent<UIOpenWindowButton>(hudBag.gameObject);
                open.Bind(inventoryWindow, hudBag.GetComponent<Button>());
            }

            if (windowBag != null && inventoryWindow != null)
            {
                var close = EnsureComponent<UICloseWindowButton>(windowBag.gameObject);
                close.Bind(inventoryWindow, windowBag.GetComponent<Button>());
            }

            ItemDetailWindow detailContent = null;
            if (itemDetailRoot != null)
            {
                EnsureCanvasGroup(itemDetailRoot.gameObject);
                var itemDetailWindow = EnsureComponent<UIWindow>(itemDetailRoot.gameObject);
                itemDetailWindow.BindRoots(itemDetailRoot, itemDetailRoot.GetComponent<CanvasGroup>());
                itemDetailWindow.EnsureOwnDimmer(Object.FindAnyObjectByType<UIManager>()?.DefaultWindowChoreographyConfig);

                detailContent = WireItemDetailContent(itemDetailRoot, out chainNodes);
                itemDetailWindow.Choreography.Invalidate();
                var closeButton = FindChild(itemDetailRoot, "Button_Close");
                if (closeButton != null)
                {
                    var close = EnsureComponent<UICloseWindowButton>(closeButton.gameObject);
                    close.Bind(itemDetailWindow, closeButton.GetComponent<Button>());
                }
                else
                {
                    Debug.LogError("[TASK17C] Binding failed: ItemDetail/Button_Close not found.");
                }
            }

            if (itemInfoView != null)
            {
                var details = detailsButton != null ? detailsButton.GetComponent<Button>() : null;
                itemInfoView.BindDetails(details, detailContent);
            }

            WireDropTargets(controller, hudBag, sell, orders);
            DisableSharedBlockerDimmer();
            PrintReport(inventoryRoot, itemDetailRoot, hudBag, windowBag, slotViews, chainNodes, detailsButton, sell, orders);
        }

        static InventorySlotView[] WireInventorySlots(RectTransform slotsGroup)
        {
            if (slotsGroup == null)
            {
                return new InventorySlotView[0];
            }

            var views = new List<InventorySlotView>(InventoryState.SlotCount);
            for (var i = 0; i < slotsGroup.childCount; i++)
            {
                var child = slotsGroup.GetChild(i);
                if (child == null || !child.name.StartsWith("Slot"))
                {
                    continue;
                }

                var view = EnsureComponent<InventorySlotView>(child.gameObject);
                var locked = FindChild(child, "Locked");
                var offer = FindChild(child, "Offer");
                view.Bind(
                    null,
                    views.Count,
                    null,
                    null,
                    locked != null ? locked.gameObject : null,
                    offer != null ? offer.gameObject : null);
                views.Add(view);
            }

            if (views.Count != InventoryState.SlotCount)
            {
                Debug.LogError($"[TASK17C] Binding failed: expected {InventoryState.SlotCount} Inventory slots under Group_Equipment, found {views.Count}.");
            }

            return views.ToArray();
        }

        static void RebindSlotOwners(InventorySlotView[] slots, InventoryView owner)
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                var locked = FindChild(slots[i].transform, "Locked");
                var offer = FindChild(slots[i].transform, "Offer");
                slots[i].Bind(
                    owner,
                    i,
                    null,
                    null,
                    locked != null ? locked.gameObject : null,
                    offer != null ? offer.gameObject : null);
            }
        }

        static ItemDetailWindow WireItemDetailContent(RectTransform itemDetailRoot, out int nodeCount)
        {
            nodeCount = 0;
            var detail = EnsureComponent<ItemDetailWindow>(itemDetailRoot.gameObject);
            var itemIcon = FindChildImage(itemDetailRoot, "Item/Icon");
            var nameText = FindChildText(itemDetailRoot, "Text_Title");
            var description = FindChildText(itemDetailRoot, "Text (TMP) (1)");
            var chainRoot = FindChild(itemDetailRoot, "Scroll View/Viewport/Content");
            if (itemIcon == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail/Item/Icon not found.");
            }

            if (nameText == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail/Text_Title not found.");
            }

            if (description == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail/Text (TMP) (1) not found.");
            }

            if (chainRoot == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail/Scroll View/Viewport/Content not found.");
            }

            var title = FindChild(itemDetailRoot, "Text_Title");
            if (title != null)
            {
                EnsureAnimated(title.gameObject, UIAnimationRole.Top, 10);
            }

            var grade = FindChild(itemDetailRoot, "Label_Grade");
            if (grade != null)
            {
                EnsureAnimated(grade.gameObject, UIAnimationRole.FadeScale, 15);
            }

            var item = FindChild(itemDetailRoot, "Item");
            if (item != null)
            {
                EnsureAnimated(item.gameObject, UIAnimationRole.FadeScale, 20);
            }

            GridLayoutGroup grid = null;
            ItemDetailChainView chainView = null;
            ItemDetailChainNode firstNode = null;
            if (chainRoot != null)
            {
                grid = chainRoot.GetComponent<GridLayoutGroup>();
                if (grid == null)
                {
                    Debug.LogError("[TASK17C] Binding failed: ItemDetail chain Content has no existing GridLayoutGroup.");
                }

                EnsureAnimated(chainRoot.gameObject, UIAnimationRole.ListContainer, 30)
                    .Configure(UIAnimationRole.ListContainer, 30, chainRoot);
                chainView = EnsureComponent<ItemDetailChainView>(chainRoot.gameObject);
                for (var i = 0; i < chainRoot.childCount; i++)
                {
                    var child = chainRoot.GetChild(i);
                    if (child == null || !child.name.StartsWith("Item"))
                    {
                        continue;
                    }

                    var node = EnsureComponent<ItemDetailChainNode>(child.gameObject);
                    var icon = FindChildImage(child, "Icon");
                    var selected = FindChild(child, "Selected");
                    var locked = FindChild(child, "Locked");
                    var arrow = FindChild(child, "arrow") ?? FindChild(child, "Arrow");
                    node.Bind(
                        icon,
                        selected != null ? selected.gameObject : null,
                        locked != null ? locked.gameObject : null,
                        arrow != null ? arrow.gameObject : null);
                    if (firstNode == null)
                    {
                        firstNode = node;
                    }

                    nodeCount++;
                }

                chainView.Bind(chainRoot, firstNode, grid);
            }

            var levelValue = FindChildText(itemDetailRoot, "Label_Grade/Text_1");
            var levelWord = FindChildText(itemDetailRoot, "Label_Grade/Text_2");
            if (levelValue == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail/Label_Grade/Text_1 not found.");
            }

            if (levelWord == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail/Label_Grade/Text_2 not found.");
            }

            var resourceParent = FindDescendant(itemDetailRoot, "resourceParent");
            var resourceEnergy = resourceParent != null ? FindDescendant(resourceParent, EnergyHudView.EnergyRootName) : null;
            var resourceValue = resourceEnergy != null
                ? FindChildText(resourceEnergy, EnergyHudView.ValueTextName)
                : null;
            var resourceTimer = resourceParent != null ? FindDescendant(resourceParent, EnergyHudView.TimerContainerName) : null;
            TMP_Text resourceTimerText = null;
            if (resourceTimer != null)
            {
                resourceTimerText = FindChildText(resourceTimer, EnergyHudView.TimerTextName);
                if (resourceTimerText == null)
                {
                    resourceTimerText = resourceTimer.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (resourceParent == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail/resourceParent not found.");
            }
            else if (resourceEnergy == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail resourceParent/Resource_Energy not found.");
            }

            if (resourceValue == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail resource value TMP not found.");
            }

            if (resourceTimer == null || resourceTimerText == null)
            {
                Debug.LogError("[TASK17C] Binding failed: ItemDetail resource timer TMP not found. Not creating a new timer.");
            }

            if (resourceEnergy != null)
            {
                var energyHud = resourceEnergy.GetComponent<EnergyHudView>();
                if (energyHud != null)
                {
                    energyHud.enabled = false;
                }
            }

            detail.Bind(
                itemIcon,
                nameText,
                description,
                chainView,
                levelValue,
                levelWord,
                resourceParent,
                resourceValue,
                resourceTimer != null ? resourceTimer.gameObject : null,
                resourceTimerText);
            return detail;
        }

        static void WireDropTargets(BoardController controller, RectTransform hudBag, RectTransform sell, RectTransform ordersRoot)
        {
            var drag = controller.GetComponent<BoardDragController>();
            if (drag == null)
            {
                Debug.LogError("[TASK17C] Binding failed: BoardDragController is missing.");
                return;
            }

            UIDropTargetFeedback sellFeedback = null;
            if (sell != null)
            {
                sellFeedback = EnsureComponent<UIDropTargetFeedback>(sell.gameObject);
                sellFeedback.Configure(sell, null);
            }

            UIDropTargetFeedback[] inventoryFeedbacks = null;
            if (hudBag != null)
            {
                var bagFeedback = EnsureComponent<UIDropTargetFeedback>(hudBag.gameObject);
                bagFeedback.Configure(hudBag, null);
                inventoryFeedbacks = new[] { bagFeedback };
            }

            OrdersHudView ordersHud = null;
            if (ordersRoot != null)
            {
                ordersHud = ordersRoot.GetComponent<OrdersHudView>();
                for (var i = 0; i < ordersRoot.childCount; i++)
                {
                    var child = ordersRoot.GetChild(i);
                    if (child == null || !child.name.StartsWith("Order"))
                    {
                        continue;
                    }

                    var feedback = EnsureComponent<UIDropTargetFeedback>(child.gameObject);
                    feedback.Configure(child as RectTransform, null);
                    WireOrderRequirementSlots(child);
                }
            }

            drag.ConfigureDropTargets(sellFeedback, inventoryFeedbacks, ordersHud);
        }

        static void WireOrderRequirementSlots(Transform orderCard)
        {
            var card = EnsureComponent<OrderCardView>(orderCard.gameObject);
            var items = FindDescendant(orderCard, "OrderItems");
            if (items == null)
            {
                return;
            }

            for (var i = 0; i < items.childCount; i++)
            {
                var child = items.GetChild(i);
                if (child == null || !child.name.StartsWith("Item"))
                {
                    continue;
                }

                var slot = EnsureComponent<OrderRequirementSlotView>(child.gameObject);
                var icon = child.Find("Icon") != null ? child.Find("Icon").GetComponent<Image>() : null;
                var amountBox = FindDescendant(child, "TextBox");
                var amountText = amountBox != null
                    ? amountBox.GetComponentInChildren<TMP_Text>(true)
                    : null;
                slot.Bind(icon, amountText, amountBox != null ? amountBox.gameObject : null);
            }

            card.BindLocal();
        }

        static RectTransform ResolveWindowBlocker()
        {
            var manager = Object.FindAnyObjectByType<UIManager>();
            if (manager == null)
            {
                return null;
            }

            if (manager.transform.Find("WindowInputBlocker") is RectTransform direct)
            {
                return direct;
            }

            return manager.WindowRoot != null ? manager.WindowRoot.Find("WindowInputBlocker") as RectTransform : null;
        }

        static void DisableSharedBlockerDimmer()
        {
            var modal = ResolveWindowBlocker();
            if (modal == null)
            {
                return;
            }

            var image = modal.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                image.enabled = false;
            }

            var click = modal.GetComponent<UIWindowModalClick>();
            if (click != null)
            {
                click.enabled = false;
            }

            if (modal.gameObject.activeSelf)
            {
                modal.gameObject.SetActive(false);
            }
        }

        static void PrintReport(
            RectTransform inventoryRoot,
            RectTransform itemDetailRoot,
            RectTransform hudBag,
            RectTransform windowBag,
            InventorySlotView[] slots,
            int chainNodes,
            RectTransform detailsButton,
            RectTransform sell,
            RectTransform orders)
        {
            var orderCount = 0;
            if (orders != null)
            {
                for (var i = 0; i < orders.childCount; i++)
                {
                    if (orders.GetChild(i) != null && orders.GetChild(i).name.StartsWith("Order"))
                    {
                        orderCount++;
                    }
                }
            }

            Debug.Log(
                "TASK17C VALIDATION\n" +
                $"Existing Inventory root: {(inventoryRoot != null ? "FOUND " + PathOf(inventoryRoot) : "MISSING")}\n" +
                $"Existing ItemDetail root: {(itemDetailRoot != null ? "FOUND " + PathOf(itemDetailRoot) : "MISSING")}\n" +
                $"HUD Bag: {(hudBag != null ? "FOUND " + PathOf(hudBag) : "MISSING")}\n" +
                $"Inventory close Bag: {(windowBag != null ? "FOUND " + PathOf(windowBag) : "MISSING")}\n" +
                $"Inventory slots: {(slots != null ? slots.Length : 0)} FOUND\n" +
                $"ItemDetail chain nodes: {chainNodes} FOUND\n" +
                $"Details trigger: {(detailsButton != null ? "FOUND " + PathOf(detailsButton) : "MISSING")}\n" +
                $"Sell target: {(sell != null ? "FOUND " + PathOf(sell) : "MISSING")}\n" +
                $"Order cards: {orderCount} FOUND\n" +
                "Generated visual UI objects created:\n0");
        }

        static RectTransform FindInventoryWindow()
        {
            var transforms = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            for (var i = 0; i < transforms.Length; i++)
            {
                var rect = transforms[i];
                if (rect != null && rect.name == InventoryWindowName && FindDescendant(rect, InventorySlotsGroupName) != null)
                {
                    return rect;
                }
            }

            return null;
        }

        static RectTransform FindHudBagButton()
        {
            var transforms = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            for (var i = 0; i < transforms.Length; i++)
            {
                var rect = transforms[i];
                if (rect != null &&
                    rect.name == InventoryWindowName &&
                    rect.parent != null &&
                    rect.parent.name == HudBagParentName &&
                    rect.GetComponent<Button>() != null)
                {
                    return rect;
                }
            }

            return null;
        }

        static RectTransform FindWindowBagButton(RectTransform inventoryRoot)
        {
            if (inventoryRoot == null)
            {
                return null;
            }

            var panel = FindChild(inventoryRoot, WindowBagParentName);
            if (panel == null)
            {
                return null;
            }

            var bag = FindChild(panel, InventoryWindowName);
            if (bag == null || bag.GetComponent<Button>() == null)
            {
                Debug.LogError("[TASK17C] Binding failed: Inventory/Panel_Left/Inventory button not found.");
                return null;
            }

            return bag;
        }

        static void ReportMissing(string label, Object obj)
        {
            if (obj == null)
            {
                Debug.LogError($"[TASK17C] Binding failed: {label} not found. Designer placement required.");
            }
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            return group != null ? group : go.AddComponent<CanvasGroup>();
        }

        static UIAnimatedElement EnsureAnimated(GameObject go, UIAnimationRole role, int order)
        {
            var element = EnsureComponent<UIAnimatedElement>(go);
            element.Configure(role, order);
            return element;
        }

        static RectTransform FindNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        static RectTransform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null && child.name == objectName)
                {
                    return child as RectTransform;
                }

                var nested = FindDescendant(child, objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        static RectTransform FindChild(Transform root, string path)
        {
            if (root == null)
            {
                return null;
            }

            var child = root.Find(path);
            return child as RectTransform;
        }

        static Image FindChildImage(Transform root, string path)
        {
            var child = FindChild(root, path);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static TMP_Text FindChildText(Transform root, string path)
        {
            var child = FindChild(root, path);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        static string PathOf(Transform t)
        {
            if (t == null)
            {
                return string.Empty;
            }

            var path = t.name;
            var current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
