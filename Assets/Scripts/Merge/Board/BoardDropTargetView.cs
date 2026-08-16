using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class BoardDropTargetView : MonoBehaviour
    {
        [SerializeField] RectTransform highlight;
        [SerializeField] Image highlightImage;
        [SerializeField] RectTransform parkingParent;

        BoardDragAnimationConfig _config;
        int _cellIndex = BoardController.NoSelectionIndex;
        float _pulseTime;

        public int CellIndex => _cellIndex;
        public bool IsVisible => highlight != null && highlight.gameObject.activeSelf;

        public void Bind(RectTransform overlay, Image image)
        {
            highlight = overlay;
            highlightImage = image;
            if (parkingParent == null && overlay != null)
            {
                parkingParent = overlay.parent as RectTransform;
            }

            if (highlightImage != null)
            {
                highlightImage.raycastTarget = false;
            }

            Hide();
        }

        public void Configure(BoardDragAnimationConfig config)
        {
            _config = config;
            if (highlightImage != null && config != null)
            {
                highlightImage.color = config.DropTargetColor;
            }
        }

        public void ShowOn(BoardCellView cell)
        {
            if (cell == null || highlight == null)
            {
                Hide();
                return;
            }

            if (_cellIndex == cell.Index && highlight.gameObject.activeSelf && highlight.parent == cell.transform)
            {
                return;
            }

            _cellIndex = cell.Index;
            _pulseTime = 0f;
            highlight.SetParent(cell.transform, false);
            highlight.anchorMin = Vector2.zero;
            highlight.anchorMax = Vector2.one;
            highlight.offsetMin = Vector2.zero;
            highlight.offsetMax = Vector2.zero;
            highlight.localScale = Vector3.one;
            highlight.localRotation = Quaternion.identity;
            highlight.SetAsFirstSibling();
            highlight.gameObject.SetActive(true);
            enabled = true;
        }

        public void Hide()
        {
            _cellIndex = BoardController.NoSelectionIndex;
            _pulseTime = 0f;
            if (highlight != null)
            {
                if (parkingParent != null && highlight.parent != parkingParent)
                {
                    highlight.SetParent(parkingParent, false);
                }

                highlight.localScale = Vector3.one;
                highlight.gameObject.SetActive(false);
            }

            enabled = false;
        }

        void Update()
        {
            if (_config == null || highlight == null || !highlight.gameObject.activeSelf)
            {
                return;
            }

            var dt = _config.GetDeltaTime();
            _pulseTime += dt;
            var period = Mathf.Max(0.2f, _config.DropTargetPulsePeriod);
            var wave = 0.5f + 0.5f * Mathf.Sin(_pulseTime * (Mathf.PI * 2f) / period);
            var scale = 1f + _config.DropTargetPulseAmplitude * wave;
            highlight.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
