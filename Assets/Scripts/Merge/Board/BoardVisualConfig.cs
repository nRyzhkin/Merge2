using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    [CreateAssetMenu(fileName = "BoardVisualConfig", menuName = "San Island/Board Visual Config")]
    public class BoardVisualConfig : ScriptableObject
    {
        [SerializeField] List<Sprite> boxSprites = new List<Sprite>();
        [SerializeField] Sprite cobwebSprite;

        public IReadOnlyList<Sprite> BoxSprites => boxSprites;
        public Sprite CobwebSprite => cobwebSprite;

        public Sprite GetBoxSprite(int cellIndex)
        {
            if (boxSprites == null || boxSprites.Count == 0)
            {
                return null;
            }

            var index = Mathf.Abs(cellIndex) % boxSprites.Count;
            return boxSprites[index];
        }

#if UNITY_EDITOR
        public void EditorSetVisuals(List<Sprite> boxes, Sprite cobweb)
        {
            boxSprites = boxes ?? new List<Sprite>();
            cobwebSprite = cobweb;
        }
#endif
    }
}
