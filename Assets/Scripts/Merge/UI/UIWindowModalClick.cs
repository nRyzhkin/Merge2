using UnityEngine;
using UnityEngine.EventSystems;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class UIWindowModalClick : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] UIWindow targetWindow;

        public void Bind(UIWindow window)
        {
            targetWindow = window;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            var window = targetWindow != null ? targetWindow : GetComponentInParent<UIWindow>(true);
            if (window == null)
            {
                return;
            }

            UIManager.Instance?.CloseWindow(window);
        }
    }
}
