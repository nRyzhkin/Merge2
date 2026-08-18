using System.Collections.Generic;
using UnityEngine;

namespace SanIsland.Merge
{
    public sealed class ProceduralOrderGenerator
    {
        readonly OrderGenerationConfig _config;
        readonly List<MergeItemFamily> _families = new List<MergeItemFamily>(8);
        readonly List<int> _weights = new List<int>(8);
        readonly List<MergeItemData> _itemsA = new List<MergeItemData>(16);
        readonly List<MergeItemData> _itemsB = new List<MergeItemData>(16);
        readonly List<OrderRequirement> _sortedA = new List<OrderRequirement>(4);
        readonly List<OrderRequirement> _sortedB = new List<OrderRequirement>(4);

        public ProceduralOrderGenerator(OrderGenerationConfig config)
        {
            _config = config;
        }

        public GeneratedOrderData TryGenerate(
            OrderDifficulty difficulty,
            IReadOnlyList<OrderDefinition> activeOrders,
            GameProgressionState progression,
            MergeDiscoveryState discovery,
            MergeItemDatabase items,
            BoardState board,
            EconomyConfig economy,
            IGeneratorRandom rng,
            out string failReason)
        {
            failReason = null;
            if (_config == null)
            {
                failReason = "No generation config";
                return null;
            }

            if (items == null || rng == null)
            {
                failReason = "Missing item database or RNG";
                return null;
            }

            CollectEligibleFamilies(progression, items);
            if (_families.Count == 0)
            {
                failReason = progression == null || progression.GetUnlockedFamilies().Count == 0
                    ? "No eligible unlocked families"
                    : "No producible family";
                return null;
            }

            var band = _config.GetBand(difficulty);
            if (band == null)
            {
                failReason = "No valid requirement for difficulty band";
                return null;
            }

            GeneratedOrderData nearest = null;
            var nearestDistance = int.MaxValue;
            var attempts = _config.MaxGenerationAttempts;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var candidate = BuildCandidate(band, activeOrders, discovery, items, rng);
                if (candidate == null || candidate.requirements == null || candidate.requirements.Count == 0)
                {
                    continue;
                }

                if (IsDuplicate(candidate.requirements, activeOrders))
                {
                    continue;
                }

                var cost = TotalCost(candidate.requirements, items);
                if (cost >= band.minCost && cost <= band.maxCost)
                {
                    candidate.coinReward = CalculateReward(cost, difficulty, candidate.requirements, items, economy);
                    return candidate;
                }

                var distance = cost < band.minCost ? band.minCost - cost : cost - band.maxCost;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            if (nearest != null)
            {
                var cost = TotalCost(nearest.requirements, items);
                nearest.coinReward = CalculateReward(cost, difficulty, nearest.requirements, items, economy);
                return nearest;
            }

            failReason = "No valid requirement for difficulty band";
            return null;
        }

        public static int CalculateItemCost(int level)
        {
            if (level < 1)
            {
                return 1;
            }

            if (level > 16)
            {
                level = 16;
            }

            return 1 << (level - 1);
        }

        public static long CalculateReward(
            int totalDifficultyCost,
            OrderDifficulty difficulty,
            IReadOnlyList<OrderRequirement> requirements,
            MergeItemDatabase items,
            EconomyConfig economy,
            OrderGenerationConfig config)
        {
            var band = config != null ? config.GetBand(difficulty) : null;
            var multiplier = band != null && band.rewardMultiplier > 0 ? band.rewardMultiplier : 12;
            var reward = (long)totalDifficultyCost * multiplier;
            var sellTotal = TotalSellValue(requirements, items, economy);
            var sellMultiplier = config != null ? config.MinimumOrderVsSellMultiplier : 1.25f;
            var sellFloor = (long)System.Math.Ceiling(sellTotal * sellMultiplier);
            if (reward < sellFloor)
            {
                reward = sellFloor;
            }

            return reward < 1 ? 1 : reward;
        }

        // V1 producer check. Replace later with progression-owned generators.
        // Content generator existence — not "is a generator occupying a board cell right now".
        public static bool FamilyHasProducer(MergeItemFamily family, MergeItemDatabase items)
        {
            return items != null && items.TryGetLowestGenerator(family, out var generator) && generator != null;
        }

