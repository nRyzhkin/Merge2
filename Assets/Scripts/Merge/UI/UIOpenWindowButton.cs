using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class UIOpenWindowButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] UIWindow targetWindow;
        [SerializeField] Button button;

        public void Bind(UIWindow window, Button clickButton = null)
        {
            if (isActiveAndEnabled && button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }

            targetWindow = window;
            if (clickButton != null)
            {
                button = clickButton;
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (isActiveAndEnabled && button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        void OnEnable()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null)
            {
                return;
            }

            HandleClick();
        }

        void HandleClick()
        {
            if (targetWindow == null || UIManager.Instance == null)
            {
                return;
            }

            UIManager.Instance.OpenWindow(targetWindow);
        }
    }
}
