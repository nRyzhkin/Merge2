using System;
using System.Collections.Generic;

namespace SanIsland.Merge
{
    public sealed class GeneratorInstanceService
    {
        const int MaxChargeCatchup = 64;

        readonly Dictionary<int, GeneratorInstanceRuntime> _instances = new Dictionary<int, GeneratorInstanceRuntime>(32);
        int _nextInstanceId = 1;

        public void Clear()
        {
            _instances.Clear();
            _nextInstanceId = 1;
        }

        public int CreateFullInstance(int generatorItemId, int maxAvailableDrops)
        {
            var instanceId = _nextInstanceId++;
            _instances[instanceId] = new GeneratorInstanceRuntime
            {
                InstanceId = instanceId,
                GeneratorItemId = generatorItemId,
                AvailableDrops = Math.Max(0, maxAvailableDrops),
                RechargeStartTimestamp = 0d,
                RechargeEndTimestamp = 0d
            };
            return instanceId;
        }

        public void RemoveInstance(int instanceId)
        {
            if (instanceId == BoardCellState.NoGeneratorInstanceId)
            {
                return;
            }

            _instances.Remove(instanceId);
        }

        public bool TryGetRuntime(int instanceId, out GeneratorInstanceRuntime runtime)
        {
            if (instanceId == BoardCellState.NoGeneratorInstanceId)
            {
                runtime = null;
                return false;
            }

            return _instances.TryGetValue(instanceId, out runtime) && runtime != null;
        }

        public bool ApplyRecharge(GeneratorInstanceRuntime runtime, GeneratorData definition, double now, float rechargeSecondsPerCharge)
        {
            if (runtime == null || definition == null)
            {
                return false;
            }

            var max = definition.MaxAvailableDrops;
            var perCharge = definition.DropsPerCharge;
            if (max <= 0 || perCharge <= 0)
            {
                return false;
            }

            if (runtime.AvailableDrops >= max)
            {
                ClearRecharge(runtime);
                return false;
            }

            if (!IsRechargeRunning(runtime))
            {
                if (ShouldStartRecharge(runtime.AvailableDrops, max, perCharge))
                {
                    StartRecharge(runtime, now, rechargeSecondsPerCharge);
                }

                return false;
            }

            if (now < runtime.RechargeEndTimestamp)
            {
                return false;
            }

            var changed = false;
            var duration = Math.Max(0.01d, rechargeSecondsPerCharge);
            for (var i = 0; i < MaxChargeCatchup && runtime.AvailableDrops < max; i++)
            {
                if (now < runtime.RechargeEndTimestamp)
                {
                    break;
                }

                runtime.AvailableDrops = Math.Min(max, runtime.AvailableDrops + perCharge);
                changed = true;
                if (runtime.AvailableDrops >= max)
                {
                    ClearRecharge(runtime);
                    return true;
                }

                runtime.RechargeStartTimestamp = runtime.RechargeEndTimestamp;
                runtime.RechargeEndTimestamp = runtime.RechargeStartTimestamp + duration;
            }

            return changed;
        }

        public bool ResolveCooldown(GeneratorInstanceRuntime runtime, GeneratorData definition, double now, float rechargeSecondsPerCharge)
        {
            return ApplyRecharge(runtime, definition, now, rechargeSecondsPerCharge);
        }

        public bool IsExhausted(GeneratorInstanceRuntime runtime)
        {
            return runtime != null && runtime.AvailableDrops <= 0;
        }

        public bool IsOnCooldown(GeneratorInstanceRuntime runtime, double now)
        {
            return IsExhausted(runtime) && IsRechargeRunning(runtime) && now < runtime.RechargeEndTimestamp;
        }

        public void OnDropConsumed(GeneratorInstanceRuntime runtime, GeneratorData definition, double now, float rechargeSecondsPerCharge)
        {
            if (runtime == null || definition == null)
            {
                return;
            }

            runtime.AvailableDrops = Math.Max(0, runtime.AvailableDrops - 1);
            var max = definition.MaxAvailableDrops;
            if (runtime.AvailableDrops >= max)
            {
                ClearRecharge(runtime);
                return;
            }

            if (IsRechargeRunning(runtime))
            {
                return;
            }

            if (ShouldStartRecharge(runtime.AvailableDrops, max, definition.DropsPerCharge))
            {
                StartRecharge(runtime, now, rechargeSecondsPerCharge);
            }
        }

        public float GetNextChargeProgress(GeneratorInstanceRuntime runtime, double now)
        {
            if (runtime == null || !IsRechargeRunning(runtime))
            {
                return -1f;
            }

            if (now >= runtime.RechargeEndTimestamp)
            {
                return 1f;
            }

            var duration = runtime.RechargeEndTimestamp - runtime.RechargeStartTimestamp;
            if (duration <= 0d)
            {
                return -1f;
            }

            return (float)Math.Clamp((now - runtime.RechargeStartTimestamp) / duration, 0d, 1d);
        }

        public float GetCooldownProgress(GeneratorInstanceRuntime runtime, double now)
        {
            if (!IsExhausted(runtime))
            {
                return -1f;
            }

            return GetNextChargeProgress(runtime, now);
        }

        public float GetSecondsUntilNextCharge(GeneratorInstanceRuntime runtime, double now)
        {
            if (runtime == null || !IsRechargeRunning(runtime))
            {
                return 0f;
            }

            return (float)Math.Max(0d, runtime.RechargeEndTimestamp - now);
        }

        public float GetCooldownRemaining(GeneratorInstanceRuntime runtime, double now)
        {
            if (!IsExhausted(runtime))
            {
                return 0f;
            }

            return GetSecondsUntilNextCharge(runtime, now);
        }

        public bool IsRechargeRunning(GeneratorInstanceRuntime runtime)
        {
            return runtime != null && runtime.RechargeEndTimestamp > 0d;
        }

        static bool ShouldStartRecharge(int availableDrops, int maxAvailableDrops, int dropsPerCharge)
        {
            if (dropsPerCharge <= 0)
            {
                return false;
            }

            return maxAvailableDrops - availableDrops >= dropsPerCharge;
        }

        static void StartRecharge(GeneratorInstanceRuntime runtime, double now, float rechargeSecondsPerCharge)
        {
            var duration = Math.Max(0.01d, rechargeSecondsPerCharge);
            runtime.RechargeStartTimestamp = now;
            runtime.RechargeEndTimestamp = now + duration;
        }

        static void ClearRecharge(GeneratorInstanceRuntime runtime)
        {
            runtime.RechargeStartTimestamp = 0d;
            runtime.RechargeEndTimestamp = 0d;
        }
    }
}
