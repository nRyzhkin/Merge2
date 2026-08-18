using UnityEngine;
using UnityEngine.EventSystems;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class PlayerRankRewardClaimButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] PlayerRankWindow window;

        void Awake()
        {
            if (window == null)
            {
                window = GetComponentInParent<PlayerRankWindow>(true);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (window == null)
            {
                window = GetComponentInParent<PlayerRankWindow>(true);
            }

            window?.TryClaimFromUi();
        }
    }
}
