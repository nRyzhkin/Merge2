using System;
using UnityEngine;

namespace SanIsland.Merge
{
    [DefaultExecutionOrder(-180)]
    [DisallowMultipleComponent]
    public class CurrencySystem : MonoBehaviour
    {
        public static CurrencySystem Current { get; private set; }

        CurrencyService _service;

        public CurrencyService Service
        {
            get
            {
                EnsureReady();
                return _service;
            }
        }

        public event Action Changed
        {
            add
            {
                EnsureReady();
                _service.Changed += value;
            }
            remove
            {
                if (_service != null)
                {
                    _service.Changed -= value;
                }
            }
        }

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[CurrencySystem] Duplicate CurrencySystem in the scene. Keeping the first instance.");
            }
            else
            {
                Current = this;
            }

            EnsureReady();
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void EnsureReady()
        {
            if (_service == null)
            {
                _service = new CurrencyService();
            }
        }
    }
}
