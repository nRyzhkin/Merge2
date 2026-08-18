using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class UIManager : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] RectTransform hudRoot;
        [SerializeField] RectTransform windowRoot;
        [SerializeField] RectTransform popupRoot;
        [SerializeField] RectTransform messagesRoot;

        [Header("Windows")]
        [SerializeField] GameObject windowInputBlocker;
        [SerializeField] Image modalBackground;
        [SerializeField] UIWindowChoreographyConfig defaultWindowChoreographyConfig;
        [SerializeField] UiInteractionFeedbackConfig interactionFeedbackConfig;

        readonly List<UIWindow> _windowStack = new List<UIWindow>(4);
        float _modalFrom;
        float _modalTo;
        float _modalElapsed;
        float _modalDuration;
        bool _modalAnimating;
        AnimationCurve _modalCurve;

        public static UIManager Instance { get; private set; }

        public RectTransform HudRoot => hudRoot;
        public RectTransform WindowRoot => windowRoot;
        public RectTransform PopupRoot => popupRoot;
        public RectTransform MessagesRoot => messagesRoot;
        public UIWindowChoreographyConfig DefaultWindowChoreographyConfig => defaultWindowChoreographyConfig;
        public int OpenWindowCount => _windowStack.Count;
        public UIWindow TopWindow => _windowStack.Count > 0 ? _windowStack[_windowStack.Count - 1] : null;
        public bool HasOpenWindows => _windowStack.Count > 0;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }

            Instance = this;
            RegisterInteractiveHierarchy(hudRoot);
            RegisterInteractiveHierarchy(windowRoot);
            RegisterInteractiveHierarchy(popupRoot);
            RefreshBlocker();
            ApplyModalAlpha(0f);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            TickModalBackground();
            if (!WasBackPressed())
            {
                return;
            }

            var top = TopWindow;
            if (top != null && top.CloseOnBack)
            {
                CloseTopWindow();
            }
        }

        public void OpenWindow(UIWindow window)
        {
            if (window == null)
            {
                return;
            }

            if (TopWindow == window && window.IsOpen)
            {
                return;
            }

            var openingFirst = _windowStack.Count == 0;
            var existing = _windowStack.IndexOf(window);
            if (existing >= 0)
            {
                _windowStack.RemoveAt(existing);
            }

            _windowStack.Add(window);
            window.Configure(defaultWindowChoreographyConfig);
            window.transform.SetAsLastSibling();
            window.Show();
            RegisterInteractiveHierarchy(window.transform);
            RefreshBlocker();
            if (openingFirst)
            {
                BeginModalFade(true);
            }
        }

        public void CloseWindow(UIWindow window)
        {
            if (window == null)
            {
                return;
            }

            var closingLast = _windowStack.Count == 1 && _windowStack.Contains(window);
            window.Hide(() =>
            {
                _windowStack.Remove(window);
                RefreshBlocker();
            });
            RefreshBlocker();
            if (closingLast)
            {
                BeginModalFade(false);
            }
        }

        public void CloseTopWindow()
        {
            var top = TopWindow;
            if (top == null)
            {
                return;
            }

            CloseWindow(top);
        }

        public void CloseAllWindows()
        {
            for (var i = _windowStack.Count - 1; i >= 0; i--)
            {
                var window = _windowStack[i];
                if (window != null)
                {
                    window.Hide();
                }
            }

            _windowStack.Clear();
            RefreshBlocker();
            BeginModalFade(false);
        }

        public void RegisterInteractiveHierarchy(Transform root)
        {
            if (root == null || interactionFeedbackConfig == null)
            {
                return;
            }

            var selectables = root.GetComponentsInChildren<Selectable>(true);
            for (var i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                if (selectable == null || ShouldSkipFeedback(selectable.gameObject))
                {
                    continue;
                }

                var feedback = selectable.GetComponent<UiHoverScaleFeedback>();
                if (feedback == null)
                {
                    feedback = selectable.gameObject.AddComponent<UiHoverScaleFeedback>();
                }

                feedback.Configure(interactionFeedbackConfig, selectable.transform as RectTransform);
            }
        }

        void RefreshBlocker()
        {
            if (windowInputBlocker == null)
            {
                return;
            }

            var shouldBlock = _windowStack.Count > 0;
            if (windowInputBlocker.activeSelf != shouldBlock)
            {
                windowInputBlocker.SetActive(shouldBlock);
            }
        }

        void BeginModalFade(bool fadeIn)
        {
            if (modalBackground == null)
            {
                return;
            }

            var target = fadeIn && defaultWindowChoreographyConfig != null
                ? defaultWindowChoreographyConfig.OptionalModalBackgroundAlpha
                : 0f;
            _modalFrom = modalBackground.color.a;
            _modalTo = target;
            _modalElapsed = 0f;
            _modalDuration = defaultWindowChoreographyConfig != null
                ? Mathf.Max(0.01f, defaultWindowChoreographyConfig.ModalBackgroundDuration)
                : 0.16f;
            if (_modalCurve == null)
            {
                _modalCurve = UIWindowChoreographyConfig.CreateEaseOut();
            }
            _modalAnimating = true;
            if (!modalBackground.gameObject.activeSelf && fadeIn)
            {
                modalBackground.gameObject.SetActive(true);
            }
        }

        void TickModalBackground()
        {
            if (!_modalAnimating || modalBackground == null)
            {
                return;
            }

            _modalElapsed += Time.unscaledDeltaTime;
            var t = _modalDuration <= 0.0001f ? 1f : Mathf.Clamp01(_modalElapsed / _modalDuration);
            var k = _modalCurve != null ? _modalCurve.Evaluate(t) : t;
            ApplyModalAlpha(Mathf.LerpUnclamped(_modalFrom, _modalTo, k));
            if (t < 1f)
            {
                return;
            }

            _modalAnimating = false;
            ApplyModalAlpha(_modalTo);
        }

        void ApplyModalAlpha(float alpha)
        {
            if (modalBackground == null)
            {
                return;
            }

            var color = modalBackground.color;
            color.a = alpha;
            modalBackground.color = color;
        }

        static bool ShouldSkipFeedback(GameObject go)
        {
            return go.GetComponent<BoardCellView>() != null
                || go.GetComponent<BoardItemAnimator>() != null
                || go.GetComponent<BoardCellPointer>() != null
                || go.GetComponentInParent<BoardCellView>() != null
                || go.GetComponentInParent<BoardItemAnimator>() != null;
        }

        static bool WasBackPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
    }
}
