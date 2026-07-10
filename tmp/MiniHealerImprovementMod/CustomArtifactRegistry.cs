using System;
using System.Collections.Generic;
using HarmonyLib;

namespace MiniHealerImprovementMod
{
    internal static class CustomArtifactRegistry
    {
        internal static bool TryInjectArtifacts()
        {
            var aegisReady = AegisChoirMod.TryInjectAegisChoir();
            var resonanceReady = ResonanceScepterMod.TryInjectResonanceScepter();
            var stormheartReady = StormheartCarapaceMod.TryInjectStormheartCarapace();
            return aegisReady && resonanceReady && stormheartReady;
        }

        internal static bool TryInjectLootSources()
        {
            var aegisReady = AegisChoirMod.TryInjectAegisChoirLootSource();
            var resonanceReady = ResonanceScepterMod.TryInjectResonanceScepterLootSource();
            var stormheartReady = StormheartCarapaceMod.TryInjectStormheartCarapaceLootSource();
            return aegisReady && resonanceReady && stormheartReady;
        }

        internal static void AddToBossSpecificDropTable(string bossKey, LootTableManager.ArtifactLootDropTable table)
        {
            AegisChoirMod.AddAegisChoirToDropTable(table);
            ResonanceScepterMod.AddResonanceToDropTable(bossKey, table);
            StormheartCarapaceMod.AddStormheartToDropTable(bossKey, table);
        }

        internal static bool TryHandleArtifactUnlocked(Artifact artifact, ref bool result)
        {
            if (!IsCustomArtifact(artifact))
            {
                return true;
            }

            result = true;
            artifact.isLockedInAtlas = false;
            return false;
        }

        internal static bool TryGetPurchaseMaterial(Artifact artifact, ref StackableMaterial result)
        {
            if (!AegisChoirMod.TryGetAegisChoirPurchaseMaterial(artifact, ref result))
            {
                return false;
            }

            if (!ResonanceScepterMod.TryGetResonancePurchaseMaterial(artifact, ref result))
            {
                return false;
            }

            return StormheartCarapaceMod.TryGetStormheartPurchaseMaterial(artifact, ref result);
        }

        internal static void EnsureSaveAttributes(Artifact artifact, List<ArtifactSaveAttribute> attributes)
        {
            AegisChoirMod.EnsureAegisChoirSaveAttributes(artifact, attributes);
            ResonanceScepterMod.EnsureResonanceSaveAttributes(artifact, attributes);
            StormheartCarapaceMod.EnsureStormheartSaveAttributes(artifact, attributes);
        }

        internal static void EnsureSaveAttributes(ArtifactSaveInfo saveInfo)
        {
            AegisChoirMod.EnsureAegisChoirSaveAttributes(saveInfo);
            ResonanceScepterMod.EnsureResonanceSaveAttributes(saveInfo);
            StormheartCarapaceMod.EnsureStormheartSaveAttributes(saveInfo);
        }

        internal static void EnsureBaseAttributes(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> attributes)
        {
            AegisChoirMod.EnsureAegisChoirBaseAttributes(artifact, saveInfo, ref attributes);
            ResonanceScepterMod.EnsureResonanceBaseAttributes(artifact, saveInfo, ref attributes);
            StormheartCarapaceMod.EnsureStormheartBaseAttributes(artifact, saveInfo, ref attributes);
        }

        internal static void AppendDescriptions(Artifact artifact, ref List<string> descriptions)
        {
            AegisChoirMod.AppendAegisChoirDescription(artifact, ref descriptions);
            ResonanceScepterMod.AppendResonanceDescription(artifact, ref descriptions);
            StormheartCarapaceMod.AppendStormheartDescription(artifact, ref descriptions);
        }

        internal static void RefreshAtlasInfo(ItemAtlasUIManager manager)
        {
            AegisChoirMod.RefreshAegisChoirAtlasInfo(manager);
            ResonanceScepterMod.RefreshResonanceAtlasInfo(manager);
            StormheartCarapaceMod.RefreshStormheartAtlasInfo(manager);
        }