        public static bool FamilyHasProducerOnBoard(
            MergeItemFamily family,
            BoardState board,
            MergeItemDatabase items)
        {
            if (board == null || items == null)
            {
                return false;
            }

            for (var i = 0; i < BoardState.CellCount; i++)
            {
                var cell = board.GetCell(i);
                if (cell == null || !cell.HasItem)
                {
                    continue;
                }

                if (!items.TryGetById(cell.ItemId, out var data) || data == null)
                {
                    continue;
                }

                if (data.Kind == MergeItemKind.Generator && data.Family == family)
                {
                    return true;
                }
            }

            return false;
        }

        long CalculateReward(
            int totalDifficultyCost,
            OrderDifficulty difficulty,
            IReadOnlyList<OrderRequirement> requirements,
            MergeItemDatabase items,
            EconomyConfig economy)
        {
            return CalculateReward(totalDifficultyCost, difficulty, requirements, items, economy, _config);
        }

        void CollectEligibleFamilies(
            GameProgressionState progression,
            MergeItemDatabase items)
        {
            _families.Clear();
            _weights.Clear();
            if (_config.Families == null || progression == null)
            {
                return;
            }

            for (var i = 0; i < _config.Families.Count; i++)
            {
                var settings = _config.Families[i];
                if (settings == null || !settings.enabledForOrders || settings.weight <= 0)
                {
                    continue;
                }

                if (!progression.IsFamilyUnlocked(settings.family))
                {
                    continue;
                }

                if (!FamilyHasProducer(settings.family, items))
                {
                    continue;
                }

                _families.Add(settings.family);
                _weights.Add(settings.weight);
            }
        }

        GeneratedOrderData BuildCandidate(
            OrderDifficultyBand band,
            IReadOnlyList<OrderDefinition> activeOrders,
            MergeDiscoveryState discovery,
            MergeItemDatabase items,
            IGeneratorRandom rng)
        {
            var reqCount = PickRequirementCount(band, rng);
            var multiFamily = reqCount >= 2 &&
                              _families.Count >= 2 &&
                              rng.NextInt(0, 100) < Mathf.RoundToInt(band.multiFamilyChance * 100f);

            var familyA = PickFamily(activeOrders, items, rng);
            if (!_families.Contains(familyA) && _families.Count > 0)
            {
                familyA = _families[0];
            }

            if (reqCount <= 1)
            {
                CollectEligibleItems(familyA, discovery, items, _itemsA);
                return BuildOneRequirement(_itemsA, band, rng);
            }

            if (multiFamily)
            {
                var familyB = PickFamily(activeOrders, items, rng, familyA);
                if (familyB == familyA || !_families.Contains(familyB))
                {
                    return BuildTwoFromOneFamily(familyA, discovery, items, band, rng);
                }

                CollectEligibleItems(familyA, discovery, items, _itemsA);
                CollectEligibleItems(familyB, discovery, items, _itemsB);
                return BuildTwoRequirements(_itemsA, _itemsB, band, rng, sameItemAllowed: false);
            }

            return BuildTwoFromOneFamily(familyA, discovery, items, band, rng);
        }

        GeneratedOrderData BuildTwoFromOneFamily(
            MergeItemFamily family,
            MergeDiscoveryState discovery,
            MergeItemDatabase items,
            OrderDifficultyBand band,
            IGeneratorRandom rng)
        {
            CollectEligibleItems(family, discovery, items, _itemsA);
            if (_itemsA.Count >= 2)
            {
                CollectEligibleItems(family, discovery, items, _itemsB);
                var two = BuildTwoRequirements(_itemsA, _itemsB, band, rng, sameItemAllowed: true);
                if (two != null)
                {
                    return two;
                }
            }

            return BuildOneRequirement(_itemsA, band, rng);
        }

        GeneratedOrderData BuildOneRequirement(
            List<MergeItemData> pool,
            OrderDifficultyBand band,
            IGeneratorRandom rng)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            Shuffle(pool, rng);
            GeneratedOrderData nearest = null;
            var nearestDistance = int.MaxValue;
            var maxAmount = _config.MaxAmountPerRequirement;
            var hasHigherThanL1 = HasLevelAbove(pool, 1);
            for (var i = 0; i < pool.Count; i++)
            {
                var item = pool[i];
                if (item == null)
                {
                    continue;
                }

                var unit = CalculateItemCost(item.Level);
                for (var amount = 1; amount <= maxAmount; amount++)
                {
                    if (amount > 1 && item.Level <= 1 && hasHigherThanL1)
                    {
                        continue;
                    }

                    var cost = unit * amount;
                    var data = MakeOrder(item.Id, amount);
                    if (cost >= band.minCost && cost <= band.maxCost)
                    {
                        return data;
                    }

                    var distance = cost < band.minCost ? band.minCost - cost : cost - band.maxCost;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = data;
                    }
                }
            }

