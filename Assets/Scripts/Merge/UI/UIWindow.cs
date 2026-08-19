using System;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public interface IUIWindowWillOpen
    {
        void OnWindowWillOpen();
    }

    [DisallowMultipleComponent]
    public class UIWindow : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] RectTransform windowRoot;
        [SerializeField] RectTransform contentRoot;
        [SerializeField] UIWindowChoreographyConfig overrideConfig;
        [SerializeField] RectTransform overlayRoot;
        [SerializeField] bool closeOnBack = true;

        UIWindowChoreographyConfig _defaultConfig;
        UIWindowChoreography _choreography;
        IUIWindowWillOpen[] _willOpen;
        bool _visible;
        bool _closing;
        bool _hiddenInvoked;
        bool _prewarmed;
        Action _onHidden;

        public bool CloseOnBack => closeOnBack;
        public bool IsOpen => _visible && !_closing;
        public bool IsVisible => _visible;
        public RectTransform ContentRoot => contentRoot;
        public RectTransform OverlayRoot => overlayRoot;
        public UIWindowChoreography Choreography => EnsureChoreography();

        public const string DimmerObjectSuffix = "_Dimmed";

        public void BindRoots(RectTransform root, CanvasGroup group)
        {
            windowRoot = root;
            canvasGroup = group;
        }

        public void BindOverlay(RectTransform overlay)
        {
            overlayRoot = overlay;
        }

        public void EnsureOwnDimmer(UIWindowChoreographyConfig config)
        {
            if (overlayRoot == null)
            {
                overlayRoot = CreateSiblingDimmer();
            }
            else
            {
                PlaceDimmerBesideWindow(overlayRoot);
            }

            ConfigureDimmer(overlayRoot, config);
        }

        public void Configure(UIWindowChoreographyConfig defaultConfig)
        {
            if (_defaultConfig == null)
            {
                _defaultConfig = defaultConfig;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            EnsureChoreography().Initialize(windowRoot, canvasGroup, ResolveConfig());
        }

        public void Prewarm(UIWindowChoreographyConfig defaultConfig)
        {
            if (_prewarmed)
            {
                return;
            }

            EnsureOwnDimmer(defaultConfig);
            Configure(defaultConfig);
            CacheWillOpen();
            EnsureChoreography().Prewarm();
            _prewarmed = true;
        }

        void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            EnsureChoreography();
            CacheWillOpen();
        }

        void OnDisable()
        {
            if (!_closing)
            {
                return;
            }

            _closing = false;
            _visible = false;
            InvokeHidden();
        }

        public void Show()
        {
            NotifyWillOpen();
            var choreography = EnsureChoreography();
            choreography.Initialize(windowRoot, canvasGroup, ResolveConfig());
            var wasActive = gameObject.activeSelf;
            _visible = true;
            _closing = false;
            _hiddenInvoked = false;
            _onHidden = null;

            if (wasActive && !choreography.IsPlaying && !choreography.IsClosing && choreography.IsSettledOpen)
            {
                return;
            }

            if (!wasActive)
            {
                SetOverlayActive(true);
                choreography.SnapOpenStart();
                gameObject.SetActive(true);
            }

            choreography.PlayOpen();
        }

        public void Hide(Action onHidden = null)
        {
            _onHidden = onHidden;
            _hiddenInvoked = false;
            if (!gameObject.activeSelf)
            {
                _visible = false;
                _closing = false;
                InvokeHidden();
                return;
            }

            var choreography = EnsureChoreography();
            choreography.Initialize(windowRoot, canvasGroup, ResolveConfig());
            _visible = true;
            _closing = true;
            choreography.PlayClose(HandleCloseFinished);
        }

        void NotifyWillOpen()
        {
            CacheWillOpen();
            if (_willOpen == null)
            {
                return;
            }

            for (var i = 0; i < _willOpen.Length; i++)
            {
                _willOpen[i].OnWindowWillOpen();
            }
        }

        void CacheWillOpen()
        {
            if (_willOpen == null)
            {
                _willOpen = GetComponents<IUIWindowWillOpen>();
            }
        }

        void HandleCloseFinished()
        {
            if (_hiddenInvoked)
            {
                return;
            }

            _closing = false;
            _visible = false;
            SetOverlayActive(false);
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            InvokeHidden();
        }

        void InvokeHidden()
        {
            if (_hiddenInvoked)
            {
                return;
            }

            _hiddenInvoked = true;
            var callback = _onHidden;
            _onHidden = null;
            callback?.Invoke();
        }

        void SetOverlayActive(bool active)
        {
            if (overlayRoot == null)
            {
                return;
            }

            if (overlayRoot.IsChildOf(transform) || overlayRoot == transform)
            {
                return;
            }

            if (overlayRoot.gameObject.activeSelf != active)
            {
                overlayRoot.gameObject.SetActive(active);
            }
        }

        RectTransform CreateSiblingDimmer()
        {
            var parent = transform.parent as RectTransform;
            var host = parent != null ? parent : transform as RectTransform;
            var name = gameObject.name + DimmerObjectSuffix;
            var existing = host.Find(name) as RectTransform;
            if (existing != null)
            {
                PlaceDimmerBesideWindow(existing);
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.layer = gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(host, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            PlaceDimmerBesideWindow(rect);
            go.SetActive(false);
            return rect;
        }

        void PlaceDimmerBesideWindow(RectTransform dimmer)
        {
            if (dimmer == null || dimmer == transform || dimmer.parent != transform.parent)
            {
                return;
            }

            var windowIndex = transform.GetSiblingIndex();
            var dimmerIndex = dimmer.GetSiblingIndex();
            if (dimmerIndex == windowIndex - 1)
            {
                return;
            }

            dimmer.SetSiblingIndex(windowIndex);
        }

        void ConfigureDimmer(RectTransform dimmer, UIWindowChoreographyConfig config)
        {
            if (dimmer == null)
            {
                return;
            }

            var alpha = config != null ? config.OptionalModalBackgroundAlpha : 0.62f;
            var image = dimmer.GetComponent<Image>();
            if (image == null)
            {
                image = dimmer.gameObject.AddComponent<Image>();
            }

            image.color = new Color(17f / 255f, 37f / 255f, 62f / 255f, alpha);
            image.raycastTarget = true;
            var group = dimmer.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = dimmer.gameObject.AddComponent<CanvasGroup>();
            }

            group.ignoreParentGroups = true;
            group.blocksRaycasts = true;
            group.interactable = true;
            var click = dimmer.GetComponent<UIWindowModalClick>();
            if (click == null)
            {
                click = dimmer.gameObject.AddComponent<UIWindowModalClick>();
            }

            click.Bind(this);
        }

        UIWindowChoreographyConfig ResolveConfig()
        {
            if (overrideConfig != null)
            {
                return overrideConfig;
            }

            if (_defaultConfig != null)
            {
                return _defaultConfig;
            }

            return UIManager.Instance != null ? UIManager.Instance.DefaultWindowChoreographyConfig : null;
        }

        UIWindowChoreography EnsureChoreography()
        {
            if (_choreography == null)
            {
                _choreography = GetComponent<UIWindowChoreography>();
            }

            if (_choreography == null)
            {
                _choreography = gameObject.AddComponent<UIWindowChoreography>();
            }

            return _choreography;
        }
    }
}
