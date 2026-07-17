using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MiniHealerImprovementMod
{
    internal static class MeteorQuillMod
    {
        internal const string MeteorQuillKey = "CODEX_METEOR_QUILL";
        private const string GreaterAlchemyShardFallbackKey = "GR_ALCHEMY_SHARD";
        private const int MeteorQuillCraftCost = 1;
        private const float MeteorQuillFallbackDropWeight = 0.1f;
        private const int EmpoweredShotCount = 5;
        private const float EmpoweredShotBonus = 0.5f;

        private static readonly CustomBaseAttributeSpec[] MeteorQuillBaseAttributeSpecs =
        {
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_RANGER_DAMAGE_FLAT, 400f, 450f, "Ranger Damage"),
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_RANGER_CRIT_CHANCE, 7f, 9f, "Ranger Crit Chance"),
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_RAPID_SHOT_DAMAGE, 2000f, 2250f, "Rapid Shot Damage"),
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT, 3000f, 3500f, "Party Health")
        };

        private static readonly ArtifactAttribute.AttriubteType[] MeteorQuillBaseAttributeTypes =
            MeteorQuillBaseAttributeSpecs.Select(spec => spec.AttributeType).ToArray();

        private sealed class BattleAttackState
        {
            internal int AttackCount;
            internal bool Empowered;
            internal int RemainingElementCallbacks;
        }

        private static readonly Dictionary<BattleManager, BattleAttackState> BattleAttackStates =
            new Dictionary<BattleManager, BattleAttackState>();

        internal static bool TryInjectMeteorQuill()
        {
            return EnsureMeteorQuill() != null;
        }

        internal static bool TryInjectMeteorQuillLootSource()
        {
            var artifact = EnsureMeteorQuill();
            var context = GetGuardianContext();
            if (artifact == null || context?.LevelDifficulty == null || context.Boss == null)
            {
                return false;
            }

            context.LevelDifficulty.Loot = ModHelpers.AppendKeyCopy(context.LevelDifficulty.Loot, MeteorQuillKey);
            context.Boss.depthLoot = ModHelpers.AppendKeyCopy(context.Boss.depthLoot, MeteorQuillKey);
            return true;
        }

        internal static Artifact EnsureMeteorQuill()
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
                MeteorQuillKey,
                templateData => FindMeteorQuillTemplate(templateData, context),
                artifact => ConfigureMeteorQuill(artifact, controller, context),
                WireMeteorQuill);
        }

        private static Artifact FindMeteorQuillTemplate(ArtifactsData data, GuardianDropContext context)
        {
            return context?.Artifacts?.FirstOrDefault(item => item != null && item.Type == Artifact.ArtifactType.ARROW)
                ?? context?.Artifacts?.FirstOrDefault(item => item != null && item.SlotType == Artifact.ArtifactSlotType.WEAPON)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != MeteorQuillKey && item.Type == Artifact.ArtifactType.ARROW)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != MeteorQuillKey && item.SlotType == Artifact.ArtifactSlotType.WEAPON)
                ?? data.ArtifactList?.FirstOrDefault(item => item != null && item.Key != MeteorQuillKey);
        }

        private static void ConfigureMeteorQuill(Artifact artifact, ArtifactDataController controller, GuardianDropContext context)
        {
            ModHelpers.ConfigureCustomArtifact(artifact, controller, new CustomArtifactSpec
            {
                Key = MeteorQuillKey,
                Name = "Meteor Quill",
                Rarity = Artifact.RarityType.Legendary,
                SlotType = Artifact.ArtifactSlotType.WEAPON,
                Type = Artifact.ArtifactType.ARROW,
                MutationPoolTypes = new List<Artifact.ArtifactCharacterType>
                {
                    Artifact.ArtifactCharacterType.RANGER_OFFENSIVE,
                    Artifact.ArtifactCharacterType.RANGER_DEFENSIVE
                },
                HiddenItemLevel = 85,
                DropRate = GetGuardianDropWeight(context),
                DroppedBossName = !string.IsNullOrEmpty(context?.Boss?.Key) ? context.Boss.Key : "Guardian",
                DroppedLevelName = !string.IsNullOrEmpty(context?.Level?.Key) ? context.Level.Key : "Guardian",
                PurchaseMaterialFallbackKey = GreaterAlchemyShardFallbackKey,
                PurchasePrice = MeteorQuillCraftCost,
                FallbackIcon = CustomArtifactIcons.Load("MeteorQuill_32.png") ?? controller.LifemenderIcon ?? controller.FaithkeeperIcon,
                BaseAttributeTypes = MeteorQuillBaseAttributeTypes,
                SearchText = "Meteor Quill arrow ranger rapid shot crit endgame legendary"
            });
        }

        internal static void EnsureMeteorQuillSaveAttributes(ArtifactSaveInfo saveInfo)
        {
            ModHelpers.TryEnsureSaveAttributes(saveInfo, MeteorQuillKey, MeteorQuillBaseAttributeTypes);
        }

        internal static void EnsureMeteorQuillSaveAttributes(Artifact artifact, List<ArtifactSaveAttribute> attributes)
        {
            ModHelpers.TryEnsureSaveAttributes(artifact, MeteorQuillKey, attributes, MeteorQuillBaseAttributeTypes);
        }

        internal static void EnsureMeteorQuillBaseAttributes(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> attributes)
        {
            ModHelpers.TryApplyFixedBaseAttributes(artifact, MeteorQuillKey, saveInfo, ref attributes, MeteorQuillBaseAttributeSpecs, MeteorQuillBaseAttributeTypes);
        }

        internal static void AppendMeteorQuillDescription(Artifact artifact, ref List<string> descriptions)
        {
            if (artifact?.Key != MeteorQuillKey)
            {
                return;
            }

            ModHelpers.AppendUniqueDescription(ref descriptions, ModHelpers.ColorizeTerms(
                "Every 5th successful living ranger auto-attack is empowered, dealing 50% bonus base damage. The counter is shared across the ranger party and resets each battle.",
                new TooltipTerm("5th", ModHelpers.LightningColor),
                new TooltipTerm("empowered", ModHelpers.LightningColor),
                new TooltipTerm("50% bonus base damage", ModHelpers.PhysicalColor)));
        }

        internal static bool TryGetMeteorQuillPurchaseMaterial(Artifact artifact, ref StackableMaterial result)
        {
            return ModHelpers.TryGetCustomPurchaseMaterial(artifact, MeteorQuillKey, GreaterAlchemyShardFallbackKey, ref result);
        }

        internal static void RefreshMeteorQuillAtlasInfo(ItemAtlasUIManager manager)
        {
            ModHelpers.RefreshAtlasSubtitle(manager, MeteorQuillKey, "(Arrow, Unique)");
        }

        internal static void AddMeteorQuillToDropTable(string bossKey, LootTableManager.ArtifactLootDropTable table)
        {
            var artifact = EnsureMeteorQuill();
            var context = GetGuardianContext(ArtifactDataController.ADM?.artifactData);
            if (artifact == null || table?.lootDropItems == null || context?.Boss == null || !string.Equals(context.Boss.Key, bossKey, StringComparison.Ordinal))
            {
                return;
            }

            if (table.lootDropItems.Any(item => item?.item != null && item.item.Key == MeteorQuillKey))
            {
                return;
            }

            table.lootDropItems.Add(new LootTableManager.ArtifactLootDropItem
            {
                item = artifact,
                probabilityWeight = GetGuardianDropWeight(context)
            });
        }

        private static void WireMeteorQuill(Artifact artifact)
        {
            artifact.OnGetBaseAttackDamage -= MeteorQuill_GetBaseAttackDamage;
            artifact.OnGetBaseAttackDamage += MeteorQuill_GetBaseAttackDamage;
        }

        private static int MeteorQuill_GetBaseAttackDamage(Artifact artifact, Character attacker, int damage, DamageData.DamageElement element, BattleManager battleManager, ArtifactSaveInfo saveInfo)
        {
            if (artifact == null || attacker == null || attacker.isEnemy || attacker.isDead || attacker.characterType != OtherGameDataController.CharacterType.Ranger)
            {
                return damage;
            }

            if (battleManager == null || !BattleAttackStates.TryGetValue(battleManager, out var state))
            {
                return damage;
            }

            var empowered = state.Empowered;
            state.RemainingElementCallbacks--;
            if (state.RemainingElementCallbacks <= 0)
            {
                state.Empowered = false;
            }

            if (!empowered)
            {
                return damage;
            }

            var bonus = Math.Max(0L, (long)Math.Round(Math.Max(0, damage) * EmpoweredShotBonus, MidpointRounding.AwayFromZero));
            return (int)Math.Min(int.MaxValue, Math.Max(0L, damage) + bonus);
        }

        internal static void BeginAutoAttack(Character attacker, List<DamageData> damageData)
        {
            if (attacker == null || attacker.isEnemy || attacker.isDead
                || attacker.characterType != OtherGameDataController.CharacterType.Ranger
                || damageData == null || damageData.Count == 0)
            {
                return;
            }

            var battleManager = ModHelpers.GetFieldValue(attacker, "BattleManager") as BattleManager;
            if (battleManager == null)
            {
                return;
            }

            if (!BattleAttackStates.TryGetValue(battleManager, out var state))
            {
                state = new BattleAttackState();
                BattleAttackStates[battleManager] = state;
            }

            state.AttackCount = (state.AttackCount % EmpoweredShotCount) + 1;
            state.Empowered = state.AttackCount == EmpoweredShotCount;
            state.RemainingElementCallbacks = Enum.GetValues(typeof(DamageData.DamageElement)).Length;
        }

        internal static void ResetBattleState(BattleManager battleManager)
        {
            if (battleManager != null)
            {
                BattleAttackStates.Remove(battleManager);
            }
        }

        private static GuardianDropContext GetGuardianContext(ArtifactsData data = null)
        {
            return ModHelpers.GetGuardianContext(
                data ?? ArtifactDataController.ADM?.artifactData,
                Artifact.ArtifactSlotType.WEAPON,
                new[] { MeteorQuillKey },
                "Meteor Quill");
        }

        private static float GetGuardianDropWeight(GuardianDropContext context)
        {
            return ModHelpers.GetAverageDropWeight(context, new[] { MeteorQuillKey }, MeteorQuillFallbackDropWeight);
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.autoAttackDamage))]
    internal static class Character_AutoAttackDamage_MeteorQuill_Patch
    {
        private static void Prefix(Character __instance, List<DamageData> damageDatas)
        {
            MeteorQuillMod.BeginAutoAttack(__instance, damageDatas);
        }
    }

    [HarmonyPatch(typeof(BattleManager), "Awake")]
    internal static class BattleManager_Awake_MeteorQuill_Patch
    {
        private static void Prefix(BattleManager __instance)
        {
            MeteorQuillMod.ResetBattleState(__instance);
        }
    }

    [HarmonyPatch(typeof(BattleManager), nameof(BattleManager.startEngageAnimation))]
    internal static class BattleManager_StartEngageAnimation_MeteorQuill_Patch
    {
        private static void Prefix(BattleManager __instance)
        {
            MeteorQuillMod.ResetBattleState(__instance);
        }
    }

    [HarmonyPatch(typeof(BattleManager), nameof(BattleManager.allBossesDefeated))]
    internal static class BattleManager_AllBossesDefeated_MeteorQuill_Patch
    {
        private static void Prefix(BattleManager __instance)
        {
            MeteorQuillMod.ResetBattleState(__instance);
        }
    }

    [HarmonyPatch(typeof(BattleManager), nameof(BattleManager.allPlayerDied))]
    internal static class BattleManager_AllPlayerDied_MeteorQuill_Patch
    {
        private static void Prefix(BattleManager __instance)
        {
            MeteorQuillMod.ResetBattleState(__instance);
        }
    }

    [HarmonyPatch(typeof(BattleManager), nameof(BattleManager.stopAllActions))]
    internal static class BattleManager_StopAllActions_MeteorQuill_Patch
    {
        private static void Prefix(BattleManager __instance)
        {
            MeteorQuillMod.ResetBattleState(__instance);
        }
    }
}
