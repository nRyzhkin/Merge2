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
            if (windowInputBlocker != null && windowInputBlocker.activeSelf)
            {
                windowInputBlocker.SetActive(false);
            }

            RegisterInteractiveHierarchy(hudRoot);
            RegisterInteractiveHierarchy(windowRoot);
            RegisterInteractiveHierarchy(popupRoot);
            RefreshBlocker();
            PrewarmWindows();
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

            var existing = _windowStack.IndexOf(window);
            if (existing >= 0)
            {
                _windowStack.RemoveAt(existing);
            }

            _windowStack.Add(window);
            window.Configure(defaultWindowChoreographyConfig);
            RefreshBlocker();
            window.Show();
        }

        public void CloseWindow(UIWindow window)
        {
            if (window == null)
            {
                return;
            }

            window.Hide(() =>
            {
                _windowStack.Remove(window);
                RefreshBlocker();
            });
            RefreshBlocker();
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
            ResetOverlayAlpha();
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

        void PrewarmWindows()
        {
            var windows = GetComponentsInChildren<UIWindow>(true);
            for (var i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null)
                {
                    windows[i].Prewarm(defaultWindowChoreographyConfig);
                }
            }
        }

        void RefreshBlocker()
        {
            if (windowInputBlocker != null && windowInputBlocker.activeSelf)
            {
                windowInputBlocker.SetActive(false);
            }
        }

        public void BindModalBackground(Image image)
        {
            modalBackground = image;
            PrepareOverlayVisual();
        }

        public void CloseItemDetailFromModal()
        {
            CloseTopWindow();
        }

        void PrepareOverlayVisual()
        {
            if (windowInputBlocker == null && modalBackground != null)
            {
                windowInputBlocker = modalBackground.gameObject;
            }

            if (modalBackground == null && windowInputBlocker != null)
            {
                modalBackground = windowInputBlocker.GetComponent<Image>();
            }

            if (modalBackground == null)
            {
                return;
            }

            var alpha = defaultWindowChoreographyConfig != null
                ? defaultWindowChoreographyConfig.OptionalModalBackgroundAlpha
                : 0.62f;
            modalBackground.color = new Color(17f / 255f, 37f / 255f, 62f / 255f, alpha);
            modalBackground.raycastTarget = true;
            if (modalBackground.GetComponent<CanvasGroup>() == null)
            {
                modalBackground.gameObject.AddComponent<CanvasGroup>();
            }
        }

        void ResetOverlayAlpha()
        {
            var target = windowInputBlocker != null ? windowInputBlocker : modalBackground != null ? modalBackground.gameObject : null;
            if (target == null)
            {
                return;
            }

            var group = target.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
            }
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
