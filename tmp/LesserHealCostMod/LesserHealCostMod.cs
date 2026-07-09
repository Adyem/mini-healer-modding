using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniHealerTestPlugin
{
    [BepInPlugin("com.codex.minihealer.lesserhealcost", "Mini Healer Lesser Heal Cost Mod", "1.0.0")]
    public sealed class LesserHealCostMod : BaseUnityPlugin
    {
        private const float CostMultiplier = 0.85f;
        private const string TargetSkillName = "Lesser Heal";
        private const string TargetSkillKeyFragment = "lesserheal";
        internal const string AegisChoirKey = "CODEX_AEGIS_CHOIR";
        private const string AegisChoirShieldKey = "CODEX_AEGIS_CHOIR_SHIELD";
        private const int AegisChoirFlatAttack = 5000;
        private const int AegisChoirPartyHealth = 5600;
        private const float AegisChoirAttackSpeedMulti = 0.20f;
        private const float AegisChoirShieldDuration = 8f;
        private const int AegisChoirCraftCost = 1;
        private const string GreaterAlchemyShardFallbackKey = "GR_ALCHEMY_SHARD";
        private const string AegisChoirEffectText = "Healer attacks grant a random living party member a shield equal to 100 plus current heal power.";
        private static readonly ArtifactAttribute.AttriubteType[] AegisChoirBaseAttributeTypes =
        {
            ArtifactAttribute.AttriubteType.INCREASE_HEALER_PHYSICAL_DAMAGE_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT,
            ArtifactAttribute.AttriubteType.INCREASE_HEALER_ATT_SPD_PERCENT
        };
        internal static readonly Regex ManaCostRegex = new Regex(@"\b\d+\s*(?i:mana)\b", RegexOptions.Compiled);
        private readonly HashSet<object> _patchedSkills = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private bool _skillPatched;
        private bool _artifactInjected;
        private bool _lootInjected;

        private void Awake()
        {
            Logger.LogInfo("Mini Healer injected balance/item mod loaded");
            new Harmony("com.codex.minihealer.lesserhealcost").PatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(ApplyWhenReady());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _skillPatched = _skillPatched || TryPatchAllControllers();
                _artifactInjected = TryInjectAegisChoir();
                _lootInjected = TryInjectAegisChoirLootSource();

                if (_skillPatched && _artifactInjected && _lootInjected)
                {
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            Logger.LogWarning("Timed out waiting for all injected balance/item changes to attach.");
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

            ReplaceArtifactCollections(data, artifact);
            return artifact;
        }

        private static void ReplaceArtifactCollections(ArtifactsData data, Artifact artifact)
        {
            var artifactList = data.ArtifactList != null
                ? new List<Artifact>(data.ArtifactList.Where(item => item != null && item.Key != AegisChoirKey))
                : new List<Artifact>((data.Artifacts ?? new Artifact[0]).Where(item => item != null && item.Key != AegisChoirKey));
            artifactList.Add(artifact);
            data.ArtifactList = artifactList;

            var artifactArray = data.Artifacts != null
                ? data.Artifacts.Where(item => item != null && item.Key != AegisChoirKey).ToList()
                : artifactList.Where(item => item != null && item.Key != AegisChoirKey).ToList();
            artifactArray.Add(artifact);
            data.Artifacts = artifactArray.ToArray();

            var artifactMap = data.artifactsMap != null
                ? new Dictionary<string, Artifact>(data.artifactsMap)
                : new Dictionary<string, Artifact>();
            artifactMap[AegisChoirKey] = artifact;
            data.artifactsMap = artifactMap;
        }

        private static Artifact CreateAegisChoirArtifact(ArtifactDataController controller, ArtifactsData data)
        {
            var artifact = new Artifact();
            var template = FindAegisChoirTemplate(data);
            if (template != null)
            {
                CopyArtifactTemplate(template, artifact);
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

        private static void CopyArtifactTemplate(Artifact source, Artifact target)
        {
            foreach (var field in typeof(Artifact).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                field.SetValue(target, field.GetValue(source));
            }
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
            AddOrReplaceAegisChoirBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_HEALER_PHYSICAL_DAMAGE_FLAT, 5000f, 6000f);
            AddOrReplaceAegisChoirBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_ALL_HP_FLAT, 5000f, 6000f);
            AddOrReplaceAegisChoirBaseAttribute(attributes, ArtifactAttribute.AttriubteType.INCREASE_HEALER_ATT_SPD_PERCENT, 20f, 25f);
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

        private static void AddOrReplaceAegisChoirBaseAttribute(List<ArtifactAttribute> attributes, ArtifactAttribute.AttriubteType attributeType, float minValue, float maxValue)
        {
            attributes.RemoveAll(attribute => attribute != null && attribute.attributeType == attributeType && attribute.addedType == ArtifactAttribute.AddedType.BASE);
            var baseAttribute = AttributesManager.ATRM.getBaseAttributByType(attributeType);
            if (baseAttribute == null)
            {
                return;
            }

            baseAttribute.addedType = ArtifactAttribute.AddedType.BASE;
            baseAttribute.T1_MIN = minValue;
            baseAttribute.T1_MAX = maxValue;
            baseAttribute.T2_MIN = minValue;
            baseAttribute.T2_MAX = maxValue;
            baseAttribute.T3_MIN = minValue;
            baseAttribute.T3_MAX = maxValue;
            baseAttribute.quality = 100;
            baseAttribute.tier = 3;
            baseAttribute.isUpgradeAble = true;
            attributes.Add(baseAttribute);
        }

        private bool TryInjectAegisChoir()
        {
            return EnsureAegisChoir() != null;
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

        internal static bool EnsureAegisChoirLootSources()
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
                    difficulty.Loot = AppendKeyCopy(difficulty.Loot, AegisChoirKey);

                    foreach (var bossKey in difficulty.Bosses)
                    {
                        var boss = bossData.Bosses.FirstOrDefault(candidate => candidate != null && candidate.Key == bossKey);
                        if (boss == null)
                        {
                            continue;
                        }

                        boss.depthLoot = AppendKeyCopy(boss.depthLoot, AegisChoirKey);

                        touchedAny = true;
                    }
                }
            }

            return touchedAny;
        }

        private static List<string> AppendKeyCopy(List<string> source, string key)
        {
            var result = source != null ? new List<string>(source) : new List<string>();
            if (!result.Contains(key))
            {
                result.Add(key);
            }

            return result;
        }

        private bool TryInjectAegisChoirLootSource()
        {
            return EnsureAegisChoirLootSources();
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
            var healPower = OtherGameDataController.getCurrentHealPower(battleManager);
            var shieldAmount = Mathf.Max(1, 100 + healPower);
            var icon = artifact?.Icon;
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
            shieldTarget.AddOngoingEffect(shieldEffect, false);
        }

        private bool TryPatchAllControllers()
        {
            var controllers = Resources.FindObjectsOfTypeAll(typeof(SkillDataController));
            var patchedAny = false;

            foreach (var controller in controllers)
            {
                if (controller == null)
                {
                    continue;
                }

                patchedAny |= TryPatchController(controller);
            }

            return patchedAny;
        }

        private bool TryPatchController(object controller)
        {
            var skillData = GetFieldValue(controller, "skillData");
            if (skillData == null)
            {
                return false;
            }

            var skills = GetFieldValue(skillData, "Skills") as Array;
            if (skills == null)
            {
                return false;
            }

            var patchedAny = false;
            foreach (var skill in skills)
            {
                if (skill == null || _patchedSkills.Contains(skill))
                {
                    continue;
                }

                if (!IsTargetSkill(skill))
                {
                    continue;
                }

                var manaCostField = skill.GetType().GetField("manaCost", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (manaCostField == null || manaCostField.FieldType != typeof(int))
                {
                    Logger.LogWarning("Found Lesser Heal but could not access manaCost.");
                    continue;
                }

                var originalCost = (int)manaCostField.GetValue(skill);
                var reducedCost = Math.Max(1, (int)Math.Round(originalCost * CostMultiplier, MidpointRounding.AwayFromZero));
                if (reducedCost >= originalCost)
                {
                    reducedCost = Math.Max(1, originalCost - Math.Max(1, originalCost / 4));
                }

                _patchedSkills.Add(skill);
                patchedAny = true;
                Logger.LogInfo($"Patched {GetSkillLabel(skill)} total mana cost: {originalCost} -> {reducedCost}");
            }

            return patchedAny;
        }

        internal static bool IsTargetSkill(object skill)
        {
            var name = Convert.ToString(GetFieldValue(skill, "SkillName")) ?? string.Empty;
            var key = Convert.ToString(GetFieldValue(skill, "Key")) ?? string.Empty;

            return string.Equals(name, TargetSkillName, StringComparison.OrdinalIgnoreCase)
                || key.IndexOf(TargetSkillKeyFragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetSkillLabel(object skill)
        {
            var name = Convert.ToString(GetFieldValue(skill, "SkillName"));
            var key = Convert.ToString(GetFieldValue(skill, "Key"));

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key))
            {
                return $"{name} ({key})";
            }

            return name ?? key ?? skill.GetType().Name;
        }

        internal static object GetFieldValue(object instance, string fieldName)
        {
            if (instance == null)
            {
                return null;
            }

            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(instance);
        }

        internal static void SetFieldValue(object instance, string fieldName, object value)
        {
            if (instance == null)
            {
                return;
            }

            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                field?.SetValue(instance, value);
        }

        internal static void LogAtlasRefreshFailure(ItemAtlasUIManager manager, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            var slotValue = GetFieldValue(manager, "currentSlotTypeFilter");
            var subtypeValue = GetFieldValue(manager, "currentSubTypeFilters") as IEnumerable<Artifact.ArtifactType>;
            var slot = slotValue != null ? slotValue.ToString() : "<null>";
            var subtypes = subtypeValue != null
                ? string.Join(",", subtypeValue.Select(type => type.ToString()).ToArray())
                : "<null>";
            Debug.LogError($"[Mini Healer Lesser Heal Cost Mod] Item atlas refresh failed. slot={slot}, subtypes={subtypes}, error={exception}");
        }

        internal static void RefreshAegisChoirAtlasInfo(ItemAtlasUIManager manager)
        {
            var selectedArtifact = GetFieldValue(manager, "selectedArtifact") as Artifact;
            if (selectedArtifact?.Key != AegisChoirKey)
            {
                return;
            }

            SetTextField(manager, "UniqueText", ArtifactDataController.ADM?.getArtifactTypeDescByArtifact(selectedArtifact, null) ?? "(Staff, Unique)");
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

        private static void SetTextField(object owner, string fieldName, string text)
        {
            var textComponent = GetFieldValue(owner, fieldName);
            if (textComponent == null)
            {
                return;
            }

            var textProperty = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            textProperty?.SetValue(textComponent, text, null);
        }
    }

    [HarmonyPatch(typeof(Skill), nameof(Skill.getTotalManaCost))]
    internal static class Skill_GetTotalManaCost_Patch
    {
        private static void Postfix(Skill __instance, ref int __result)
        {
            if (!LesserHealCostMod.IsTargetSkill(__instance))
            {
                return;
            }

            __result = Math.Max(1, (int)Math.Round(__result * 0.85f, MidpointRounding.AwayFromZero));
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.getBossSpecificDropTable))]
    internal static class LootTableManager_GetBossSpecificDropTable_Patch
    {
        private static void Postfix(ref LootTableManager.ArtifactLootDropTable __result)
        {
            LesserHealCostMod.AddAegisChoirToDropTable(__result);
        }
    }

    [HarmonyPatch(typeof(ArtifactDataController), nameof(ArtifactDataController.isArtifactUnlocked))]
    internal static class ArtifactDataController_IsArtifactUnlocked_Patch
    {
        private static bool Prefix(Artifact artifact, ref bool __result)
        {
            if (artifact?.Key != LesserHealCostMod.AegisChoirKey)
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
            LesserHealCostMod.LogAtlasRefreshFailure(__instance, __exception);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ItemAtlasUIManager), nameof(ItemAtlasUIManager.refreshArtifactInfoView))]
    internal static class ItemAtlasUIManager_RefreshArtifactInfoView_Patch
    {
        private static void Postfix(ItemAtlasUIManager __instance)
        {
            LesserHealCostMod.RefreshAegisChoirAtlasInfo(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemAtlasUIManager), "getArtifactPurchaseMat")]
    internal static class ItemAtlasUIManager_GetArtifactPurchaseMat_Patch
    {
        private static bool Prefix(Artifact artifact, ref StackableMaterial __result)
        {
            return LesserHealCostMod.TryGetAegisChoirPurchaseMaterial(artifact, ref __result);
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.rollAttributesByLevel))]
    internal static class LootTableManager_RollAttributesByLevel_Patch
    {
        private static void Postfix(Artifact artifact, ref List<ArtifactSaveAttribute> __result)
        {
            LesserHealCostMod.EnsureAegisChoirSaveAttributes(artifact, __result);
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.rollAttributesModular))]
    internal static class LootTableManager_RollAttributesModular_Patch
    {
        private static void Postfix(Artifact artifact, ref List<ArtifactSaveAttribute> __result)
        {
            LesserHealCostMod.EnsureAegisChoirSaveAttributes(artifact, __result);
        }
    }

    [HarmonyPatch(typeof(ArtifactSaveInfo), nameof(ArtifactSaveInfo.upgradeRandomBaseAttribute))]
    internal static class ArtifactSaveInfo_UpgradeRandomBaseAttribute_Patch
    {
        private static void Prefix(ArtifactSaveInfo __instance)
        {
            LesserHealCostMod.EnsureAegisChoirSaveAttributes(__instance);
        }
    }

    [HarmonyPatch(typeof(AttributesManager), nameof(AttributesManager.getArtifactBaseAttributes))]
    internal static class AttributesManager_GetArtifactBaseAttributes_Patch
    {
        private static void Postfix(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> __result)
        {
            LesserHealCostMod.EnsureAegisChoirBaseAttributes(artifact, saveInfo, ref __result);
        }
    }

    [HarmonyPatch(typeof(OtherGameDataController), "getDescriptionByArtifact")]
    internal static class OtherGameDataController_GetDescriptionByArtifact_Patch
    {
        private static void Postfix(Artifact artifact, ref List<string> __result)
        {
            LesserHealCostMod.AppendAegisChoirDescription(artifact, ref __result);
        }
    }

    [HarmonyPatch(typeof(LevelDescriptionModalController), nameof(LevelDescriptionModalController.updateLootView))]
    internal static class LevelDescriptionModalController_UpdateLootView_Patch
    {
        private static void Prefix()
        {
            LesserHealCostMod.EnsureAegisChoir();
            LesserHealCostMod.EnsureAegisChoirLootSources();
        }
    }

    [HarmonyPatch(typeof(Skill), nameof(Skill.getDescription))]
    internal static class Skill_GetDescription_Patch
    {
        private static void Postfix(Skill __instance, ref string __result)
        {
            if (!LesserHealCostMod.IsTargetSkill(__instance) || string.IsNullOrEmpty(__result))
            {
                return;
            }

            var reducedCost = Math.Max(1, __instance.getTotalManaCost(false, null));
            var replacement = $"{reducedCost} mana";
            var updated = LesserHealCostMod.ManaCostRegex.Replace(__result, replacement);
            if (!string.Equals(updated, __result, StringComparison.Ordinal))
            {
                __result = updated;
            }
        }
    }

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    internal static class SkillPatchHelpers
    {
        public static bool IsTargetSkill(Skill skill)
        {
            return LesserHealCostMod.IsTargetSkill(skill);
        }
    }
}
