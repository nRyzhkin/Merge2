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
        BoardMergeAnimationConfig _mergeConfig;
        int _cellIndex = BoardController.NoSelectionIndex;
        float _pulseTime;
        bool _mergeFeedback;
        bool _visible;
        float _alpha;
        float _alphaTarget;
        float _scaleBase = 1f;
        float _scaleTarget = 1f;
        Color _baseColor = Color.white;
        Color _mergeColor = new Color(1f, 0.90f, 0.55f, 1f);

        public int CellIndex => _cellIndex;
        public bool IsVisible => _visible;

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

            HideImmediate();
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

            _mergeFeedback = false;
            _mergeConfig = null;
            _baseColor = _config != null ? _config.DropTargetColor : new Color(1f, 1f, 1f, 0.18f);
            _scaleBase = 1f;
            _scaleTarget = 1f;
            _alphaTarget = _baseColor.a;
            AttachTo(cell);
            if (!_visible)
            {
                _alpha = 0f;
            }

            _visible = true;
            enabled = true;
            ApplyVisual();
        }

        public void ShowMergeOn(BoardCellView cell, BoardMergeAnimationConfig mergeConfig)
        {
            if (cell == null || highlight == null)
            {
                Hide();
                return;
            }

            _mergeFeedback = true;
            _mergeConfig = mergeConfig;
            _mergeColor = mergeConfig != null
                ? mergeConfig.MergeTargetHighlightColor
                : new Color(1f, 0.90f, 0.55f, 1f);
            _baseColor = _mergeColor;
            _scaleBase = mergeConfig != null ? mergeConfig.MergeTargetHighlightScale : 1.035f;
            _scaleTarget = _scaleBase;
            _alphaTarget = mergeConfig != null ? mergeConfig.MergeTargetHighlightAlpha : 0.42f;
            AttachTo(cell);
            if (!_visible)
            {
                _alpha = 0f;
            }

            _visible = true;
            enabled = true;
            ApplyVisual();
        }

        public void Hide()
        {
            if (!_visible && _alpha <= 0.001f)
            {
                HideImmediate();
                return;
            }

            _alphaTarget = 0f;
            _scaleTarget = 1f;
            _mergeFeedback = false;
            enabled = true;
        }

        public void HideImmediate()
        {
            _cellIndex = BoardController.NoSelectionIndex;
            _pulseTime = 0f;
            _mergeFeedback = false;
            _mergeConfig = null;
            _visible = false;
            _alpha = 0f;
            _alphaTarget = 0f;
            _scaleBase = 1f;
            _scaleTarget = 1f;
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
            if (highlight == null)
            {
                return;
            }

            var dt = _mergeFeedback && _mergeConfig != null
                ? _mergeConfig.GetDeltaTime()
                : _config != null ? _config.GetDeltaTime() : Time.unscaledDeltaTime;

            var duration = _alphaTarget > _alpha
                ? (_mergeConfig != null ? _mergeConfig.MergeTargetHighlightEnterDuration : 0.08f)
                : (_mergeConfig != null ? _mergeConfig.MergeTargetHighlightExitDuration : 0.08f);
            if (!_mergeFeedback && _config != null)
            {
                duration = 0.08f;
            }

            duration = Mathf.Max(0.01f, duration);
            _alpha = Mathf.MoveTowards(_alpha, _alphaTarget, dt / duration);
            _scaleBase = Mathf.MoveTowards(_scaleBase, _scaleTarget, dt / duration);

            if (_alphaTarget <= 0.001f && _alpha <= 0.001f)
            {
                HideImmediate();
                return;
            }

            if (_mergeFeedback || (_config != null && highlight.gameObject.activeSelf))
            {
                _pulseTime += dt;
                var period = _config != null ? Mathf.Max(0.2f, _config.DropTargetPulsePeriod) : 1.1f;
                var amplitude = _config != null ? _config.DropTargetPulseAmplitude : 0.015f;
                if (_mergeFeedback)
                {
                    amplitude *= 1.55f;
                }

                var wave = 0.5f + 0.5f * Mathf.Sin(_pulseTime * (Mathf.PI * 2f) / period);
                var pulse = 1f + amplitude * wave;
                highlight.localScale = new Vector3(_scaleBase * pulse, _scaleBase * pulse, 1f);
            }

            ApplyVisual();
        }

        void AttachTo(BoardCellView cell)
        {
            if (_cellIndex != cell.Index || highlight.parent != cell.transform)
            {
                _pulseTime = 0f;
            }

            _cellIndex = cell.Index;
            highlight.SetParent(cell.transform, false);
            highlight.anchorMin = Vector2.zero;
            highlight.anchorMax = Vector2.one;
            highlight.offsetMin = Vector2.zero;
            highlight.offsetMax = Vector2.zero;
            highlight.localRotation = Quaternion.identity;
            highlight.SetAsFirstSibling();
            if (!highlight.gameObject.activeSelf)
            {
                highlight.gameObject.SetActive(true);
            }
        }

        void ApplyVisual()
        {
            if (highlightImage == null)
            {
                return;
            }

            var color = _baseColor;
            color.a = _alpha;
            highlightImage.color = color;
        }
    }
}
