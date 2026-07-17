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
            return ModHelpers.EnsureCustomArtifact(
                data,
                StormheartCarapaceKey,
                templateData => FindStormheartTemplate(templateData, context),
                artifact => ConfigureStormheartCarapace(artifact, controller, context),
                WireStormheartCarapace);
        }

        private static Artifact FindStormheartTemplate(ArtifactsData data, GuardianDropContext context)
        {
            return context?.Artifacts?.FirstOrDefault(item => item != null && item.Type == Artifact.ArtifactType.BODYARMOR)
                ?? context?.Artifacts?.FirstOrDefault(item => item != null && item.SlotType == Artifact.ArtifactSlotType.ARMOR)
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
                FallbackIcon = CustomArtifactIcons.Load("StormheartCarapace_32.png") ?? controller?.DEFAULT_ITEM_ICON,
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
            var desiredName = attributeType.ToString();
            var range = ModHelpers.GetGuardianAttributeRange(
                GetGuardianContext(ArtifactDataController.ADM?.artifactData),
                attributeType,
                fallbackMin,
                fallbackMax,
                name => desiredName.Contains("HP")
                    ? name.Contains("HP") && name.EndsWith("_FLAT", StringComparison.Ordinal)
                    : desiredName.Contains("RESIST")
                        ? name.Contains("RESIST")
                        : name.Contains("DAMAGE_TAKEN"));
            ModHelpers.AddOrReplaceBaseAttribute(attributes, attributeType, range.Min, range.Max);
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
            return ModHelpers.GetAverageDropWeight(context, new[] { StormheartCarapaceKey }, StormheartFallbackDropWeight);
        }

        private static GuardianDropContext GetGuardianContext(ArtifactsData data)
        {
            return ModHelpers.GetGuardianContext(
                data,
                Artifact.ArtifactSlotType.ARMOR,
                new[] { StormheartCarapaceKey },
                "Stormheart Carapace");
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

    }

}
