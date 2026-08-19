using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SanIsland.Merge
{
    public class ItemInfoView : MonoBehaviour
    {
        [SerializeField] Image generatorIcon;
        [SerializeField] Image itemIcon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] MergeChainInfoView chainInfoView;
        [SerializeField] GameObject sellRoot;
        [SerializeField] Button detailsButton;
        [SerializeField] ItemDetailWindow itemDetailWindow;

        MergeItemDatabase _database;
        MergeDiscoveryState _discovery;
        MergeItemData _current;
        LocalizedString _nameString;
        UIWindow _detailUiWindow;

        public MergeChainInfoView ChainInfoView => chainInfoView;
        public bool IsShowing(int itemId) => _current != null && _current.Id == itemId && gameObject.activeSelf;

        public void Bind(
            Image generator,
            Image item,
            TMP_Text name,
            TMP_Text level,
            MergeChainInfoView chain,
            Button details,
            ItemDetailWindow detailWindow)
        {
            generatorIcon = generator;
            itemIcon = item;
            nameText = name;
            levelText = level;
            chainInfoView = chain;
            detailsButton = details;
            itemDetailWindow = detailWindow;
        }

        public void BindDetails(Button details, ItemDetailWindow detailWindow)
        {
            if (isActiveAndEnabled && detailsButton != null)
            {
                detailsButton.onClick.RemoveListener(OpenDetails);
            }

            detailsButton = details;
            itemDetailWindow = detailWindow;
            if (isActiveAndEnabled && detailsButton != null)
            {
                detailsButton.onClick.AddListener(OpenDetails);
            }
        }

        public void Configure(MergeItemDatabase database, MergeDiscoveryState discovery)
        {
            _database = database;
            _discovery = discovery;
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            if (detailsButton != null)
            {
                detailsButton.onClick.AddListener(OpenDetails);
            }
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            if (detailsButton != null)
            {
                detailsButton.onClick.RemoveListener(OpenDetails);
            }

            ClearNameSubscription();
        }

        public void Show(MergeItemData data)
        {
            _current = data;
            if (data == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            if (itemIcon != null)
            {
                itemIcon.sprite = data.Icon;
                itemIcon.enabled = data.Icon != null;
            }

            ApplyLocalizedName(data);
            ApplyLevel(data.Level);
            ApplyGeneratorIcon(data);

            if (chainInfoView != null)
            {
                chainInfoView.Hide();
                if (chainInfoView.gameObject.activeSelf)
                {
                    chainInfoView.gameObject.SetActive(false);
                }
            }

        }

        void OpenDetails()
        {
            if (_current == null || itemDetailWindow == null || UIManager.Instance == null)
            {
                return;
            }

            itemDetailWindow.Configure(_database, _discovery);
            itemDetailWindow.Prepare(_current);
            if (_detailUiWindow == null)
            {
                _detailUiWindow = itemDetailWindow.GetComponent<UIWindow>();
            }

            if (_detailUiWindow != null)
            {
                UIManager.Instance.OpenWindow(_detailUiWindow);
            }
        }

        public void SetSellVisible(bool visible)
        {
            EnsureSellRoot();
            if (sellRoot != null && sellRoot.activeSelf != visible)
            {
                sellRoot.SetActive(visible);
            }
        }

        public void Hide()
        {
            _current = null;
            ClearNameSubscription();
            if (chainInfoView != null)
            {
                chainInfoView.Hide();
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        void OnLocaleChanged(Locale _)
        {
            if (_current != null)
            {
                Show(_current);
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

        void ApplyLevel(int level)
        {
            if (levelText != null)
            {
                levelText.text = MergeUiLocalization.GetLevelLabel(level);
            }
        }

        void ApplyGeneratorIcon(MergeItemData data)
        {
            if (generatorIcon == null || _database == null)
            {
                return;
            }

            if (_database.TryGetLowestGenerator(data.Family, out var generator) && generator != null)
            {
                generatorIcon.sprite = generator.Icon;
                generatorIcon.enabled = generator.Icon != null;
            }
            else
            {
                generatorIcon.enabled = false;
            }
        }

        void EnsureSellRoot()
        {
            if (sellRoot != null)
            {
                return;
            }

            var sell = transform.Find(SellButtonView.SellRootName);
            if (sell != null)
            {
                sellRoot = sell.gameObject;
            }
        }

        public void ApplyGeneratorPresentation(GeneratorPresentationInfo info)
        {
            // Reserved for future Item Info TMP fields:
            // Available: info.AvailableDrops / info.CapacityDrops
            // Cooldown remaining: info.CooldownRemainingSeconds
        }
    }
}
