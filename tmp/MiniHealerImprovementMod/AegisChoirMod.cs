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
        private const string GreaterAlchemyShardFallbackKey = "GR_ALCHEMY_SHARD";
        private const string AegisChoirEffectText = "Healer attacks grant a random living party member a stackable shield equal to 100 plus current heal power, capped at 80% of that member's maximum health, for 8 seconds. New applications add a stack and refresh the shield duration.";
        private const float AegisChoirShieldDuration = 8f;
        private const float AegisChoirShieldMaxHealthPercent = 0.8f;
        private const int AegisChoirMaxShieldStacks = 999;
        private static readonly ArtifactAttribute.AttriubteType[] AegisChoirBaseAttributeTypes =
        {
            ArtifactAttribute.AttriubteType.INCREASE_HEALER_PHYSICAL_DAMAGE_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_HEALER_ATT_SPD_PERCENT
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

            if (data.artifactsMap != null && data.artifactsMap.TryGetValue(AegisChoirKey, out var existingArtifact) && existingArtifact != null)
            {
                ConfigureAegisChoir(existingArtifact, controller, data);
                WireAegisChoir(existingArtifact);
                return existingArtifact;
            }

            var artifact = CreateAegisChoirArtifact(controller, data);
            ModHelpers.ReplaceArtifactCollections(data, artifact);
            return artifact;
        }

        private static Artifact CreateAegisChoirArtifact(ArtifactDataController controller, ArtifactsData data)
        {
            var artifact = new Artifact();
            var template = FindAegisChoirTemplate(data);
            if (template != null)
            {
                ModHelpers.CopyArtifactTemplate(template, artifact);
            }

            ConfigureAegisChoir(artifact, controller, data);
            WireAegisChoir(artifact);
            return artifact;
        }

        private static Artifact FindAegisChoirTemplate(ArtifactsData data)
        {
            return data.Artifacts?.FirstOrDefault(item => item != null && item.Key != AegisChoirKey && item.SlotType == Artifact.ArtifactSlotType.WEAPON && item.Type == Artifact.ArtifactType.STAFF)
                ?? data.ArtifactList?.FirstOrDefault(item => item != null && item.Key != AegisChoirKey && item.SlotType == Artifact.ArtifactSlotType.WEAPON)
                ?? data.Artifacts?.FirstOrDefault(item => item != null && item.Key != AegisChoirKey);
        }

        private static void ConfigureAegisChoir(Artifact artifact, ArtifactDataController controller, ArtifactsData data)
        {
            artifact.ArtifactName = "Aegis Choir";
            artifact.Key = AegisChoirKey;
            artifact.Rarity = Artifact.RarityType.Legendary;
            artifact.SlotType = Artifact.ArtifactSlotType.WEAPON;
            artifact.Type = Artifact.ArtifactType.STAFF;
            artifact.MutationPoolType = new List<Artifact.ArtifactCharacterType>
            {
                Artifact.ArtifactCharacterType.HEALER_OFFENSIVE,
                Artifact.ArtifactCharacterType.HEALER_DEFENSIVE
            };
            artifact.HiddenItemLevel = 85;
            artifact.DropRate = 1f;
            artifact.weight = 1f;
            artifact.isEquippable = true;
            artifact.isMutateable = true;
            artifact.isAugmentable = true;
            artifact.isDiscoverable = true;
            artifact.isDepth = false;
            artifact.isDivine = false;
            artifact.linkedDivineArtifactKey = string.Empty;
            artifact.linkedNormalArtifactKey = string.Empty;
            artifact.droppedBossName = "LOM";
            artifact.droppedLevelName = "BOSS_LOM_NAME";
            artifact.PurchaseMat = ResolveGreaterAlchemyShardKey() ?? GreaterAlchemyShardFallbackKey;
            artifact.PurchasePrice = AegisChoirCraftCost;
            artifact.specialDesc = string.Empty;
            artifact.Icon = artifact.Icon ?? controller.LifemenderIcon ?? controller.FaithkeeperIcon ?? controller.DEFAULT_ITEM_ICON;

            artifact.possibleMutationAttributes = artifact.possibleMutationAttributes ?? new List<ArtifactAttribute.AttriubteType>();
            artifact.possibleRolledAttributes = artifact.possibleRolledAttributes ?? new List<ArtifactAttribute.AttriubteType>();
            artifact.tempRolledSavedAttributes = artifact.tempRolledSavedAttributes ?? new List<ArtifactSaveAttribute>();
            artifact.tempRolledSockets = artifact.tempRolledSockets ?? new List<Socket>();
            artifact.artifactVFXSpawnType = artifact.artifactVFXSpawnType ?? new List<Artifact.ArtifactVFXSpawnType>();
            EnsureAegisChoirAttributePools(artifact);
            EnsureAegisChoirSaveAttributes(artifact.tempRolledSavedAttributes);

            if (controller.baseArtifactSearchStringMap != null)
            {
                controller.baseArtifactSearchStringMap[AegisChoirKey] = "Aegis Choir staff healer shield weapon legendary";
            }
        }

        private static void EnsureAegisChoirAttributePools(Artifact artifact)
        {
            foreach (var attributeType in AegisChoirBaseAttributeTypes)
            {
                if (!artifact.possibleRolledAttributes.Contains(attributeType))
                {
                    artifact.possibleRolledAttributes.Add(attributeType);
                }

                if (!artifact.possibleMutationAttributes.Contains(attributeType))
                {
                    artifact.possibleMutationAttributes.Add(attributeType);
                }
            }
        }

        internal static void EnsureAegisChoirSaveAttributes(ArtifactSaveInfo saveInfo)
        {
            if (saveInfo?.ArtifactKey != AegisChoirKey)
            {
                return;
            }

            saveInfo.SaveAttributes = saveInfo.SaveAttributes ?? new List<ArtifactSaveAttribute>();
            saveInfo.AttributeUpgrade = saveInfo.AttributeUpgrade ?? new List<ArtifactAttrUpgradeSaveInfo>();
            EnsureAegisChoirSaveAttributes(saveInfo.SaveAttributes);
        }

        internal static void EnsureAegisChoirSaveAttributes(Artifact artifact, List<ArtifactSaveAttribute> attributes)
        {
            if (artifact?.Key != AegisChoirKey)
            {
                return;
            }

            EnsureAegisChoirSaveAttributes(attributes);
        }

        private static void EnsureAegisChoirSaveAttributes(List<ArtifactSaveAttribute> attributes)
        {
            if (attributes == null)
            {
                return;
            }

            attributes.RemoveAll(attribute =>
                attribute != null
                && AegisChoirBaseAttributeTypes.Contains(attribute.attributeType)
                && (attribute.addedType == ArtifactAttribute.AddedType.ROLL_BASE || attribute.addedType == ArtifactAttribute.AddedType.ROLL));
        }

        internal static void EnsureAegisChoirBaseAttributes(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> attributes)
        {
            if (artifact?.Key != AegisChoirKey || AttributesManager.ATRM == null)
            {
                return;
            }

            attributes = new List<ArtifactAttribute>();
            EnsureAegisChoirSaveAttributes(saveInfo);
            ModHelpers.AddOrReplaceBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_HEALER_PHYSICAL_DAMAGE_FLAT, 5000f, 6000f);
            ModHelpers.AddOrReplaceBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT, 5000f, 6000f);
            ModHelpers.AddOrReplaceBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_HEALER_ATT_SPD_PERCENT, 20f, 25f);
        }

        internal static void AppendAegisChoirDescription(Artifact artifact, ref List<string> descriptions)
        {
            if (artifact?.Key != AegisChoirKey)
            {
                return;
            }

            descriptions = descriptions ?? new List<string>();
            if (!descriptions.Contains(AegisChoirEffectText))
            {
                descriptions.Add(AegisChoirEffectText);
            }
        }

        internal static bool TryGetAegisChoirPurchaseMaterial(Artifact artifact, ref StackableMaterial result)
        {
            if (artifact?.Key != AegisChoirKey)
            {
                return true;
            }

            result = MaterialDataController.MATDM?.getMaterialByKey(artifact.PurchaseMat)
                ?? MaterialDataController.MATDM?.getMaterialByKey(GreaterAlchemyShardFallbackKey);
            return result == null;
        }

        internal static void RefreshAegisChoirAtlasInfo(ItemAtlasUIManager manager)
        {
            var selectedArtifact = ModHelpers.GetFieldValue(manager, "selectedArtifact") as Artifact;
            if (selectedArtifact?.Key != AegisChoirKey)
            {
                return;
            }

            SetTextField(manager, "UniqueText", ArtifactDataController.ADM?.getArtifactTypeDescByArtifact(selectedArtifact, null) ?? "(Staff, Unique)");
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
                probabilityWeight = Mathf.Max(1f, artifact.weight)
            });
        }

        private static string ResolveGreaterAlchemyShardKey()
        {
            var materials = MaterialDataController.MATDM?.materialData?.Materials;
            if (materials == null)
            {
                return null;
            }

            var candidates = materials
                .Where(material => material != null)
                .Select(material => new
                {
                    Material = material,
                    Search = $"{material.Key} {material.Name} {material.Description}".ToLowerInvariant()
                })
                .Where(item => item.Search.Contains("alch") && item.Search.Contains("shard"))
                .ToList();

            return candidates
                .OrderByDescending(item => item.Search.Contains("greater"))
                .ThenByDescending(item => item.Search.Contains("large") || item.Search.Contains("glorious"))
                .Select(item => item.Material.Key)
                .FirstOrDefault();
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
            RefreshAegisChoirShieldDuration(shieldTarget);
            var shieldEffect = UtilsManager.UTILM?.getGenericShieldEffect(
                attacker,
                shieldTarget,
                "Aegis Choir",
                AegisChoirShieldKey,
                icon,
                AegisChoirShieldDuration,
                shieldAmount,
                new List<float>(),
                true,
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
            var maxHealthValue = ModHelpers.GetFieldValue(shieldTarget, "maxHealth");
            var maxHealth = maxHealthValue != null ? Convert.ToInt64(maxHealthValue) : 0L;
            var healPower = OtherGameDataController.getCurrentHealPower(battleManager);
            var uncappedShieldAmount = Math.Max(1L, 100L + healPower);
            var maxShieldAmount = Math.Max(1L, (long)Math.Round(maxHealth * AegisChoirShieldMaxHealthPercent, MidpointRounding.AwayFromZero));
            var shieldAmount = Math.Min(uncappedShieldAmount, maxShieldAmount);
            return shieldAmount > int.MaxValue ? int.MaxValue : (int)shieldAmount;
        }

        private static void RefreshAegisChoirShieldDuration(Character shieldTarget)
        {
            var existingEffect = shieldTarget?.getEffectByKey(AegisChoirShieldKey);
            if (existingEffect == null)
            {
                return;
            }

            existingEffect.maxDuration = AegisChoirShieldDuration;
            existingEffect.currentDuration = AegisChoirShieldDuration;
            existingEffect.leftOverDuration = AegisChoirShieldDuration;
            existingEffect.AppliedTime = DateTime.Now;
        }

        private static void ConfigureAegisChoirShieldEffect(EffectData shieldEffect, int shieldAmount)
        {
            shieldEffect.isStackable = true;
            shieldEffect.maxStackSize = AegisChoirMaxShieldStacks;
            shieldEffect.currentStack = Math.Max(1, shieldEffect.currentStack);
            shieldEffect.currentDuration = AegisChoirShieldDuration;
            shieldEffect.maxDuration = AegisChoirShieldDuration;
            shieldEffect.leftOverDuration = AegisChoirShieldDuration;
            shieldEffect.currentShieldValue = shieldAmount;
        }

        private static void SetTextField(object owner, string fieldName, string text)
        {
            ModHelpers.SetTextField(owner, fieldName, text);
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.getBossSpecificDropTable))]
    internal static class LootTableManager_GetBossSpecificDropTable_Patch
    {
        private static void Postfix(ref LootTableManager.ArtifactLootDropTable __result)
        {
            AegisChoirMod.AddAegisChoirToDropTable(__result);
        }
    }

    [HarmonyPatch(typeof(ArtifactDataController), nameof(ArtifactDataController.isArtifactUnlocked))]
    internal static class ArtifactDataController_IsArtifactUnlocked_Patch
    {
        private static bool Prefix(Artifact artifact, ref bool __result)
        {
            if (artifact?.Key != AegisChoirMod.AegisChoirKey)
            {
                return true;
            }

            __result = true;
            artifact.isLockedInAtlas = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemAtlasUIManager), nameof(ItemAtlasUIManager.refreshItemAtlas))]
    internal static class ItemAtlasUIManager_RefreshItemAtlas_Diagnostics_Patch
    {
        private static Exception Finalizer(ItemAtlasUIManager __instance, Exception __exception)
        {
            ModHelpers.LogAtlasRefreshFailure(__instance, __exception);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ItemAtlasUIManager), nameof(ItemAtlasUIManager.refreshArtifactInfoView))]
    internal static class ItemAtlasUIManager_RefreshArtifactInfoView_Patch
    {
        private static void Postfix(ItemAtlasUIManager __instance)
        {
            AegisChoirMod.RefreshAegisChoirAtlasInfo(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemAtlasUIManager), "getArtifactPurchaseMat")]
    internal static class ItemAtlasUIManager_GetArtifactPurchaseMat_Patch
    {
        private static bool Prefix(Artifact artifact, ref StackableMaterial __result)
        {
            return AegisChoirMod.TryGetAegisChoirPurchaseMaterial(artifact, ref __result);
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.rollAttributesByLevel))]
    internal static class LootTableManager_RollAttributesByLevel_Patch
    {
        private static void Postfix(Artifact artifact, ref List<ArtifactSaveAttribute> __result)
        {
            AegisChoirMod.EnsureAegisChoirSaveAttributes(artifact, __result);
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.rollAttributesModular))]
    internal static class LootTableManager_RollAttributesModular_Patch
    {
        private static void Postfix(Artifact artifact, ref List<ArtifactSaveAttribute> __result)
        {
            AegisChoirMod.EnsureAegisChoirSaveAttributes(artifact, __result);
        }
    }

    [HarmonyPatch(typeof(ArtifactSaveInfo), nameof(ArtifactSaveInfo.upgradeRandomBaseAttribute))]
    internal static class ArtifactSaveInfo_UpgradeRandomBaseAttribute_Patch
    {
        private static void Prefix(ArtifactSaveInfo __instance)
        {
            AegisChoirMod.EnsureAegisChoirSaveAttributes(__instance);
        }
    }

    [HarmonyPatch(typeof(AttributesManager), nameof(AttributesManager.getArtifactBaseAttributes))]
    internal static class AttributesManager_GetArtifactBaseAttributes_Patch
    {
        private static void Postfix(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> __result)
        {
            AegisChoirMod.EnsureAegisChoirBaseAttributes(artifact, saveInfo, ref __result);
        }
    }

    [HarmonyPatch(typeof(OtherGameDataController), "getDescriptionByArtifact")]
    internal static class OtherGameDataController_GetDescriptionByArtifact_Patch
    {
        private static void Postfix(Artifact artifact, ref List<string> __result)
        {
            AegisChoirMod.AppendAegisChoirDescription(artifact, ref __result);
        }
    }

    [HarmonyPatch(typeof(LevelDescriptionModalController), nameof(LevelDescriptionModalController.updateLootView))]
    internal static class LevelDescriptionModalController_UpdateLootView_Patch
    {
        private static void Prefix()
        {
            AegisChoirMod.EnsureAegisChoir();
            AegisChoirMod.TryInjectAegisChoirLootSource();
        }
    }
}
