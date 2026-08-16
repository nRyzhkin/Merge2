using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SanIsland.Merge
{
    public static class UiPointerUtility
    {
        public static bool SupportsHover(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            if (eventData is ExtendedPointerEventData extended)
            {
                return extended.pointerType == UIPointerType.MouseOrPen;
            }

            return eventData.pointerId < 0;
        }
    }
}
