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
        [SerializeField] RectTransform resourceParent;
        [SerializeField] TMP_Text resourceValueText;
        [SerializeField] GameObject resourceTimerContainer;
        [SerializeField] TMP_Text resourceTimerText;

        MergeItemDatabase _database;
        MergeDiscoveryState _discovery;
        BoardController _board;
        MergeItemData _pending;
        MergeItemData _current;
        int _pendingCellIndex = BoardController.NoSelectionIndex;
        int _sourceCellIndex = BoardController.NoSelectionIndex;
        LocalizedString _nameString;
        LocalizedString _levelWordString;
        LocalizedString _descriptionString;
        int _displayedDrops = int.MinValue;
        int _displayedTimerSeconds = int.MinValue;
        bool _loggedMissingTimer;
        bool _loggedMissingValue;
        bool _loggedMissingResourceParent;

        public void Bind(
            Image icon,
            TMP_Text name,
            TMP_Text description,
            ItemDetailChainView chain,
            TMP_Text levelValue,
            TMP_Text levelWord,
            RectTransform resourceRoot = null,
            TMP_Text resourceValue = null,
            GameObject resourceTimerRoot = null,
            TMP_Text resourceTimer = null)
        {
            itemIcon = icon;
            nameText = name;
            descriptionText = description;
            chainView = chain;
            levelValueText = levelValue;
            levelWordText = levelWord;
            resourceParent = resourceRoot;
            resourceValueText = resourceValue;
            resourceTimerContainer = resourceTimerRoot;
            resourceTimerText = resourceTimer;
        }

        public void Configure(MergeItemDatabase database, MergeDiscoveryState discovery, BoardController board = null)
        {
            _database = database;
            _discovery = discovery;
            BindBoard(board);
        }

        public void Prepare(MergeItemData data, int boardCellIndex = BoardController.NoSelectionIndex)
        {
            _pending = data;
            _pendingCellIndex = boardCellIndex;
        }

        public void OnWindowWillOpen()
        {
            if (_pending != null)
            {
                Show(_pending, _pendingCellIndex);
            }
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            SubscribeBoard();
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            UnsubscribeBoard();
            ClearNameSubscription();
            ClearLevelWordSubscription();
            ClearDescriptionSubscription();
        }

        void LateUpdate()
        {
            if (_current != null && _current.Kind == MergeItemKind.Generator)
            {
                RefreshGeneratorResource();
            }
        }

        public void Show(MergeItemData data, int boardCellIndex = BoardController.NoSelectionIndex)
        {
            _current = data;
            _sourceCellIndex = boardCellIndex;
            if (data == null)
            {
                return;
            }

            EnsureResourceBindings();
            if (itemIcon != null)
            {
                itemIcon.sprite = data.Icon;
                itemIcon.enabled = data.Icon != null;
            }

            ApplyLocalizedName(data);
            ApplyLevel(data.Level);
            ApplyDescription(data);

            if (chainView != null && _database != null)
            {
                chainView.Show(_database.GetChain(data.Family, data.Kind), data.Id, _discovery);
            }

            RefreshGeneratorResource();
        }

        void OnLocaleChanged(Locale _)
        {
            if (_current != null)
            {
                Show(_current, _sourceCellIndex);
            }
        }

        void BindBoard(BoardController board)
        {
            if (_board == board)
            {
                return;
            }

            UnsubscribeBoard();
            _board = board;
            SubscribeBoard();
        }

        void SubscribeBoard()
        {
            if (_board == null)
            {
                return;
            }

            _board.GeneratorProduced -= RefreshGeneratorResource;
            _board.GeneratorProduced += RefreshGeneratorResource;
        }

        void UnsubscribeBoard()
        {
            if (_board != null)
            {
                _board.GeneratorProduced -= RefreshGeneratorResource;
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

        void ApplyDescription(MergeItemData data)
        {
            ClearDescriptionSubscription();
            if (descriptionText == null)
            {
                return;
            }

            if (data.Kind == MergeItemKind.Generator)
            {
                _descriptionString = new LocalizedString(MergeUiLocalization.TableName, MergeUiLocalization.GeneratorDescriptionKey);
                _descriptionString.StringChanged += OnDescriptionChanged;
                descriptionText.text = _descriptionString.GetLocalizedString();
                return;
            }

            var placeholder = MergeUiLocalization.Get(MergeUiLocalization.ItemDescriptionPlaceholderKey);
            descriptionText.text = string.IsNullOrEmpty(placeholder) ? "Description" : placeholder;
        }

        void OnDescriptionChanged(string value)
        {
            if (descriptionText != null)
            {
                descriptionText.text = value;
            }
        }

        void ClearDescriptionSubscription()
        {
            if (_descriptionString != null)
            {
                _descriptionString.StringChanged -= OnDescriptionChanged;
                _descriptionString = null;
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

        void RefreshGeneratorResource()
        {
            EnsureResourceBindings();
            var isGenerator = _current != null && _current.Kind == MergeItemKind.Generator;
            if (resourceParent != null && resourceParent.gameObject.activeSelf != isGenerator)
            {
                resourceParent.gameObject.SetActive(isGenerator);
            }

            if (!isGenerator)
            {
                _displayedDrops = int.MinValue;
                HideResourceTimer();
                return;
            }

            var available = 0;
            var secondsUntilCharge = 0f;
            if (_board != null &&
                _sourceCellIndex != BoardController.NoSelectionIndex &&
                _board.TryGetGeneratorPresentationInfo(_sourceCellIndex, out var info))
            {
                available = info.AvailableDrops;
                secondsUntilCharge = info.SecondsUntilNextCharge;
            }

            if (resourceValueText != null && available != _displayedDrops)
            {
                _displayedDrops = available;
                resourceValueText.text = available.ToString();
            }

            if (available > 0)
            {
                HideResourceTimer();
                return;
            }

            ShowResourceTimer(secondsUntilCharge);
        }

        void HideResourceTimer()
        {
            _displayedTimerSeconds = int.MinValue;
            if (resourceTimerContainer != null && resourceTimerContainer.activeSelf)
            {
                resourceTimerContainer.SetActive(false);
            }
        }

        void ShowResourceTimer(float secondsUntilNextCharge)
        {
            if (resourceTimerContainer == null)
            {
                return;
            }

            if (!resourceTimerContainer.activeSelf)
            {
                resourceTimerContainer.SetActive(true);
            }

            if (resourceTimerText == null)
            {
                return;
            }

            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsUntilNextCharge));
            if (totalSeconds == _displayedTimerSeconds)
            {
                return;
            }

            _displayedTimerSeconds = totalSeconds;
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            resourceTimerText.text = $"{minutes:00}:{seconds:00}";
        }

        void EnsureResourceBindings()
        {
            if (resourceParent == null)
            {
                var found = FindNamed(transform, "resourceParent");
                resourceParent = found as RectTransform;
            }

            if (resourceParent == null)
            {
                if (!_loggedMissingResourceParent)
                {
                    _loggedMissingResourceParent = true;
                    Debug.LogError("[ItemDetail] resourceParent is missing. Bind the existing designer object; do not create a new Resource UI.");
                }

                return;
            }

            var resourceEnergy = FindNamed(resourceParent, EnergyHudView.EnergyRootName);
            if (resourceEnergy != null)
            {
                var energyHud = resourceEnergy.GetComponent<EnergyHudView>();
                if (energyHud != null)
                {
                    energyHud.enabled = false;
                }
            }

            if (resourceValueText == null)
            {
                var value = FindNamed(resourceParent, EnergyHudView.ValueTextName);
                resourceValueText = value != null ? value.GetComponent<TMP_Text>() : null;
            }

            if (resourceValueText == null && !_loggedMissingValue)
            {
                _loggedMissingValue = true;
                Debug.LogError("[ItemDetail] Resource value TMP is missing under resourceParent.");
            }

            if (resourceTimerContainer == null)
            {
                var timer = FindNamed(resourceParent, EnergyHudView.TimerContainerName);
                resourceTimerContainer = timer != null ? timer.gameObject : null;
            }

            if (resourceTimerText == null && resourceTimerContainer != null)
            {
                var timerText = resourceTimerContainer.transform.Find(EnergyHudView.TimerTextName);
                if (timerText == null)
                {
                    timerText = FindNamed(resourceTimerContainer.transform, EnergyHudView.TimerTextName);
                }

                resourceTimerText = timerText != null ? timerText.GetComponent<TMP_Text>() : null;
            }

            if ((resourceTimerContainer == null || resourceTimerText == null) && !_loggedMissingTimer)
            {
                _loggedMissingTimer = true;
                Debug.LogError("[ItemDetail] Timer TMP is missing under resourceParent. Existing designer timer was not found; not creating a new one.");
            }
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            var direct = root.Find(objectName);
            if (direct != null)
            {
                return direct;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var nested = FindNamed(root.GetChild(i), objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
