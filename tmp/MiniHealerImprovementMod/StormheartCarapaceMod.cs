using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MiniHealerImprovementMod
{
    internal static class StormheartCarapaceMod
    {
        internal const string StormheartCarapaceKey = "CODEX_STORMHEART_CARAPACE";
        private const int StormheartCraftCost = 1;
        private const float StormheartFallbackDropWeight = 0.1f;
        private const float CurrentHealthLightningRatio = 0.03f;
        private const string GreaterAlchemyShardFallbackKey = "GR_ALCHEMY_SHARD";

        private static readonly ArtifactAttribute.AttriubteType[] StormheartBaseAttributeTypes =
        {
            ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_ALL_LIGHTNING_RESIST,
            ArtifactAttribute.AttriubteType.DECREASE_ALL_DAMAGE_TAKEN
        };

        private static readonly CustomBaseAttributeSpec StormheartHealthSpec =
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT, 5000f, 5500f, "Party Health");

        private static GuardianDropContext _guardianContext;

        internal static bool TryInjectStormheartCarapace()
        {
            return EnsureStormheartCarapace() != null;
        }

        internal static bool TryInjectStormheartCarapaceLootSource()
        {
            return EnsureStormheartCarapaceLootSources();
        }

        internal static Artifact EnsureStormheartCarapace()
        {
            var controller = ArtifactDataController.ADM ?? Resources.FindObjectsOfTypeAll<ArtifactDataController>().FirstOrDefault();
            var data = controller?.artifactData;
            if (data == null)
            {
                return null;
            }

            var context = GetGuardianContext(data);
            if (data.artifactsMap != null && data.artifactsMap.TryGetValue(StormheartCarapaceKey, out var existingArtifact) && existingArtifact != null)
            {
                ConfigureStormheartCarapace(existingArtifact, controller, context);
                WireStormheartCarapace(existingArtifact);
                return existingArtifact;
            }

            var artifact = CreateStormheartCarapaceArtifact(controller, data, context);
            ModHelpers.ReplaceArtifactCollections(data, artifact);
            return artifact;
        }

        private static Artifact CreateStormheartCarapaceArtifact(ArtifactDataController controller, ArtifactsData data, GuardianDropContext context)
        {
            var artifact = new Artifact();
            var template = FindStormheartTemplate(data, context);
            if (template != null)
            {
                ModHelpers.CopyArtifactTemplate(template, artifact);
            }

            ConfigureStormheartCarapace(artifact, controller, context);
            WireStormheartCarapace(artifact);
            return artifact;
        }

        private static Artifact FindStormheartTemplate(ArtifactsData data, GuardianDropContext context)
        {
            return context?.Armors?.FirstOrDefault(item => item != null && item.Type == Artifact.ArtifactType.BODYARMOR)
                ?? context?.Armors?.FirstOrDefault(item => item != null && item.SlotType == Artifact.ArtifactSlotType.ARMOR)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != StormheartCarapaceKey && item.SlotType == Artifact.ArtifactSlotType.ARMOR && item.Type == Artifact.ArtifactType.BODYARMOR)
                ?? data.ArtifactList?.FirstOrDefault(item => item != null && item.Key != StormheartCarapaceKey && item.SlotType == Artifact.ArtifactSlotType.ARMOR)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != StormheartCarapaceKey);
        }

        private static void ConfigureStormheartCarapace(Artifact artifact, ArtifactDataController controller, GuardianDropContext context)
        {
            ModHelpers.ConfigureCustomArtifact(artifact, controller, new CustomArtifactSpec
            {
                Key = StormheartCarapaceKey,
                Name = "Stormheart Carapace",
                Rarity = Artifact.RarityType.Legendary,
                SlotType = Artifact.ArtifactSlotType.ARMOR,
                Type = Artifact.ArtifactType.BODYARMOR,
                MutationPoolTypes = new List<Artifact.ArtifactCharacterType>
                {
                    Artifact.ArtifactCharacterType.ALL_DEFENSIVE,
                    Artifact.ArtifactCharacterType.ALL
                },
                HiddenItemLevel = 85,
                DropRate = GetGuardianDropWeight(context),
                DroppedBossName = !string.IsNullOrEmpty(context?.Boss?.Key) ? context.Boss.Key : "Guardian",
                DroppedLevelName = !string.IsNullOrEmpty(context?.Level?.Key) ? context.Level.Key : "Guardian",
                PurchaseMaterialFallbackKey = GreaterAlchemyShardFallbackKey,
                PurchasePrice = StormheartCraftCost,
                BaseAttributeTypes = StormheartBaseAttributeTypes,
                SearchText = "Stormheart Carapace body armor lightning health guardian legendary"
            });
        }

        internal static void EnsureStormheartSaveAttributes(ArtifactSaveInfo saveInfo)
        {
            ModHelpers.TryEnsureSaveAttributes(saveInfo, StormheartCarapaceKey, StormheartBaseAttributeTypes);
        }

        internal static void EnsureStormheartSaveAttributes(Artifact artifact, List<ArtifactSaveAttribute> attributes)
        {
            ModHelpers.TryEnsureSaveAttributes(artifact, StormheartCarapaceKey, attributes, StormheartBaseAttributeTypes);
        }

        internal static void EnsureStormheartBaseAttributes(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> attributes)
        {
            if (artifact?.Key != StormheartCarapaceKey || AttributesManager.ATRM == null)
            {
                return;
            }

            attributes = new List<ArtifactAttribute>();
            EnsureStormheartSaveAttributes(saveInfo);
            ModHelpers.AddOrReplaceBaseAttribute(attributes, StormheartHealthSpec.AttributeType, StormheartHealthSpec.MinValue, StormheartHealthSpec.MaxValue);
            AddBalancedBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_ALL_LIGHTNING_RESIST, 8f, 12f);
            AddBalancedBaseAttribute(attributes, ArtifactAttribute.AttriubteType.DECREASE_ALL_DAMAGE_TAKEN, 2f, 4f);
        }

        private static void AddBalancedBaseAttribute(List<ArtifactAttribute> attributes, ArtifactAttribute.AttriubteType attributeType, float fallbackMin, float fallbackMax)
        {
            var range = GetGuardianAttributeRange(attributeType, fallbackMin, fallbackMax);
            ModHelpers.AddOrReplaceBaseAttribute(attributes, attributeType, range.Min, range.Max);
        }

        private static AttributeRange GetGuardianAttributeRange(ArtifactAttribute.AttriubteType attributeType, float fallbackMin, float fallbackMax)
        {
            var context = GetGuardianContext(ArtifactDataController.ADM?.artifactData);
            var exactMatches = GetGuardianBaseAttributes(context)
                .Where(attribute => attribute.attributeType == attributeType)
                .Select(GetAttributeRange)
                .Where(range => range.IsValid)
                .ToList();

            if (exactMatches.Count > 0)
            {
                return AverageRange(exactMatches);
            }

            var desiredName = attributeType.ToString();
            var relatedRanges = GetGuardianBaseAttributes(context)
                .Where(attribute =>
                {
                    var name = attribute.attributeType.ToString();
                    if (desiredName.Contains("HP"))
                    {
                        return name.Contains("HP") && name.EndsWith("_FLAT", StringComparison.Ordinal);
                    }

                    if (desiredName.Contains("RESIST"))
                    {
                        return name.Contains("RESIST");
                    }

                    return name.Contains("DAMAGE_TAKEN");
                })
                .Select(GetAttributeRange)
                .Where(range => range.IsValid)
                .ToList();

            return relatedRanges.Count > 0 ? AverageRange(relatedRanges) : new AttributeRange(fallbackMin, fallbackMax);
        }

        private static IEnumerable<ArtifactAttribute> GetGuardianBaseAttributes(GuardianDropContext context)
        {
            if (context?.Armors == null || AttributesManager.ATRM == null)
            {
                return Enumerable.Empty<ArtifactAttribute>();
            }

            var result = new List<ArtifactAttribute>();
            foreach (var armor in context.Armors.Where(armor => armor != null && armor.Key != StormheartCarapaceKey))
            {
                var baseAttributes = AttributesManager.ATRM.getArtifactBaseAttributes(armor, null, true, false, false);
                if (baseAttributes != null)
                {
                    result.AddRange(baseAttributes.Where(attribute => attribute != null));
                }
            }

            return result;
        }

        private static AttributeRange GetAttributeRange(ArtifactAttribute attribute)
        {
            if (attribute == null)
            {
                return AttributeRange.Invalid;
            }

            var min = attribute.T3_MIN != 0f || attribute.T3_MAX != 0f ? attribute.T3_MIN : attribute.T1_MIN;
            var max = attribute.T3_MIN != 0f || attribute.T3_MAX != 0f ? attribute.T3_MAX : attribute.T1_MAX;
            return max > 0f && max >= min ? new AttributeRange(min, max) : AttributeRange.Invalid;
        }

        private static AttributeRange AverageRange(List<AttributeRange> ranges)
        {
            return new AttributeRange(ranges.Average(range => range.Min), ranges.Average(range => range.Max));
        }

        internal static void AppendStormheartDescription(Artifact artifact, ref List<string> descriptions)
        {
            if (artifact?.Key != StormheartCarapaceKey)
            {
                return;
            }

            ModHelpers.AppendUniqueDescription(ref descriptions, GetStormheartEffectText());
        }

        private static string GetStormheartEffectText()
        {
            return ModHelpers.ColorizeTerms(
                "Party members gain added lightning damage equal to 3% of their current health.",
                new TooltipTerm("lightning damage", ModHelpers.LightningColor),
                new TooltipTerm("current health", ModHelpers.HealthColor));
        }

        internal static bool TryGetStormheartPurchaseMaterial(Artifact artifact, ref StackableMaterial result)
        {
            return ModHelpers.TryGetCustomPurchaseMaterial(artifact, StormheartCarapaceKey, GreaterAlchemyShardFallbackKey, ref result);
        }

        internal static void RefreshStormheartAtlasInfo(ItemAtlasUIManager manager)
        {
            ModHelpers.RefreshAtlasSubtitle(manager, StormheartCarapaceKey, "(Body Armor, Unique)");
        }

        private static bool EnsureStormheartCarapaceLootSources()
        {
            var artifact = EnsureStormheartCarapace();
            var context = GetGuardianContext(ArtifactDataController.ADM?.artifactData);
            if (artifact == null || context?.LevelDifficulty == null || context.Boss == null)
            {
                return false;
            }

            context.LevelDifficulty.Loot = ModHelpers.AppendKeyCopy(context.LevelDifficulty.Loot, StormheartCarapaceKey);
            context.Boss.depthLoot = ModHelpers.AppendKeyCopy(context.Boss.depthLoot, StormheartCarapaceKey);
            return true;
        }

        internal static void AddStormheartToDropTable(string bossKey, LootTableManager.ArtifactLootDropTable table)
        {
            var artifact = EnsureStormheartCarapace();
            var context = GetGuardianContext(ArtifactDataController.ADM?.artifactData);
            if (artifact == null || table?.lootDropItems == null || context?.Boss == null || !string.Equals(context.Boss.Key, bossKey, StringComparison.Ordinal))
            {
                return;
            }

            if (table.lootDropItems.Any(item => item?.item != null && item.item.Key == StormheartCarapaceKey))
            {
                return;
            }

            table.lootDropItems.Add(new LootTableManager.ArtifactLootDropItem
            {
                item = artifact,
                probabilityWeight = GetGuardianDropWeight(context)
            });
        }

        private static float GetGuardianDropWeight(GuardianDropContext context)
        {
            var weights = context?.Armors?
                .Where(item => item != null && item.Key != StormheartCarapaceKey && item.weight > 0f)
                .Select(item => item.weight)
                .ToList();

            return weights != null && weights.Count > 0 ? Mathf.Max(0.01f, weights.Average()) : StormheartFallbackDropWeight;
        }

        private static GuardianDropContext GetGuardianContext(ArtifactsData data)
        {
            if (_guardianContext != null || data?.artifactsMap == null)
            {
                return _guardianContext;
            }

            var levelData = LevelDataController.LDM?.levelData;
            var bossData = BossDataController.BDM?.bossData;
            if (levelData?.Levels == null || bossData?.Bosses == null)
            {
                return null;
            }

            foreach (var level in levelData.Levels.Where(level => level != null && level.isGuardian && level.Difficulties != null).OrderBy(level => level.Key))
            {
                foreach (var difficulty in level.Difficulties.Where(difficulty => difficulty?.Bosses != null))
                {
                    foreach (var bossKey in difficulty.Bosses.Where(key => !string.IsNullOrEmpty(key)))
                    {
                        var boss = bossData.Bosses.FirstOrDefault(candidate => candidate != null && candidate.Key == bossKey);
                        if (boss == null)
                        {
                            continue;
                        }

                        var armors = ResolveGuardianArmors(data, difficulty.Loot, boss.depthLoot);
                        if (armors.Count == 0)
                        {
                            continue;
                        }

                        _guardianContext = new GuardianDropContext
                        {
                            Level = level,
                            LevelDifficulty = difficulty,
                            Boss = boss,
                            Armors = armors
                        };
                        MiniHealerImprovementModPlugin.LogSource?.LogInfo($"Stormheart Carapace matched guardian {boss.Key} with {armors.Count} guardian armor references for balance.");
                        return _guardianContext;
                    }
                }
            }

            return null;
        }

        private static List<Artifact> ResolveGuardianArmors(ArtifactsData data, params List<string>[] keyLists)
        {
            return keyLists
                .Where(list => list != null)
                .SelectMany(list => list)
                .Where(key => !string.IsNullOrEmpty(key) && data.artifactsMap.TryGetValue(key, out var artifact) && artifact != null && artifact.SlotType == Artifact.ArtifactSlotType.ARMOR)
                .Select(key => data.artifactsMap[key])
                .GroupBy(artifact => artifact.Key)
                .Select(group => group.First())
                .ToList();
        }

        private static void WireStormheartCarapace(Artifact artifact)
        {
            artifact.OnGetBaseAttackDamage -= StormheartCarapace_GetBaseAttackDamage;
            artifact.OnGetBaseAttackDamage += StormheartCarapace_GetBaseAttackDamage;
        }

        private static int StormheartCarapace_GetBaseAttackDamage(Artifact artifact, Character attacker, int damage, DamageData.DamageElement element, BattleManager battleManager, ArtifactSaveInfo saveInfo)
        {
            if (attacker == null || attacker.isEnemy || attacker.isDead || element != DamageData.DamageElement.Lightning)
            {
                return damage;
            }

            var currentHealth = Convert.ToInt64(ModHelpers.GetFieldValue(attacker, "m_currentHealth") ?? 0L);
            var bonusDamage = Math.Max(0L, (long)Math.Round(currentHealth * CurrentHealthLightningRatio, MidpointRounding.AwayFromZero));
            var totalDamage = Math.Min(int.MaxValue, Math.Max(0L, damage) + bonusDamage);
            return (int)totalDamage;
        }

        private sealed class GuardianDropContext
        {
            internal Level Level;
            internal LevelDifficultyData LevelDifficulty;
            internal Boss Boss;
            internal List<Artifact> Armors;
        }

        private struct AttributeRange
        {
            internal static readonly AttributeRange Invalid = new AttributeRange(float.NaN, float.NaN);

            internal AttributeRange(float min, float max)
            {
                Min = min;
                Max = max;
            }

            internal float Min { get; }
            internal float Max { get; }
            internal bool IsValid => !float.IsNaN(Min) && !float.IsNaN(Max) && Max >= Min;
        }
    }

}
