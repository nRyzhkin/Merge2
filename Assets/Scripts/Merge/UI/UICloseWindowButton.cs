using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class UICloseWindowButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] UIWindow targetWindow;
        [SerializeField] Button button;

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
            if (UIManager.Instance == null)
            {
                return;
            }

            var window = targetWindow != null ? targetWindow : GetComponentInParent<UIWindow>(true);
            if (window == null)
            {
                return;
            }

            UIManager.Instance.CloseWindow(window);
        }
    }
}
