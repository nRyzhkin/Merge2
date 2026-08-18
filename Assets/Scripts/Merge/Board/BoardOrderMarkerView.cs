using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardOrderMarkerView : MonoBehaviour
    {
        public const string MarkerName = "Ordered";
        public const string MarkName = "mark";

        [SerializeField] RectTransform template;
        [SerializeField] RectTransform parkingParent;

        readonly List<RectTransform> _pool = new List<RectTransform>(BoardState.CellCount);
        readonly List<RectTransform> _marks = new List<RectTransform>(BoardState.CellCount);
        readonly List<int> _highlightScratch = new List<int>(BoardState.CellCount);
        readonly RectTransform[] _markerByCell = new RectTransform[BoardState.CellCount];
        readonly bool[] _wantCell = new bool[BoardState.CellCount];
        OrderSystem _orders;
        BoardController _board;

        public bool HasTemplate => template != null;

        public void Bind(RectTransform orderedTemplate)
        {
            template = orderedTemplate;
            if (parkingParent == null && template != null)
            {
                parkingParent = template.parent as RectTransform;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            EnsurePool();
            SyncMarkers();
        }

        void OnEnable()
        {
            BindOrders(OrderSystem.Current);
            SyncMarkers();
        }

        void OnDisable()
        {
            UnbindOrders();
            if (gameObject.activeInHierarchy)
            {
                HideAll();
            }
        }

        void LateUpdate()
        {
            if (_orders == null)
            {
                BindOrders(OrderSystem.Current);
            }

            SyncMarkers();
        }

        void BindOrders(OrderSystem system)
        {
            if (system == _orders && _orders != null)
            {
                return;
            }

            UnbindOrders();
            _orders = system;
            if (_orders == null)
            {
                return;
            }

            _board = _orders.GetComponent<BoardController>();
            _orders.Changed += SyncMarkers;
            ResolveTemplateFromScene();
            EnsurePool();
        }

        void UnbindOrders()
        {
            if (_orders != null)
            {
                _orders.Changed -= SyncMarkers;
            }

            _orders = null;
            _board = null;
        }

        void SyncMarkers()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ResolveTemplateFromScene();
            EnsurePool();
            if (template == null || _board == null || _board.BoardView == null || _orders == null)
            {
                HideAll();
                return;
            }

            for (var i = 0; i < _wantCell.Length; i++)
            {
                _wantCell[i] = false;
            }

            _orders.CopyReadyHighlightCells(_highlightScratch);
            var drag = _board.DragController;
            for (var i = 0; i < _highlightScratch.Count; i++)
            {
                var index = _highlightScratch[i];
                if (index < 0 || index >= _wantCell.Length)
                {
                    continue;
                }

                if (drag != null && drag.IsBusyWithCell(index))
                {
                    continue;
                }

                var cell = _board.BoardView.GetCellView(index);
                if (cell == null ||
                    !cell.gameObject.activeInHierarchy ||
                    cell.IsItemHiddenForDrag ||
                    cell.IsItemPresentationSuppressed)
                {
                    continue;
                }

                _wantCell[index] = true;
            }

            for (var i = 0; i < _markerByCell.Length; i++)
            {
                if (_wantCell[i] || _markerByCell[i] == null)
                {
                    continue;
                }

                Park(_markerByCell[i], MarkOf(_markerByCell[i]));
                _markerByCell[i] = null;
            }

            for (var i = 0; i < _wantCell.Length; i++)
            {
                if (!_wantCell[i] || _markerByCell[i] != null)
                {
                    continue;
                }

                var cell = _board.BoardView.GetCellView(i);
                var marker = RentFree();
                if (cell == null || marker == null)
                {
                    continue;
                }

                ShowOn(cell, marker, MarkOf(marker));
                _markerByCell[i] = marker;
            }
        }

        void ShowOn(BoardCellView cell, RectTransform marker, RectTransform mark)
        {
            if (cell == null || marker == null || !cell.gameObject.activeInHierarchy)
            {
                return;
            }

            if (marker.parent != cell.transform && !CanReparent(marker))
            {
                return;
            }

            AttachBacking(marker, cell.transform);
            if (mark == null)
            {
                mark = marker.Find(MarkName) as RectTransform;
            }

            if (mark != null && mark.parent != cell.transform && CanReparent(mark))
            {
                mark.SetParent(cell.transform, false);
            }

            var selection = _board != null ? _board.SelectionView : null;
            RectTransform back = null;
            RectTransform front = null;
            if (selection != null)
            {
                if (selection.SelectionBack != null && selection.SelectionBack.parent == cell.transform)
                {
                    back = selection.SelectionBack;
                }

                if (selection.SelectionFront != null && selection.SelectionFront.parent == cell.transform)
                {
                    front = selection.SelectionFront;
                }
            }

            cell.ApplyContentSiblingOrder(back, front, marker, mark);
            if (!marker.gameObject.activeSelf)
            {
                marker.gameObject.SetActive(true);
            }

            if (mark != null && !mark.gameObject.activeSelf)
            {
                mark.gameObject.SetActive(true);
            }
        }

        void EnsurePool()
        {
            if (!Application.isPlaying || template == null)
            {
                return;
            }

            if (parkingParent == null)
            {
                parkingParent = template.parent as RectTransform;
            }

            DisableRaycasts(template);
            RelocateTemplateIfNeeded();
            if (template.gameObject.activeSelf)
            {
                template.gameObject.SetActive(false);
            }

            while (_pool.Count < BoardState.CellCount)
            {
                var instance = Instantiate(template.gameObject, parkingParent, false);
                instance.name = MarkerName;
                instance.layer = template.gameObject.layer;
                var rect = instance.GetComponent<RectTransform>();
                DisableRaycasts(rect);
                var mark = rect != null ? rect.Find(MarkName) as RectTransform : null;
                Park(rect, mark);
                _pool.Add(rect);
                _marks.Add(mark);
            }
        }

        RectTransform RentFree()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                var marker = _pool[i];
                if (marker == null || IsAssigned(marker))
                {
                    continue;
                }

                return marker;
            }

            return null;
        }

        bool IsAssigned(RectTransform marker)
        {
            for (var i = 0; i < _markerByCell.Length; i++)
            {
                if (_markerByCell[i] == marker)
                {
                    return true;
                }
            }

            return false;
        }

        RectTransform MarkOf(RectTransform marker)
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] == marker)
                {
                    return i < _marks.Count ? _marks[i] : null;
                }
            }

            return marker != null ? marker.Find(MarkName) as RectTransform : null;
        }

        void ResolveTemplateFromScene()
        {
            if (template != null)
            {
                if (parkingParent == null)
                {
                    parkingParent = template.parent as RectTransform;
                }

                return;
            }

            if (_board == null)
            {
                return;
            }

            var selection = _board.SelectionView;
            if (TryFindOrdered(selection != null && selection.SelectionBack != null
                    ? selection.SelectionBack.parent
                    : null))
            {
                return;
            }

            TryFindOrdered(_board.BoardRoot != null ? _board.BoardRoot.parent : null);
        }

        bool TryFindOrdered(Transform searchParent)
        {
            if (searchParent == null)
            {
                return false;
            }

            for (var i = 0; i < searchParent.childCount; i++)
            {
                var child = searchParent.GetChild(i) as RectTransform;
                if (child == null || child.name != MarkerName || _pool.Contains(child))
                {
                    continue;
                }

                template = child;
                parkingParent = searchParent as RectTransform;
                RelocateTemplateIfNeeded();
                return true;
            }

            return false;
        }

        void RelocateTemplateIfNeeded()
        {
            if (template == null)
            {
                return;
            }

            if (template.GetComponentInParent<BoardCellView>(true) == null)
            {
                if (parkingParent == null)
                {
                    parkingParent = template.parent as RectTransform;
                }

                return;
            }

            var selection = _board != null ? _board.SelectionView : null;
            var park = selection != null && selection.SelectionBack != null
                ? selection.SelectionBack.parent as RectTransform
                : parkingParent;
            if (park == null || template.parent == park || !CanReparent(template))
            {
                return;
            }

            template.SetParent(park, false);
            parkingParent = park;
        }

        void HideAll()
        {
            for (var i = 0; i < _markerByCell.Length; i++)
            {
                _markerByCell[i] = null;
            }

            for (var i = 0; i < _pool.Count; i++)
            {
                Park(_pool[i], i < _marks.Count ? _marks[i] : null);
            }
        }

        void Park(RectTransform rect, RectTransform mark)
        {
            if (rect == null)
            {
                return;
            }

            if (mark != null && mark.parent != rect && CanReparent(mark))
            {
                mark.SetParent(rect, false);
            }

            if (parkingParent != null && rect.parent != parkingParent && CanReparent(rect))
            {
                rect.SetParent(parkingParent, false);
            }

            if (rect.gameObject.activeSelf)
            {
                rect.gameObject.SetActive(false);
            }
        }

        bool CanReparent(Transform target)
        {
            if (!isActiveAndEnabled || target == null)
            {
                return false;
            }

            var parent = target.parent;
            return parent == null || parent.gameObject.activeInHierarchy;
        }

        static void AttachBacking(RectTransform rect, Transform parent)
        {
            if (rect.parent != parent)
            {
                rect.SetParent(parent, false);
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        static void DisableRaycasts(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }
}
