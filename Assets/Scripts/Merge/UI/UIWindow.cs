using System;
using UnityEngine;

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
        [SerializeField] bool closeOnBack = true;

        UIWindowChoreographyConfig _defaultConfig;
        UIWindowChoreography _choreography;
        bool _visible;
        bool _closing;
        bool _hiddenInvoked;
        Action _onHidden;

        public bool CloseOnBack => closeOnBack;
        public bool IsOpen => _visible && !_closing;
        public bool IsVisible => _visible;
        public RectTransform ContentRoot => contentRoot;
        public UIWindowChoreography Choreography => EnsureChoreography();

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

        void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            EnsureChoreography();
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
            var listeners = GetComponents<IUIWindowWillOpen>();
            for (var i = 0; i < listeners.Length; i++)
            {
                listeners[i].OnWindowWillOpen();
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
