using System;
using UnityEngine;

namespace SanIsland.Merge
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class EnergySystem : MonoBehaviour
    {
        public static EnergySystem Current { get; private set; }

        readonly GameTimeProvider _time = new GameTimeProvider();
        EnergyService _service;

        public GameTimeProvider Time
        {
            get
            {
                EnsureReady();
                return _time;
            }
        }

        public EnergyService Service
        {
            get
            {
                EnsureReady();
                return _service;
            }
        }

        public event Action SpendRejected;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[EnergySystem] Duplicate EnergySystem in the scene. Keeping the first instance.");
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
                _service = new EnergyService(_time);
            }
        }

        void Update()
        {
            if (_service != null)
            {
                _service.ResolveRegeneration();
            }
        }

        public void NotifySpendRejected()
        {
            SpendRejected?.Invoke();
        }

        public void DebugSetEnergy(int value)
        {
            Service.DebugSetEnergy(value);
        }

        public void DebugAdvanceEnergyTime(double seconds)
        {
            _time.AdvanceDebugOffset(seconds);
            Service.ResolveRegeneration();
        }
    }
}
