using System;
using System.Collections.Generic;

namespace SanIsland.Merge
{
    public sealed class GeneratorInstanceService
    {
        readonly Dictionary<int, GeneratorInstanceRuntime> _instances = new Dictionary<int, GeneratorInstanceRuntime>(32);
        int _nextInstanceId = 1;

        public void Clear()
        {
            _instances.Clear();
            _nextInstanceId = 1;
        }

        public int CreateFullInstance(int generatorItemId, int capacityDrops)
        {
            var instanceId = _nextInstanceId++;
            _instances[instanceId] = new GeneratorInstanceRuntime
            {
                InstanceId = instanceId,
                GeneratorItemId = generatorItemId,
                AvailableDrops = capacityDrops,
                CooldownStartTimestamp = 0d,
                CooldownEndTimestamp = 0d
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

        public bool ResolveCooldown(GeneratorInstanceRuntime runtime, GeneratorData definition, double now)
        {
            if (runtime == null || definition == null)
            {
                return false;
            }

            if (runtime.AvailableDrops > 0)
            {
                ClearCooldown(runtime);
                return false;
            }

            if (runtime.CooldownEndTimestamp <= 0d)
            {
                return false;
            }

            if (now < runtime.CooldownEndTimestamp)
            {
                return false;
            }

            runtime.AvailableDrops = definition.CapacityDrops;
            ClearCooldown(runtime);
            return true;
        }

        public bool IsOnCooldown(GeneratorInstanceRuntime runtime, double now)
        {
            return runtime != null &&
                   runtime.AvailableDrops <= 0 &&
                   runtime.CooldownEndTimestamp > now;
        }

        public void OnDropConsumed(GeneratorInstanceRuntime runtime, GeneratorData definition, double now, float cooldownSeconds)
        {
            if (runtime == null || definition == null)
            {
                return;
            }

            runtime.AvailableDrops = Math.Max(0, runtime.AvailableDrops - 1);
            if (runtime.AvailableDrops > 0)
            {
                ClearCooldown(runtime);
                return;
            }

            runtime.CooldownStartTimestamp = now;
            runtime.CooldownEndTimestamp = now + cooldownSeconds;
        }

        public float GetCooldownProgress(GeneratorInstanceRuntime runtime, double now)
        {
            if (runtime == null || runtime.AvailableDrops > 0)
            {
                return -1f;
            }

            if (runtime.CooldownEndTimestamp <= runtime.CooldownStartTimestamp)
            {
                return -1f;
            }

            if (now >= runtime.CooldownEndTimestamp)
            {
                return 1f;
            }

            var duration = runtime.CooldownEndTimestamp - runtime.CooldownStartTimestamp;
            return (float)Math.Clamp((now - runtime.CooldownStartTimestamp) / duration, 0d, 1d);
        }

        public float GetCooldownRemaining(GeneratorInstanceRuntime runtime, double now)
        {
            if (!IsOnCooldown(runtime, now))
            {
                return 0f;
            }

            return (float)Math.Max(0d, runtime.CooldownEndTimestamp - now);
        }

        static void ClearCooldown(GeneratorInstanceRuntime runtime)
        {
            runtime.CooldownStartTimestamp = 0d;
            runtime.CooldownEndTimestamp = 0d;
        }
    }
}
