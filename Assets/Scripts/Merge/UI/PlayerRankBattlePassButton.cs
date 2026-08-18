using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class PlayerRankBattlePassButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] PlayerRankWindow window;
        [SerializeField] Button button;

        void Awake()
        {
            if (window == null)
            {
                window = GetComponentInParent<PlayerRankWindow>(true);
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        void OnEnable()
        {
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
            if (window == null)
            {
                window = GetComponentInParent<PlayerRankWindow>(true);
            }

            window?.HandleBattlePassClicked();
        }
    }
}
