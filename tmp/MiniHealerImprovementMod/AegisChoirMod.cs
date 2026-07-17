using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MiniHealerImprovementMod
{
    internal static class AegisChoirMod
    {
        internal const string AegisChoirKey = "CODEX_AEGIS_CHOIR";
        private const string AegisChoirShieldKey = "CODEX_AEGIS_CHOIR_SHIELD";
        private const int AegisChoirCraftCost = 1;
        private const float AegisChoirGuardianDropWeight = 0.1f;
        private const string GreaterAlchemyShardFallbackKey = "GR_ALCHEMY_SHARD";
        private const float AegisChoirShieldDuration = 8f;
        private static readonly ArtifactAttribute.AttriubteType[] AegisChoirBaseAttributeTypes =
        {
            ArtifactAttribute.AttriubteType.INCREASE_HEALER_PHYSICAL_DAMAGE_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_HEALER_ATT_SPD_PERCENT
        };

        private static readonly CustomBaseAttributeSpec[] AegisChoirBaseAttributeSpecs =
        {
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_HEALER_PHYSICAL_DAMAGE_FLAT, 5000f, 6000f, "Healer Physical Damage"),
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT, 5000f, 6000f, "Party Health"),
            new CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType.INCREASE_HEALER_ATT_SPD_PERCENT, 20f, 25f, "Healer Attack Speed")
        };

        internal static bool TryInjectAegisChoir()
        {
            return EnsureAegisChoir() != null;
        }

        internal static bool TryInjectAegisChoirLootSource()
        {
            return EnsureAegisChoirLootSources();
        }

        internal static Artifact EnsureAegisChoir()
        {
            var controller = ArtifactDataController.ADM ?? Resources.FindObjectsOfTypeAll<ArtifactDataController>().FirstOrDefault();
            var data = controller?.artifactData;
            if (data == null)
            {
                return null;
            }

            return ModHelpers.EnsureCustomArtifact(
                data,
                AegisChoirKey,
                FindAegisChoirTemplate,
                artifact => ConfigureAegisChoir(artifact, controller, data),
                WireAegisChoir);
        }

        private static Artifact FindAegisChoirTemplate(ArtifactsData data)
        {
            return data.Artifacts?.FirstOrDefault(item => item != null && item.Key != AegisChoirKey && item.SlotType == Artifact.ArtifactSlotType.WEAPON && item.Type == Artifact.ArtifactType.STAFF)
                ?? data.ArtifactList?.FirstOrDefault(item => item != null && item.Key != AegisChoirKey && item.SlotType == Artifact.ArtifactSlotType.WEAPON)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != AegisChoirKey);
        }

        private static void ConfigureAegisChoir(Artifact artifact, ArtifactDataController controller, ArtifactsData data)
        {
            ModHelpers.ConfigureCustomArtifact(artifact, controller, new CustomArtifactSpec
            {
                Key = AegisChoirKey,
                Name = "Aegis Choir",
                Rarity = Artifact.RarityType.Legendary,
                SlotType = Artifact.ArtifactSlotType.WEAPON,
                Type = Artifact.ArtifactType.STAFF,
                MutationPoolTypes = new List<Artifact.ArtifactCharacterType>
                {
                    Artifact.ArtifactCharacterType.HEALER_OFFENSIVE,
                    Artifact.ArtifactCharacterType.HEALER_DEFENSIVE
                },
                HiddenItemLevel = 85,
                DropRate = AegisChoirGuardianDropWeight,
                DroppedBossName = "LOM",
                DroppedLevelName = "BOSS_LOM_NAME",
                PurchaseMaterialFallbackKey = GreaterAlchemyShardFallbackKey,
                PurchasePrice = AegisChoirCraftCost,
                FallbackIcon = CustomArtifactIcons.Load("AegisChoir_32.png") ?? controller.LifemenderIcon ?? controller.FaithkeeperIcon,
                BaseAttributeTypes = AegisChoirBaseAttributeTypes,
                SearchText = "Aegis Choir staff healer shield weapon legendary"
            });
        }

        internal static void EnsureAegisChoirSaveAttributes(ArtifactSaveInfo saveInfo)
        {
            ModHelpers.TryEnsureSaveAttributes(saveInfo, AegisChoirKey, AegisChoirBaseAttributeTypes);
        }

        internal static void EnsureAegisChoirSaveAttributes(Artifact artifact, List<ArtifactSaveAttribute> attributes)
        {
            ModHelpers.TryEnsureSaveAttributes(artifact, AegisChoirKey, attributes, AegisChoirBaseAttributeTypes);
        }

        internal static void EnsureAegisChoirBaseAttributes(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> attributes)
        {
            ModHelpers.TryApplyFixedBaseAttributes(artifact, AegisChoirKey, saveInfo, ref attributes, AegisChoirBaseAttributeSpecs, AegisChoirBaseAttributeTypes);
        }

        internal static void AppendAegisChoirDescription(Artifact artifact, ref List<string> descriptions)
        {
            if (artifact?.Key != AegisChoirKey)
            {
                return;
            }

            ModHelpers.AppendUniqueDescription(ref descriptions, GetAegisChoirEffectText());
        }

        private static string GetAegisChoirEffectText()
        {
            return ModHelpers.ColorizeTerms(
                "Healer attacks grant a random living party member a non-stackable shield equal to 100 plus current heal power for 8 seconds. Reapplying the shield refreshes its value, but not its duration.",
                new TooltipTerm("shield", ModHelpers.ShieldColor),
                new TooltipTerm("current heal power", ModHelpers.HealPowerColor));
        }

        internal static bool TryGetAegisChoirPurchaseMaterial(Artifact artifact, ref StackableMaterial result)
        {
            return ModHelpers.TryGetCustomPurchaseMaterial(artifact, AegisChoirKey, GreaterAlchemyShardFallbackKey, ref result);
        }

        internal static void RefreshAegisChoirAtlasInfo(ItemAtlasUIManager manager)
        {
            ModHelpers.RefreshAtlasSubtitle(manager, AegisChoirKey, "(Staff, Unique)");
        }

        private static bool EnsureAegisChoirLootSources()
        {
            var levelData = LevelDataController.LDM?.levelData;
            var bossData = BossDataController.BDM?.bossData;
            if (levelData?.Levels == null || bossData?.Bosses == null)
            {
                return false;
            }

            var touchedAny = false;
            foreach (var level in levelData.Levels.Where(level => level != null && level.isGuardian && level.Difficulties != null))
            {
                foreach (var difficulty in level.Difficulties.Where(difficulty => difficulty?.Bosses != null))
                {
                    difficulty.Loot = ModHelpers.AppendKeyCopy(difficulty.Loot, AegisChoirKey);

                    foreach (var bossKey in difficulty.Bosses)
                    {
                        var boss = bossData.Bosses.FirstOrDefault(candidate => candidate != null && candidate.Key == bossKey);
                        if (boss == null)
                        {
                            continue;
                        }

                        boss.depthLoot = ModHelpers.AppendKeyCopy(boss.depthLoot, AegisChoirKey);
                        touchedAny = true;
                    }
                }
            }

            return touchedAny;
        }

        internal static void AddAegisChoirToDropTable(LootTableManager.ArtifactLootDropTable table)
        {
            var artifact = EnsureAegisChoir();
            if (artifact == null || table?.lootDropItems == null || table.lootDropItems.Any(item => item?.item != null && item.item.Key == AegisChoirKey))
            {
                return;
            }

            table.lootDropItems.Add(new LootTableManager.ArtifactLootDropItem
            {
                item = artifact,
                probabilityWeight = GetAegisChoirDropWeight(table, artifact)
            });
        }

        private static float GetAegisChoirDropWeight(LootTableManager.ArtifactLootDropTable table, Artifact artifact)
        {
            var similarGuardianWeaponWeights = table?.lootDropItems?
                .Where(item => item?.item != null
                    && item.item.Key != AegisChoirKey
                    && item.item.SlotType == Artifact.ArtifactSlotType.WEAPON
                    && item.probabilityWeight > 0f)
                .Select(item => item.probabilityWeight)
                .ToList();

            if (similarGuardianWeaponWeights != null && similarGuardianWeaponWeights.Count > 0)
            {
                return Mathf.Max(0.01f, similarGuardianWeaponWeights.Average());
            }

            return Mathf.Max(0.01f, artifact?.weight ?? AegisChoirGuardianDropWeight);
        }

        private static void WireAegisChoir(Artifact artifact)
        {
            artifact.OnAddCurrentBonus -= AegisChoir_AddCurrentBonus;
            artifact.OnGetBaseAttackDamage -= AegisChoir_GetBaseAttackDamage;
            artifact.OnAttack -= AegisChoir_OnAttack;
            artifact.OnAttack += AegisChoir_OnAttack;
        }

        private static void AegisChoir_AddCurrentBonus(Artifact artifact, int quality, ArtifactSaveInfo saveInfo)
        {
        }

        private static int AegisChoir_GetBaseAttackDamage(Artifact artifact, Character attacker, int damage, DamageData.DamageElement element, BattleManager battleManager, ArtifactSaveInfo saveInfo)
        {
            return damage;
        }

        private static void AegisChoir_OnAttack(List<Character> characters, Artifact artifact, Character attacker, Character target, int damage, BattleManager battleManager, DamageData damageData)
        {
            if (attacker == null || attacker.characterType != OtherGameDataController.CharacterType.Healer || characters == null || characters.Count == 0)
            {
                return;
            }

            var candidates = characters.Where(character => character != null && !character.isEnemy && !character.isDead).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var shieldTarget = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var shieldAmount = GetAegisChoirShieldAmount(shieldTarget, battleManager);
            var icon = artifact?.Icon;
            if (RefreshAegisChoirShieldValue(shieldTarget, shieldAmount))
            {
                battleManager?.spawnArtifactVFX(artifact, shieldTarget);
                return;
            }

            var shieldEffect = UtilsManager.UTILM?.getGenericShieldEffect(
                attacker,
                shieldTarget,
                "Aegis Choir",
                AegisChoirShieldKey,
                icon,
                AegisChoirShieldDuration,
                shieldAmount,
                new List<float>(),
                false,
                false,
                true);

            if (shieldEffect == null)
            {
                shieldTarget.setShielded(true, shieldAmount);
                return;
            }

            battleManager?.spawnArtifactVFX(artifact, shieldTarget);
            ConfigureAegisChoirShieldEffect(shieldEffect, shieldAmount);
            shieldTarget.AddOngoingEffect(shieldEffect, false);
        }

        private static int GetAegisChoirShieldAmount(Character shieldTarget, BattleManager battleManager)
        {
            var healPower = OtherGameDataController.getCurrentHealPower(battleManager);
            var shieldAmount = Math.Max(1L, 100L + healPower);
            return shieldAmount > int.MaxValue ? int.MaxValue : (int)shieldAmount;
        }

        private static bool RefreshAegisChoirShieldValue(Character shieldTarget, int shieldAmount)
        {
            var existingEffect = shieldTarget?.getEffectByKey(AegisChoirShieldKey);
            if (existingEffect == null)
            {
                return false;
            }

            existingEffect.isStackable = false;
            existingEffect.maxStackSize = 1;
            existingEffect.currentStack = 1;
            existingEffect.currentShieldValue = shieldAmount;
            shieldTarget.setShielded(true, shieldAmount);
            return true;
        }

        private static void ConfigureAegisChoirShieldEffect(EffectData shieldEffect, int shieldAmount)
        {
            shieldEffect.isStackable = false;
            shieldEffect.maxStackSize = 1;
            shieldEffect.currentStack = 1;
            shieldEffect.currentDuration = AegisChoirShieldDuration;
            shieldEffect.maxDuration = AegisChoirShieldDuration;
            shieldEffect.leftOverDuration = AegisChoirShieldDuration;
            shieldEffect.currentShieldValue = shieldAmount;
        }

    }

}
