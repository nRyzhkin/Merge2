using System;

namespace SanIsland.Merge
{
    public sealed class EnergyService
    {
        public const int DefaultMaxNaturalEnergy = 100;
        public const float DefaultRegenSecondsPerEnergy = 120f;
        public const int GeneratorProductionCost = 1;

        readonly EnergyState _state = new EnergyState();
        readonly IGameTimeProvider _time;
        bool _resolvingRegen;

        public EnergyService(IGameTimeProvider timeProvider)
        {
            _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            ResetToFull();
        }

        public EnergyState State => _state;

        public event Action Changed;
        public event Action Spent;

        public int GetCurrentEnergy()
        {
            ResolveRegeneration();
            return _state.CurrentEnergy;
        }

        public int GetMaxNaturalEnergy()
        {
            return _state.MaxNaturalEnergy;
        }

        public bool CanSpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            ResolveRegeneration();
            return _state.CurrentEnergy >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            ResolveRegeneration();
            if (_state.CurrentEnergy < amount)
            {
                return false;
            }

            _state.CurrentEnergy -= amount;
            EnsureRegenTimerStarted();
            Changed?.Invoke();
            Spent?.Invoke();
            return true;
        }

        public void AddEnergy(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            ResolveRegeneration();
            _state.CurrentEnergy += amount;
            if (_state.CurrentEnergy >= _state.MaxNaturalEnergy)
            {
                _state.NextEnergyReadyTimestamp = 0d;
            }
            else
            {
                EnsureRegenTimerStarted();
            }

            Changed?.Invoke();
        }

        public void ResolveRegeneration()
        {
            if (_resolvingRegen)
            {
                return;
            }

            _resolvingRegen = true;
            try
            {
                ResolveRegenerationUnguarded();
            }
            finally
            {
                _resolvingRegen = false;
            }
        }

        void ResolveRegenerationUnguarded()
        {
            var now = Now();
            if (_state.CurrentEnergy >= _state.MaxNaturalEnergy)
            {
                _state.NextEnergyReadyTimestamp = 0d;
                return;
            }

            if (_state.NextEnergyReadyTimestamp <= 0d)
            {
                EnsureRegenTimerStarted(now);
                return;
            }

            var regenSeconds = EffectiveRegenSeconds();
            var changed = false;
            while (_state.CurrentEnergy < _state.MaxNaturalEnergy && now >= _state.NextEnergyReadyTimestamp)
            {
                _state.CurrentEnergy++;
                changed = true;
                if (_state.CurrentEnergy >= _state.MaxNaturalEnergy)
                {
                    _state.NextEnergyReadyTimestamp = 0d;
                    break;
                }

                _state.NextEnergyReadyTimestamp += regenSeconds;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public float GetSecondsUntilNextEnergy()
        {
            ResolveRegeneration();
            if (_state.CurrentEnergy >= _state.MaxNaturalEnergy || _state.NextEnergyReadyTimestamp <= 0d)
            {
                return 0f;
            }

            return (float)Math.Max(0d, _state.NextEnergyReadyTimestamp - Now());
        }

        public float GetNextEnergyProgress()
        {
            ResolveRegeneration();
            var regenSeconds = EffectiveRegenSeconds();
            if (_state.CurrentEnergy >= _state.MaxNaturalEnergy ||
                _state.NextEnergyReadyTimestamp <= 0d ||
                regenSeconds <= 0f)
            {
                return 0f;
            }

            var now = Now();
            var cycleStart = _state.NextEnergyReadyTimestamp - regenSeconds;
            return (float)Math.Clamp((now - cycleStart) / regenSeconds, 0d, 1d);
        }

        public void ResetToFull()
        {
            _state.MaxNaturalEnergy = DefaultMaxNaturalEnergy;
            _state.RegenSecondsPerEnergy = DefaultRegenSecondsPerEnergy;
            _state.CurrentEnergy = DefaultMaxNaturalEnergy;
            _state.NextEnergyReadyTimestamp = 0d;
            Changed?.Invoke();
        }

        public void DebugSetEnergy(int value)
        {
            ResolveRegeneration();
            _state.CurrentEnergy = Math.Max(0, value);
            if (_state.CurrentEnergy >= _state.MaxNaturalEnergy)
            {
                _state.NextEnergyReadyTimestamp = 0d;
            }
            else
            {
                EnsureRegenTimerStarted();
            }

            Changed?.Invoke();
        }

        void EnsureRegenTimerStarted()
        {
            EnsureRegenTimerStarted(Now());
        }

        void EnsureRegenTimerStarted(double now)
        {
            if (_state.CurrentEnergy >= _state.MaxNaturalEnergy)
            {
                _state.NextEnergyReadyTimestamp = 0d;
                return;
            }

            if (_state.NextEnergyReadyTimestamp > 0d)
            {
                return;
            }

            _state.NextEnergyReadyTimestamp = now + EffectiveRegenSeconds();
        }

        float EffectiveRegenSeconds()
        {
            return _state.RegenSecondsPerEnergy > 0f
                ? _state.RegenSecondsPerEnergy
                : DefaultRegenSecondsPerEnergy;
        }

        double Now()
        {
            return _time.UnixTimeNow;
        }
    }
}