            return nearest;
        }

        GeneratedOrderData BuildTwoRequirements(
            List<MergeItemData> poolA,
            List<MergeItemData> poolB,
            OrderDifficultyBand band,
            IGeneratorRandom rng,
            bool sameItemAllowed)
        {
            if (poolA == null || poolB == null || poolA.Count == 0 || poolB.Count == 0)
            {
                return null;
            }

            Shuffle(poolA, rng);
            Shuffle(poolB, rng);
            GeneratedOrderData nearest = null;
            var nearestDistance = int.MaxValue;
            var maxAmount = _config.MaxAmountPerRequirement;
            var limitA = Mathf.Min(poolA.Count, 8);
            var limitB = Mathf.Min(poolB.Count, 8);
            for (var a = 0; a < limitA; a++)
            {
                var itemA = poolA[a];
                if (itemA == null)
                {
                    continue;
                }

                for (var b = 0; b < limitB; b++)
                {
                    var itemB = poolB[b];
                    if (itemB == null || !sameItemAllowed && itemA.Id == itemB.Id)
                    {
                        continue;
                    }

                    for (var amountA = 1; amountA <= maxAmount; amountA++)
                    {
                        for (var amountB = 1; amountB <= maxAmount; amountB++)
                        {
                            if (itemA.Id == itemB.Id && amountA > 1)
                            {
                                continue;
                            }

                            var cost = CalculateItemCost(itemA.Level) * amountA +
                                       CalculateItemCost(itemB.Level) * amountB;
                            var data = itemA.Id == itemB.Id
                                ? MakeOrder(itemA.Id, amountA + amountB > maxAmount ? maxAmount : amountA + amountB)
                                : MakeOrder(itemA.Id, amountA, itemB.Id, amountB);
                            if (cost >= band.minCost && cost <= band.maxCost)
                            {
                                return data;
                            }

                            var distance = cost < band.minCost ? band.minCost - cost : cost - band.maxCost;
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearest = data;
                            }
                        }
                    }
                }
            }

            return nearest;
        }

        MergeItemFamily PickFamily(
            IReadOnlyList<OrderDefinition> activeOrders,
            MergeItemDatabase items,
            IGeneratorRandom rng,
            MergeItemFamily? exclude = null)
        {
            if (TryPickWeighted(activeOrders, items, rng, exclude, avoidMonopoly: true, out var family))
            {
                return family;
            }

            if (TryPickWeighted(activeOrders, items, rng, exclude, avoidMonopoly: false, out family))
            {
                return family;
            }

            return _families.Count > 0 ? _families[0] : MergeItemFamily.Tools;
        }

        bool TryPickWeighted(
            IReadOnlyList<OrderDefinition> activeOrders,
            MergeItemDatabase items,
            IGeneratorRandom rng,
            MergeItemFamily? exclude,
            bool avoidMonopoly,
            out MergeItemFamily family)
        {
            family = MergeItemFamily.Tools;
            var total = 0;
            for (var i = 0; i < _families.Count; i++)
            {
                if (exclude.HasValue && _families[i] == exclude.Value)
                {
                    continue;
                }

                if (avoidMonopoly && WouldMonopolize(_families[i], activeOrders, items))
                {
                    continue;
                }

                total += _weights[i];
            }

            if (total <= 0)
            {
                return false;
            }

            var roll = rng.NextInt(0, total);
            var cursor = 0;
            for (var i = 0; i < _families.Count; i++)
            {
                if (exclude.HasValue && _families[i] == exclude.Value)
                {
                    continue;
                }

                if (avoidMonopoly && WouldMonopolize(_families[i], activeOrders, items))
                {
                    continue;
                }

                cursor += _weights[i];
                if (roll < cursor)
                {
                    family = _families[i];
                    return true;
                }
            }

            family = _families[_families.Count - 1];
            return true;
        }

        bool WouldMonopolize(
            MergeItemFamily family,
            IReadOnlyList<OrderDefinition> activeOrders,
            MergeItemDatabase items)
        {
            if (_families.Count <= 1 || _config.SlotCount <= 1)
            {
                return false;
            }

            var onlyThis = 0;
            if (activeOrders != null)
            {
                for (var i = 0; i < activeOrders.Count; i++)
                {
                    if (UsesOnlyFamily(activeOrders[i], family, items))
                    {
                        onlyThis++;
                    }
                }
            }

            return onlyThis + 1 >= _config.SlotCount;
        }

        static bool UsesOnlyFamily(OrderDefinition order, MergeItemFamily family, MergeItemDatabase items)
        {
            if (order == null || order.requirements == null || order.requirements.Count == 0 || items == null)
            {
                return false;
            }

            for (var i = 0; i < order.requirements.Count; i++)
            {
                var requirement = order.requirements[i];
                if (requirement == null || !items.TryGetById(requirement.itemId, out var data) || data == null)
                {
                    return false;
                }

                if (data.Family != family)
                {
                    return false;
                }
            }

            return true;
        }

        void CollectEligibleItems(
            MergeItemFamily family,
            MergeDiscoveryState discovery,
            MergeItemDatabase items,
            List<MergeItemData> buffer)
        {
            buffer.Clear();
            var chain = items.GetNormalChain(family);
            if (chain == null || chain.Count == 0)
            {
                return;
            }

            var maxLevel = MaxAllowedLevel(chain, discovery);
            for (var i = 0; i < chain.Count; i++)
            {
                var item = chain[i];
                if (item != null && item.Kind == MergeItemKind.Normal && item.Level >= 1 && item.Level <= maxLevel)
                {
                    buffer.Add(item);
                }
            }
        }

        public static int GetHighestDiscoveredNormalLevel(
            IReadOnlyList<MergeItemData> chain,
            MergeDiscoveryState discovery)
        {
            var highest = 0;
            if (chain == null)
            {
                return 0;
            }

            for (var i = 0; i < chain.Count; i++)
            {
                var item = chain[i];
                if (item == null || item.Kind != MergeItemKind.Normal)
                {
                    continue;
                }

                if (discovery != null && discovery.IsDiscovered(item.Id) && item.Level > highest)
                {
                    highest = item.Level;
                }
            }

            return highest;
        }

        static int MaxAllowedLevel(IReadOnlyList<MergeItemData> chain, MergeDiscoveryState discovery)
        {
            var chainMax = 1;
            if (chain != null)
            {
                for (var i = 0; i < chain.Count; i++)
                {
                    var item = chain[i];
                    if (item != null && item.Level > chainMax)
                    {
                        chainMax = item.Level;
                    }
                }
            }

            var highestDiscovered = GetHighestDiscoveredNormalLevel(chain, discovery);
            // Undiscovered family still gets L1, otherwise it can never receive a first order.
            var allowed = highestDiscovered < 1 ? 1 : highestDiscovered + 1;
            return allowed > chainMax ? chainMax : allowed;
        }

        int PickRequirementCount(OrderDifficultyBand band, IGeneratorRandom rng)
        {
            var min = band.minRequirements < 1 ? 1 : band.minRequirements;
            var max = band.maxRequirements < min ? min : band.maxRequirements;
            if (max > 2)
            {
                max = 2;
            }

            if (min == max)
            {
                return min;
            }

            return rng.NextInt(min, max + 1);
        }

        bool IsDuplicate(List<OrderRequirement> requirements, IReadOnlyList<OrderDefinition> activeOrders)
        {
            if (activeOrders == null)
            {
                return false;
            }

            CopySorted(requirements, _sortedA);
            for (var i = 0; i < activeOrders.Count; i++)
            {
                var order = activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                CopySorted(order.requirements, _sortedB);
                if (SameSorted(_sortedA, _sortedB))
                {
                    return true;
                }
            }

            return false;
        }

        static void CopySorted(IReadOnlyList<OrderRequirement> source, List<OrderRequirement> dest)
        {
            dest.Clear();
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    dest.Add(source[i]);
                }
            }

            dest.Sort(CompareRequirements);
        }

        static int CompareRequirements(OrderRequirement a, OrderRequirement b)
        {
            if (a.itemId != b.itemId)
            {
                return a.itemId.CompareTo(b.itemId);
            }

            return a.amount.CompareTo(b.amount);
        }

        static bool SameSorted(List<OrderRequirement> a, List<OrderRequirement> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i].itemId != b[i].itemId || a[i].amount != b[i].amount)
                {
                    return false;
                }
            }

            return true;
        }

        static int TotalCost(IReadOnlyList<OrderRequirement> requirements, MergeItemDatabase items)
        {
            var total = 0;
            if (requirements == null || items == null)
            {
                return 0;
            }

            for (var i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];
                if (requirement == null || !items.TryGetById(requirement.itemId, out var data) || data == null)
                {
                    continue;
                }

                total += CalculateItemCost(data.Level) * (requirement.amount < 1 ? 1 : requirement.amount);
            }

            return total;
        }

        static long TotalSellValue(
            IReadOnlyList<OrderRequirement> requirements,
            MergeItemDatabase items,
            EconomyConfig economy)
        {
            long total = 0;
            if (requirements == null || items == null || economy == null)
            {
                return 0;
            }

            for (var i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];
                if (requirement == null || !items.TryGetById(requirement.itemId, out var data) || data == null)
                {
                    continue;
                }

                if (!economy.TryGetSellPrice(data.Level, out var price) || price <= 0)
                {
                    continue;
                }

                total += price * (requirement.amount < 1 ? 1 : requirement.amount);
            }

            return total;
        }

        static GeneratedOrderData MakeOrder(int itemId, int amount)
        {
            return new GeneratedOrderData
            {
                requirements = new List<OrderRequirement>(1)
                {
                    new OrderRequirement { itemId = itemId, amount = amount < 1 ? 1 : amount }
                }
            };
        }

        static GeneratedOrderData MakeOrder(int itemA, int amountA, int itemB, int amountB)
        {
            return new GeneratedOrderData
            {
                requirements = new List<OrderRequirement>(2)
                {
                    new OrderRequirement { itemId = itemA, amount = amountA < 1 ? 1 : amountA },
                    new OrderRequirement { itemId = itemB, amount = amountB < 1 ? 1 : amountB }
                }
            };
        }

        static bool HasLevelAbove(List<MergeItemData> pool, int level)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].Level > level)
                {
                    return true;
                }
            }

            return false;
        }

        static void Shuffle<T>(List<T> list, IGeneratorRandom rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.NextInt(0, i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

#if UNITY_EDITOR
        public string DebugSimulateOrders(
            int count,
            GameProgressionState progression,
            MergeDiscoveryState discovery,
            MergeItemDatabase items,
            BoardState board,
            EconomyConfig economy,
            IGeneratorRandom rng)
        {
            var report = new System.Text.StringBuilder(2048);
            report.AppendLine("[OrderGenerator] DebugGenerateOrders");
            if (progression == null)
            {
                report.AppendLine("Unlocked families: (none / no progression)");
            }
            else
            {
                report.Append("Unlocked families: ");
                var first = true;
                foreach (var family in progression.GetUnlockedFamilies())
                {
                    if (!first)
                    {
                        report.Append(", ");
                    }

                    report.Append(family);
                    first = false;
                }

                if (first)
                {
                    report.Append("(none)");
                }

                report.AppendLine();
            }

            if (_config.Families != null && items != null)
            {
                for (var i = 0; i < _config.Families.Count; i++)
                {
                    var settings = _config.Families[i];
                    if (settings == null)
                    {
                        continue;
                    }

                    var family = settings.family;
                    var chain = items.GetNormalChain(family);
                    var discovered = GetHighestDiscoveredNormalLevel(chain, discovery);
                    var maxLevel = MaxAllowedLevel(chain, discovery);
                    CollectEligibleItems(family, discovery, items, _itemsA);
                    report.Append(family);
                    report.Append(": unlocked=");
                    report.Append(progression != null && progression.IsFamilyUnlocked(family));
                    report.Append(" enabled=");
                    report.Append(settings.enabledForOrders);
                    report.Append(" weight=");
                    report.Append(settings.weight);
                    report.Append(" contentGenerator=");
                    report.Append(FamilyHasProducer(family, items));
                    report.Append(" boardGenerator=");
                    report.Append(FamilyHasProducerOnBoard(family, board, items));
                    report.Append(" discovered=");
                    report.Append(discovered < 1 ? "none" : "L" + discovered);
                    report.Append(" eligible=");
                    if (_itemsA.Count == 0)
                    {
                        report.Append("none");
                    }
                    else
                    {
                        report.Append("L1-L");
                        report.Append(maxLevel);
                        report.Append(" (");
                        report.Append(_itemsA.Count);
                        report.Append(" items)");
                    }

                    report.Append(" bandCandidates Easy=");
                    report.Append(CountBandCandidates(_itemsA, _config.GetBand(OrderDifficulty.Easy)));
                    report.Append(" Medium=");
                    report.Append(CountBandCandidates(_itemsA, _config.GetBand(OrderDifficulty.Medium)));
                    report.Append(" Hard=");
                    report.Append(CountBandCandidates(_itemsA, _config.GetBand(OrderDifficulty.Hard)));
                    report.AppendLine();
                }
            }

            if (count < 1)
            {
                count = 1;
            }

            var familyHits = new Dictionary<MergeItemFamily, int>();
            var levelHits = new Dictionary<int, int>();
            var simulatedActive = new List<OrderDefinition>(4);
            var generated = 0;
            var failed = 0;
            for (var i = 0; i < count; i++)
            {
                var difficulty = _config.GetSlotDifficulty(i % _config.SlotCount);
                var data = TryGenerate(
                    difficulty,
                    simulatedActive,
                    progression,
                    discovery,
                    items,
                    board,
                    economy,
                    rng,
                    out var reason);
                if (data == null || data.requirements == null || data.requirements.Count == 0)
                {
                    failed++;
                    if (failed == 1 && !string.IsNullOrEmpty(reason))
                    {
                        report.AppendLine("First failure: " + reason);
                    }

                    continue;
                }

                generated++;
                var seenFamilies = new HashSet<MergeItemFamily>();
                for (var r = 0; r < data.requirements.Count; r++)
                {
                    var requirement = data.requirements[r];
                    if (requirement == null || !items.TryGetById(requirement.itemId, out var item) || item == null)
                    {
                        continue;
                    }

                    seenFamilies.Add(item.Family);
                    if (!levelHits.ContainsKey(item.Level))
                    {
                        levelHits[item.Level] = 0;
                    }

                    levelHits[item.Level]++;
                }

                foreach (var family in seenFamilies)
                {
                    if (!familyHits.ContainsKey(family))
                    {
                        familyHits[family] = 0;
                    }

                    familyHits[family]++;
                }

                simulatedActive.Add(new OrderDefinition
                {
                    id = 900000 + i,
                    requirements = data.requirements,
                    enabled = true
                });
                if (simulatedActive.Count > _config.SlotCount)
                {
                    simulatedActive.RemoveAt(0);
                }
            }

            report.Append("Generated ");
            report.Append(generated);
            report.Append("/");
            report.Append(count);
            report.Append(" (failed ");
            report.Append(failed);
            report.AppendLine(")");
            report.AppendLine("Family (share of generated orders containing family):");
            var allFamilies = (MergeItemFamily[])System.Enum.GetValues(typeof(MergeItemFamily));
            for (var i = 0; i < allFamilies.Length; i++)
            {
                var family = allFamilies[i];
                familyHits.TryGetValue(family, out var hits);
                var percent = generated <= 0 ? 0f : hits * 100f / generated;
                report.Append("  ");
                report.Append(family);
                report.Append(": ");
                report.Append(percent.ToString("0.0"));
                report.AppendLine("%");
            }

            report.AppendLine("Item levels (share of requirements):");
            var totalLevels = 0;
            foreach (var pair in levelHits)
            {
                totalLevels += pair.Value;
            }

            var levels = new List<int>(levelHits.Keys);
            levels.Sort();
            for (var i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                var percent = totalLevels <= 0 ? 0f : levelHits[level] * 100f / totalLevels;
                report.Append("  L");
                report.Append(level);
                report.Append(": ");
                report.Append(percent.ToString("0.0"));
                report.AppendLine("%");
            }

            return report.ToString();
        }

        int CountBandCandidates(List<MergeItemData> pool, OrderDifficultyBand band)
        {
            if (pool == null || band == null)
            {
                return 0;
            }

            var count = 0;
            var maxAmount = _config.MaxAmountPerRequirement;
            for (var i = 0; i < pool.Count; i++)
            {
                var item = pool[i];
                if (item == null)
                {
                    continue;
                }

                var unit = CalculateItemCost(item.Level);
                for (var amount = 1; amount <= maxAmount; amount++)
                {
                    var cost = unit * amount;
                    if (cost >= band.minCost && cost <= band.maxCost)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
#endif
    }
}
