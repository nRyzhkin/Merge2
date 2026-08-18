using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class PlayerRankMilestoneView : MonoBehaviour
    {
        [SerializeField] Image milestoneImage;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text rankText;
        [SerializeField] GameObject obtainedMarkImage;

        public RectTransform Rect => transform as RectTransform;
        public int Rank { get; private set; }

        public void BindLocal()
        {
            if (milestoneImage == null)
            {
                var mask = FindChild(transform, "Mask");
                var image = mask != null ? FindChild(mask, "Image") : null;
                if (image != null)
                {
                    milestoneImage = image.GetComponent<Image>();
                }
            }

            if (titleText == null)
            {
                var title = FindChild(transform, "Title");
                if (title != null)
                {
                    titleText = title.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (statusText == null)
            {
                var status = FindChild(transform, "Text Bottom");
                if (status != null)
                {
                    statusText = status.GetComponent<TMP_Text>();
                }
            }

            var mark = FindChild(transform, "RankMark");
            if (mark == null)
            {
                mark = FindChild(transform, "Label_Round_White");
            }

            if (rankText == null && mark != null)
            {
                rankText = mark.GetComponentInChildren<TMP_Text>(true);
            }

            if (obtainedMarkImage == null && mark != null)
            {
                var image = FindChild(mark, "Image");
                if (image != null)
                {
                    obtainedMarkImage = image.gameObject;
                }
            }
        }

        public void Apply(PlayerRankDefinition definition, RankMilestoneCardState state)
        {
            BindLocal();
            Rank = definition != null ? definition.rank : 0;
            if (milestoneImage != null)
            {
                if (definition != null && definition.milestoneSprite != null)
                {
                    milestoneImage.sprite = definition.milestoneSprite;
                    milestoneImage.enabled = true;
                }
            }

            if (titleText != null && definition != null)
            {
                var title = MergeUiLocalization.Get(definition.milestoneTitleLocalizationKey);
                titleText.text = string.IsNullOrEmpty(title) ? definition.milestoneTitleLocalizationKey : title;
            }

            if (descriptionText != null && definition != null)
            {
                var description = MergeUiLocalization.Get(definition.milestoneDescriptionLocalizationKey);
                descriptionText.text = description;
            }

            var obtained = state != RankMilestoneCardState.Locked;
            if (obtainedMarkImage != null)
            {
                obtainedMarkImage.SetActive(obtained);
            }

            if (rankText != null)
            {
                rankText.text = obtained ? string.Empty : Rank.ToString();
                rankText.gameObject.SetActive(!obtained);
            }

            if (statusText != null)
            {
                if (state == RankMilestoneCardState.Locked)
                {
                    var locked = MergeUiLocalization.Get(MergeUiLocalization.RankMilestoneLockedKey);
                    if (!string.IsNullOrEmpty(locked))
                    {
                        statusText.text = locked;
                    }
                }
                else
                {
                    var unlocked = MergeUiLocalization.Get(MergeUiLocalization.RankMilestoneUnlockedKey);
                    if (!string.IsNullOrEmpty(unlocked))
                    {
                        statusText.text = unlocked;
                    }
                }
            }
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
