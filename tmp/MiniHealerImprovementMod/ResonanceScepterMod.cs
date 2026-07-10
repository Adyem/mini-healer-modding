using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MiniHealerImprovementMod
{
    internal static class ResonanceScepterMod
    {
        internal const string ResonanceScepterKey = "CODEX_RESONANCE_SCEPTER";
        private const string ResonanceShieldKey = "CODEX_RESONANCE_SCEPTER_SHIELD";
        private const int ResonanceCraftCost = 1;
        private const float ResonanceFallbackDropWeight = 0.1f;
        private const int CastsPerPulse = 4;
        private const float PulseHealPowerRatio = 0.25f;
        private const float PulseShieldDuration = 4f;
        private const float PulseInternalCooldown = 3f;
        private const string GreaterAlchemyShardFallbackKey = "GR_ALCHEMY_SHARD";

        private static readonly ArtifactAttribute.AttriubteType[] ResonanceBaseAttributeTypes =
        {
            ArtifactAttribute.AttriubteType.INCREASE_HEALPOWER_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_CASTSPD_PERCENT,
            ArtifactAttribute.AttriubteType.INCREASE_MANA_REGEN_PERCENT
        };

        private static readonly CustomBaseAttributeSpec ResonanceHealPowerSpec =
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_HEALPOWER_FLAT, 500f, 550f, "Heal Power");

        private static readonly CustomBaseAttributeSpec[] ResonanceFallbackAtlasSpecs =
        {
            ResonanceHealPowerSpec,
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_CASTSPD_PERCENT, 12f, 16f, "Cast Speed"),
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_MANA_REGEN_PERCENT, 12f, 16f, "Mana Regen")
        };

        private static readonly Dictionary<Artifact, int> CastCounters = new Dictionary<Artifact, int>();
        private static readonly Dictionary<Artifact, float> LastPulseTimes = new Dictionary<Artifact, float>();
        private static GuardianDropContext _guardianContext;

        internal static bool TryInjectResonanceScepter()
        {
            return EnsureResonanceScepter() != null;
        }

        internal static bool TryInjectResonanceScepterLootSource()
        {
            return EnsureResonanceScepterLootSources();
        }

        internal static Artifact EnsureResonanceScepter()
        {
            var controller = ArtifactDataController.ADM ?? Resources.FindObjectsOfTypeAll<ArtifactDataController>().FirstOrDefault();
            var data = controller?.artifactData;
            if (data == null)
            {
                return null;
            }

            var context = GetGuardianContext(data);
            if (data.artifactsMap != null && data.artifactsMap.TryGetValue(ResonanceScepterKey, out var existingArtifact) && existingArtifact != null)
            {
                ConfigureResonanceScepter(existingArtifact, controller, data, context);
                WireResonanceScepter(existingArtifact);
                return existingArtifact;
            }

            var artifact = CreateResonanceScepterArtifact(controller, data, context);
            ModHelpers.ReplaceArtifactCollections(data, artifact);
            return artifact;
        }

        private static Artifact CreateResonanceScepterArtifact(ArtifactDataController controller, ArtifactsData data, GuardianDropContext context)
        {
            var artifact = new Artifact();
            var template = FindResonanceTemplate(data, context);
            if (template != null)
            {
                ModHelpers.CopyArtifactTemplate(template, artifact);
            }

            ConfigureResonanceScepter(artifact, controller, data, context);
            WireResonanceScepter(artifact);
            return artifact;
        }

        private static Artifact FindResonanceTemplate(ArtifactsData data, GuardianDropContext context)
        {
            return context?.Weapons?.FirstOrDefault(item => item != null && item.Type == Artifact.ArtifactType.STAFF)
                ?? context?.Weapons?.FirstOrDefault(item => item != null && item.SlotType == Artifact.ArtifactSlotType.WEAPON)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != ResonanceScepterKey && item.SlotType == Artifact.ArtifactSlotType.WEAPON && item.Type == Artifact.ArtifactType.STAFF)
                ?? data.ArtifactList?.FirstOrDefault(item => item != null && item.Key != ResonanceScepterKey && item.SlotType == Artifact.ArtifactSlotType.WEAPON)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != ResonanceScepterKey);
        }

        private static void ConfigureResonanceScepter(Artifact artifact, ArtifactDataController controller, ArtifactsData data, GuardianDropContext context)
        {
            ModHelpers.ConfigureCustomArtifact(artifact, controller, new CustomArtifactSpec
            {
                Key = ResonanceScepterKey,
                Name = "Resonance Scepter",
                Rarity = Artifact.RarityType.Legendary,
                SlotType = Artifact.ArtifactSlotType.WEAPON,
                Type = Artifact.ArtifactType.STAFF,
                MutationPoolTypes = new List<Artifact.ArtifactCharacterType>
                {
                    Artifact.ArtifactCharacterType.HEALER_OFFENSIVE,
                    Artifact.ArtifactCharacterType.HEALER_DEFENSIVE
                },
                HiddenItemLevel = 85,
                DropRate = GetGuardianDropWeight(context),
                DroppedBossName = !string.IsNullOrEmpty(context?.Boss?.Key) ? context.Boss.Key : "Guardian",
                DroppedLevelName = !string.IsNullOrEmpty(context?.Level?.Key) ? context.Level.Key : "Guardian",
                PurchaseMaterialFallbackKey = GreaterAlchemyShardFallbackKey,
                PurchasePrice = ResonanceCraftCost,
                FallbackIcon = controller.LifemenderIcon ?? controller.FaithkeeperIcon,
                BaseAttributeTypes = ResonanceBaseAttributeTypes,
                SearchText = "Resonance Scepter staff healer pulse shield heal guardian legendary"
            });
        }

        internal static void EnsureResonanceSaveAttributes(ArtifactSaveInfo saveInfo)
        {
            ModHelpers.TryEnsureSaveAttributes(saveInfo, ResonanceScepterKey, ResonanceBaseAttributeTypes);
        }

        internal static void EnsureResonanceSaveAttributes(Artifact artifact, List<ArtifactSaveAttribute> attributes)
        {
            ModHelpers.TryEnsureSaveAttributes(artifact, ResonanceScepterKey, attributes, ResonanceBaseAttributeTypes);
        }

        internal static void EnsureResonanceBaseAttributes(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> attributes)
        {
            if (artifact?.Key != ResonanceScepterKey || AttributesManager.ATRM == null)
            {
                return;
            }

            attributes = new List<ArtifactAttribute>();
            EnsureResonanceSaveAttributes(saveInfo);
            ModHelpers.AddOrReplaceBaseAttribute(attributes, ResonanceHealPowerSpec.AttributeType, ResonanceHealPowerSpec.MinValue, ResonanceHealPowerSpec.MaxValue);
            AddBalancedBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_CASTSPD_PERCENT, 12f, 16f);
            AddBalancedBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_MANA_REGEN_PERCENT, 12f, 16f);
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
            var isFlat = desiredName.EndsWith("_FLAT", StringComparison.Ordinal);
            var isPercent = desiredName.Contains("_PERCENT") || desiredName.Contains("CASTSPD") || desiredName.Contains("ATT_SPD") || desiredName.Contains("REGEN_PERCENT");
            var relatedRanges = GetGuardianBaseAttributes(context)
                .Where(attribute =>
                {
                    var name = attribute.attributeType.ToString();
                    return (isFlat && name.EndsWith("_FLAT", StringComparison.Ordinal))
                        || (isPercent && (name.Contains("_PERCENT") || name.Contains("CASTSPD") || name.Contains("ATT_SPD") || name.Contains("REGEN_PERCENT")));
                })
                .Select(GetAttributeRange)
                .Where(range => range.IsValid)
                .ToList();

            if (relatedRanges.Count > 0)
            {
                return AverageRange(relatedRanges);
            }

            return new AttributeRange(fallbackMin, fallbackMax);
        }

        private static IEnumerable<ArtifactAttribute> GetGuardianBaseAttributes(GuardianDropContext context)
        {
            if (context?.Weapons == null || AttributesManager.ATRM == null)
            {
                return Enumerable.Empty<ArtifactAttribute>();
            }

            var result = new List<ArtifactAttribute>();
            foreach (var weapon in context.Weapons.Where(weapon => weapon != null && weapon.Key != ResonanceScepterKey && weapon.Key != AegisChoirMod.AegisChoirKey))
            {
                var baseAttributes = AttributesManager.ATRM.getArtifactBaseAttributes(weapon, null, true, false, false);
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

        internal static void AppendResonanceDescription(Artifact artifact, ref List<string> descriptions)
        {
            if (artifact?.Key != ResonanceScepterKey)
            {
                return;
            }

            ModHelpers.AppendUniqueDescription(ref descriptions, GetResonanceEffectText());
        }

        internal static bool TryGetResonancePurchaseMaterial(Artifact artifact, ref StackableMaterial result)
        {
            return ModHelpers.TryGetCustomPurchaseMaterial(artifact, ResonanceScepterKey, GreaterAlchemyShardFallbackKey, ref result);
        }

        internal static void RefreshResonanceAtlasInfo(ItemAtlasUIManager manager)
        {
            var selectedArtifact = ModHelpers.GetFieldValue(manager, "selectedArtifact") as Artifact;
            if (selectedArtifact?.Key != ResonanceScepterKey)
            {
                return;
            }

            ModHelpers.RefreshAtlasSubtitle(manager, ResonanceScepterKey, "(Staff, Unique)");
            ModHelpers.SetTextField(manager, "DescriptionTextMeshPro", GetResonanceAtlasStatsText());
        }

        private static string GetResonanceEffectText()
        {
            return ModHelpers.ColorizeTerms(
                "Every 4th healer spell sends a resonance pulse to the lowest-health living party member, healing and shielding them for 25% of current heal power. This can occur once every 3 seconds.",
                new TooltipTerm("healing", ModHelpers.HealPowerColor),
                new TooltipTerm("shielding", ModHelpers.ShieldColor),
                new TooltipTerm("25% of current heal power", ModHelpers.HealPowerColor));
        }

        private static string GetResonanceAtlasStatsText()
        {
            return ModHelpers.FormatAtlasStats(ResonanceFallbackAtlasSpecs, GetResonanceEffectText());
        }

        private static bool EnsureResonanceScepterLootSources()
        {
            var artifact = EnsureResonanceScepter();
            var context = GetGuardianContext(ArtifactDataController.ADM?.artifactData);
            if (artifact == null || context?.LevelDifficulty == null || context.Boss == null)
            {
                return false;
            }

            context.LevelDifficulty.Loot = ModHelpers.AppendKeyCopy(context.LevelDifficulty.Loot, ResonanceScepterKey);
            context.Boss.depthLoot = ModHelpers.AppendKeyCopy(context.Boss.depthLoot, ResonanceScepterKey);
            return true;
        }

        internal static void AddResonanceToDropTable(string bossKey, LootTableManager.ArtifactLootDropTable table)
        {
            var artifact = EnsureResonanceScepter();
            var context = GetGuardianContext(ArtifactDataController.ADM?.artifactData);
            if (artifact == null || table?.lootDropItems == null || context?.Boss == null || !string.Equals(context.Boss.Key, bossKey, StringComparison.Ordinal))
            {
                return;
            }

            if (table.lootDropItems.Any(item => item?.item != null && item.item.Key == ResonanceScepterKey))
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
            var weights = context?.Weapons?
                .Where(item => item != null && item.Key != ResonanceScepterKey && item.weight > 0f)
                .Select(item => item.weight)
                .ToList();

            return weights != null && weights.Count > 0 ? Mathf.Max(0.01f, weights.Average()) : ResonanceFallbackDropWeight;
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

                        var weapons = ResolveGuardianWeapons(data, difficulty.Loot, boss.depthLoot);
                        if (weapons.Count == 0)
                        {
                            continue;
                        }

                        _guardianContext = new GuardianDropContext
                        {
                            Level = level,
                            LevelDifficulty = difficulty,
                            Boss = boss,
                            Weapons = weapons
                        };
                        MiniHealerImprovementModPlugin.LogSource?.LogInfo($"Resonance Scepter matched guardian {boss.Key} with {weapons.Count} guardian weapon references for balance.");
                        return _guardianContext;
                    }
                }
            }

            return null;
        }

        private static List<Artifact> ResolveGuardianWeapons(ArtifactsData data, params List<string>[] keyLists)
        {
            return keyLists
                .Where(list => list != null)
                .SelectMany(list => list)
                .Where(key => !string.IsNullOrEmpty(key) && data.artifactsMap.TryGetValue(key, out var artifact) && artifact != null && artifact.SlotType == Artifact.ArtifactSlotType.WEAPON)
                .Select(key => data.artifactsMap[key])
                .GroupBy(artifact => artifact.Key)
                .Select(group => group.First())
                .ToList();
        }

        private static void WireResonanceScepter(Artifact artifact)
        {
            artifact.OnCast -= ResonanceScepter_OnCast;
            artifact.OnCast += ResonanceScepter_OnCast;
        }

        private static void ResonanceScepter_OnCast(List<Character> characters, Artifact artifact, Character character, List<Character> targets, Skill skill, BattleManager battleManager)
        {
            if (artifact == null || character == null || character.characterType != OtherGameDataController.CharacterType.Healer || characters == null)
            {
                return;
            }

            CastCounters.TryGetValue(artifact, out var casts);
            casts++;
            CastCounters[artifact] = casts;
            if (casts % CastsPerPulse != 0)
            {
                return;
            }

            LastPulseTimes.TryGetValue(artifact, out var lastPulseTime);
            if (Time.time - lastPulseTime < PulseInternalCooldown)
            {
                return;
            }

            var pulseTarget = characters
                .Where(candidate => candidate != null && !candidate.isEnemy && !candidate.isDead && candidate.canBeHealed)
                .OrderBy(GetHealthPercent)
                .FirstOrDefault();

            if (pulseTarget == null)
            {
                return;
            }

            var amount = GetPulseAmount(battleManager);
            var healData = new HealData
            {
                Amount = amount,
                HealingSource = character,
                canCrit = false,
                associatedSkill = skill
            };
            pulseTarget.heal(healData);
            ApplyPulseShield(character, pulseTarget, artifact, amount);
            battleManager?.spawnArtifactVFX(artifact, pulseTarget);
            LastPulseTimes[artifact] = Time.time;
        }

        private static long GetPulseAmount(BattleManager battleManager)
        {
            var healPower = OtherGameDataController.getCurrentHealPower(battleManager);
            return Math.Max(1L, (long)Math.Round(healPower * PulseHealPowerRatio, MidpointRounding.AwayFromZero));
        }

        private static float GetHealthPercent(Character character)
        {
            var currentHealth = Convert.ToInt64(ModHelpers.GetFieldValue(character, "m_currentHealth") ?? 0L);
            var maxHealth = Math.Max(1L, Convert.ToInt64(ModHelpers.GetFieldValue(character, "maxHealth") ?? 1L));
            return currentHealth / (float)maxHealth;
        }

        private static void ApplyPulseShield(Character caster, Character target, Artifact artifact, long amount)
        {
            var shieldAmount = amount > int.MaxValue ? int.MaxValue : (int)amount;
            var existingEffect = target.getEffectByKey(ResonanceShieldKey);
            if (existingEffect != null)
            {
                existingEffect.currentShieldValue = shieldAmount;
                existingEffect.currentDuration = PulseShieldDuration;
                existingEffect.maxDuration = PulseShieldDuration;
                existingEffect.leftOverDuration = PulseShieldDuration;
                target.setShielded(true, shieldAmount);
                return;
            }

            var shieldEffect = UtilsManager.UTILM?.getGenericShieldEffect(
                caster,
                target,
                "Resonance Scepter",
                ResonanceShieldKey,
                artifact?.Icon,
                PulseShieldDuration,
                shieldAmount,
                new List<float>(),
                false,
                false,
                true);

            if (shieldEffect == null)
            {
                target.setShielded(true, shieldAmount);
                return;
            }

            shieldEffect.isStackable = false;
            shieldEffect.maxStackSize = 1;
            shieldEffect.currentStack = 1;
            shieldEffect.currentShieldValue = shieldAmount;
            target.AddOngoingEffect(shieldEffect, false);
        }

        private sealed class GuardianDropContext
        {
            internal Level Level;
            internal LevelDifficultyData LevelDifficulty;
            internal Boss Boss;
            internal List<Artifact> Weapons;
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
