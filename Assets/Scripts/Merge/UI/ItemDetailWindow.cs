using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class ItemDetailWindow : MonoBehaviour, IUIWindowWillOpen
    {
        [SerializeField] Image itemIcon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text levelValueText;
        [SerializeField] TMP_Text levelWordText;
        [SerializeField] ItemDetailChainView chainView;

        MergeItemDatabase _database;
        MergeDiscoveryState _discovery;
        MergeItemData _pending;
        MergeItemData _current;
        LocalizedString _nameString;
        LocalizedString _levelWordString;

        public void Bind(
            Image icon,
            TMP_Text name,
            TMP_Text description,
            ItemDetailChainView chain,
            TMP_Text levelValue,
            TMP_Text levelWord)
        {
            itemIcon = icon;
            nameText = name;
            descriptionText = description;
            chainView = chain;
            levelValueText = levelValue;
            levelWordText = levelWord;
        }

        public void Configure(MergeItemDatabase database, MergeDiscoveryState discovery)
        {
            _database = database;
            _discovery = discovery;
        }

        public void Prepare(MergeItemData data)
        {
            _pending = data;
        }

        public void OnWindowWillOpen()
        {
            if (_pending != null)
            {
                Show(_pending);
            }
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            ClearNameSubscription();
            ClearLevelWordSubscription();
        }

        public void Show(MergeItemData data)
        {
            _current = data;
            if (data == null)
            {
                return;
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = data.Icon;
                itemIcon.enabled = data.Icon != null;
            }

            ApplyLocalizedName(data);
            ApplyLevel(data.Level);
            if (descriptionText != null)
            {
                var placeholder = MergeUiLocalization.Get(MergeUiLocalization.ItemDescriptionPlaceholderKey);
                descriptionText.text = string.IsNullOrEmpty(placeholder) ? "Description" : placeholder;
            }

            if (chainView != null && _database != null)
            {
                chainView.Show(_database.GetChain(data.Family, data.Kind), data.Id, _discovery);
            }
        }

        void OnLocaleChanged(Locale _)
        {
            if (_current != null)
            {
                Show(_current);
            }
        }

        void ApplyLevel(int level)
        {
            if (levelValueText != null)
            {
                levelValueText.text = level.ToString();
            }

            ClearLevelWordSubscription();
            if (levelWordText == null)
            {
                return;
            }

            _levelWordString = new LocalizedString(MergeUiLocalization.TableName, MergeUiLocalization.LevelWordKey);
            _levelWordString.StringChanged += OnLevelWordChanged;
            levelWordText.text = _levelWordString.GetLocalizedString();
        }

        void OnLevelWordChanged(string value)
        {
            if (levelWordText != null)
            {
                levelWordText.text = value;
            }
        }

        void ClearLevelWordSubscription()
        {
            if (_levelWordString != null)
            {
                _levelWordString.StringChanged -= OnLevelWordChanged;
                _levelWordString = null;
            }
        }

        void ApplyLocalizedName(MergeItemData data)
        {
            ClearNameSubscription();
            if (nameText == null || string.IsNullOrEmpty(data.LocalizationKey))
            {
                return;
            }

            _nameString = new LocalizedString(MergeItemLocalization.TableName, data.LocalizationKey);
            _nameString.StringChanged += OnNameChanged;
            nameText.text = _nameString.GetLocalizedString();
        }

        void OnNameChanged(string value)
        {
            if (nameText != null)
            {
                nameText.text = value;
            }
        }

        void ClearNameSubscription()
        {
            if (_nameString != null)
            {
                _nameString.StringChanged -= OnNameChanged;
                _nameString = null;
            }
        }
    }
}