        internal static bool IsCustomArtifact(Artifact artifact)
        {
            return artifact?.Key == AegisChoirMod.AegisChoirKey
                || artifact?.Key == ResonanceScepterMod.ResonanceScepterKey
                || artifact?.Key == StormheartCarapaceMod.StormheartCarapaceKey;
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.getBossSpecificDropTable))]
    internal static class LootTableManager_GetBossSpecificDropTable_CustomArtifacts_Patch
    {
        private static void Postfix(string bossKey, ref LootTableManager.ArtifactLootDropTable __result)
        {
            CustomArtifactRegistry.AddToBossSpecificDropTable(bossKey, __result);
        }
    }

    [HarmonyPatch(typeof(ArtifactDataController), nameof(ArtifactDataController.isArtifactUnlocked))]
    internal static class ArtifactDataController_IsArtifactUnlocked_CustomArtifacts_Patch
    {
        private static bool Prefix(Artifact artifact, ref bool __result)
        {
            return CustomArtifactRegistry.TryHandleArtifactUnlocked(artifact, ref __result);
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
    internal static class ItemAtlasUIManager_RefreshArtifactInfoView_CustomArtifacts_Patch
    {
        private static void Postfix(ItemAtlasUIManager __instance)
        {
            CustomArtifactRegistry.RefreshAtlasInfo(__instance);
        }

        private static Exception Finalizer(ItemAtlasUIManager __instance, Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }

            ModHelpers.LogAtlasRefreshFailure(__instance, __exception);
            var selectedArtifact = ModHelpers.GetFieldValue(__instance, "selectedArtifact") as Artifact;
            if (CustomArtifactRegistry.IsCustomArtifact(selectedArtifact))
            {
                CustomArtifactRegistry.RefreshAtlasInfo(__instance);
                return null;
            }

            return __exception;
        }
    }

    [HarmonyPatch(typeof(ItemAtlasUIManager), "getArtifactPurchaseMat")]
    internal static class ItemAtlasUIManager_GetArtifactPurchaseMat_CustomArtifacts_Patch
    {
        private static bool Prefix(Artifact artifact, ref StackableMaterial __result)
        {
            return CustomArtifactRegistry.TryGetPurchaseMaterial(artifact, ref __result);
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.rollAttributesByLevel))]
    internal static class LootTableManager_RollAttributesByLevel_CustomArtifacts_Patch
    {
        private static void Postfix(Artifact artifact, ref List<ArtifactSaveAttribute> __result)
        {
            CustomArtifactRegistry.EnsureSaveAttributes(artifact, __result);
        }
    }

    [HarmonyPatch(typeof(LootTableManager), nameof(LootTableManager.rollAttributesModular))]
    internal static class LootTableManager_RollAttributesModular_CustomArtifacts_Patch
    {
        private static void Postfix(Artifact artifact, ref List<ArtifactSaveAttribute> __result)
        {
            CustomArtifactRegistry.EnsureSaveAttributes(artifact, __result);
        }
    }

    [HarmonyPatch(typeof(ArtifactSaveInfo), nameof(ArtifactSaveInfo.upgradeRandomBaseAttribute))]
    internal static class ArtifactSaveInfo_UpgradeRandomBaseAttribute_CustomArtifacts_Patch
    {
        private static void Prefix(ArtifactSaveInfo __instance)
        {
            CustomArtifactRegistry.EnsureSaveAttributes(__instance);
        }
    }

    [HarmonyPatch(typeof(AttributesManager), nameof(AttributesManager.getArtifactBaseAttributes))]
    internal static class AttributesManager_GetArtifactBaseAttributes_CustomArtifacts_Patch
    {
        private static void Postfix(Artifact artifact, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> __result)
        {
            CustomArtifactRegistry.EnsureBaseAttributes(artifact, saveInfo, ref __result);
        }
    }

    [HarmonyPatch(typeof(OtherGameDataController), "getDescriptionByArtifact")]
    internal static class OtherGameDataController_GetDescriptionByArtifact_CustomArtifacts_Patch
    {
        private static void Postfix(Artifact artifact, ref List<string> __result)
        {
            CustomArtifactRegistry.AppendDescriptions(artifact, ref __result);
        }
    }

    [HarmonyPatch(typeof(LevelDescriptionModalController), nameof(LevelDescriptionModalController.updateLootView))]
    internal static class LevelDescriptionModalController_UpdateLootView_CustomArtifacts_Patch
    {
        private static void Prefix()
        {
            CustomArtifactRegistry.TryInjectArtifacts();
            CustomArtifactRegistry.TryInjectLootSources();
        }
    }
}
