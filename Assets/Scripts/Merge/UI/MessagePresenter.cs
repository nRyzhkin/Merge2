using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SanIsland.Merge
{
    [DisallowMultipleComponent]
    public class MessagePresenter : MonoBehaviour
    {
        const int InitialPoolPerKind = 2;

        [SerializeField] RectTransform messagesRoot;
        [SerializeField] RectTransform infoTemplate;
        [SerializeField] RectTransform warningTemplate;
        [SerializeField] BoardGeneratorAnimationConfig config;
        [SerializeField] Canvas rootCanvas;

        readonly List<MessageView> _infoPool = new List<MessageView>(4);
        readonly List<MessageView> _warningPool = new List<MessageView>(4);

        public void Configure(
            RectTransform layer,
            RectTransform whiteTemplate,
            RectTransform roseTemplate,
            BoardGeneratorAnimationConfig animationConfig,
            Canvas canvas)
        {
            messagesRoot = layer;
            infoTemplate = whiteTemplate;
            warningTemplate = roseTemplate;
            config = animationConfig;
            rootCanvas = canvas;
            HideTemplates();
            EnsurePools();
        }

        public void SetConfig(BoardGeneratorAnimationConfig animationConfig)
        {
            config = animationConfig;
        }

        public void ShowLocalized(MessageKind kind, string localizationKey, Vector2 screenPosition)
        {
            var text = MergeUiLocalization.Get(localizationKey);
            Show(kind, text, screenPosition);
        }

        public void ShowBoardFull(Vector2 screenPosition)
        {
            ShowLocalized(MessageKind.Info, MergeUiLocalization.BoardFullKey, screenPosition);
        }

        public void ShowGeneratorRecharging(Vector2 screenPosition)
        {
            ShowLocalized(MessageKind.Info, MergeUiLocalization.GeneratorRechargingKey, screenPosition);
        }

        public void ShowNotEnoughEnergy(Vector2 screenPosition)
        {
            ShowLocalized(MessageKind.Warning, MergeUiLocalization.NotEnoughEnergyKey, screenPosition);
        }

        public void Show(MessageKind kind, string text, Vector2 screenPosition)
        {
            if (messagesRoot == null || config == null)
            {
                return;
            }

            EnsurePools();
            var view = Rent(kind);
            if (view == null)
            {
                return;
            }

            var anchored = ScreenToMessagesLocal(screenPosition);
            view.Play(kind, text, anchored, config);
        }

        Vector2 ScreenToMessagesLocal(Vector2 screenPosition)
        {
            var canvas = rootCanvas != null
                ? rootCanvas
                : messagesRoot != null ? messagesRoot.GetComponentInParent<Canvas>() : null;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                messagesRoot,
                screenPosition,
                camera,
                out var local);
            return local;
        }

        MessageView Rent(MessageKind kind)
        {
            var pool = kind == MessageKind.Warning ? _warningPool : _infoPool;
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && !pool[i].IsPlaying)
                {
                    return pool[i];
                }
            }

            return CreateMessage(kind);
        }

        void EnsurePools()
        {
            while (_infoPool.Count < InitialPoolPerKind)
            {
                CreateMessage(MessageKind.Info);
            }

            while (_warningPool.Count < InitialPoolPerKind)
            {
                CreateMessage(MessageKind.Warning);
            }
        }

        MessageView CreateMessage(MessageKind kind)
        {
            var template = kind == MessageKind.Warning ? warningTemplate : infoTemplate;
            if (template == null || messagesRoot == null)
            {
                return null;
            }

            var instance = Instantiate(template, messagesRoot, false);
            instance.name = kind == MessageKind.Warning ? "ToastMessage_Warning_Pooled" : "ToastMessage_Info_Pooled";
            instance.gameObject.SetActive(false);
            var view = instance.GetComponent<MessageView>();
            if (view == null)
            {
                view = instance.gameObject.AddComponent<MessageView>();
            }

            var group = instance.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = instance.gameObject.AddComponent<CanvasGroup>();
            }

            var label = instance.GetComponentInChildren<TextMeshProUGUI>(true);
            view.Bind(instance, group, label);
            if (kind == MessageKind.Warning)
            {
                _warningPool.Add(view);
            }
            else
            {
                _infoPool.Add(view);
            }

            return view;
        }

        void HideTemplates()
        {
            if (infoTemplate != null)
            {
                infoTemplate.gameObject.SetActive(false);
            }

            if (warningTemplate != null)
            {
                warningTemplate.gameObject.SetActive(false);
            }
        }
    }
}
