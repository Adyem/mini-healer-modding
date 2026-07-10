using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniHealerImprovementMod
{
    [BepInPlugin("com.codex.minihealer.improvements", "Mini Healer Improvement Mod", "1.0.0")]
    public sealed class MiniHealerImprovementModPlugin : BaseUnityPlugin
    {
        private const float CostMultiplier = 0.85f;
        private const string TargetSkillName = "Lesser Heal";
        private const string TargetSkillKeyFragment = "lesserheal";
        internal static readonly Regex ManaCostRegex = new Regex(@"\b\d+\s*(?i:mana)\b", RegexOptions.Compiled);
        internal static ManualLogSource LogSource { get; private set; }

        private readonly HashSet<object> _patchedSkills = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private bool _skillPatched;
        private bool _artifactInjected;
        private bool _lootInjected;

        private void Awake()
        {
            LogSource = Logger;
            Logger.LogInfo("Mini Healer improvement mod loaded");
            new Harmony("com.codex.minihealer.improvements").PatchAll();
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
                _artifactInjected = AegisChoirMod.TryInjectAegisChoir();
                _lootInjected = AegisChoirMod.TryInjectAegisChoirLootSource();

                if (_skillPatched && _artifactInjected && _lootInjected)
                {
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            Logger.LogWarning("Timed out waiting for all Mini Healer improvement changes to attach.");
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
            var skillData = ModHelpers.GetFieldValue(controller, "skillData");
            if (skillData == null)
            {
                return false;
            }

            var skills = ModHelpers.GetFieldValue(skillData, "Skills") as Array;
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
                    LogSource?.LogWarning("Found Lesser Heal but could not access manaCost.");
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
                LogSource?.LogInfo($"Patched {GetSkillLabel(skill)} total mana cost: {originalCost} -> {reducedCost}");
            }

            return patchedAny;
        }

        internal static bool IsTargetSkill(object skill)
        {
            var name = Convert.ToString(ModHelpers.GetFieldValue(skill, "SkillName")) ?? string.Empty;
            var key = Convert.ToString(ModHelpers.GetFieldValue(skill, "Key")) ?? string.Empty;

            return string.Equals(name, TargetSkillName, StringComparison.OrdinalIgnoreCase)
                || key.IndexOf(TargetSkillKeyFragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetSkillLabel(object skill)
        {
            var name = Convert.ToString(ModHelpers.GetFieldValue(skill, "SkillName"));
            var key = Convert.ToString(ModHelpers.GetFieldValue(skill, "Key"));

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key))
            {
                return $"{name} ({key})";
            }

            return name ?? key ?? skill.GetType().Name;
        }
    }

    [HarmonyPatch(typeof(Skill), nameof(Skill.getTotalManaCost))]
    internal static class Skill_GetTotalManaCost_Patch
    {
        private static void Postfix(Skill __instance, ref int __result)
        {
            if (!MiniHealerImprovementModPlugin.IsTargetSkill(__instance))
            {
                return;
            }

            __result = Math.Max(1, (int)Math.Round(__result * 0.85f, MidpointRounding.AwayFromZero));
        }
    }

    [HarmonyPatch(typeof(Skill), nameof(Skill.getDescription))]
    internal static class Skill_GetDescription_Patch
    {
        private static void Postfix(Skill __instance, ref string __result)
        {
            if (!MiniHealerImprovementModPlugin.IsTargetSkill(__instance) || string.IsNullOrEmpty(__result))
            {
                return;
            }

            var reducedCost = Math.Max(1, __instance.getTotalManaCost(false, null));
            var replacement = $"{reducedCost} mana";
            var updated = MiniHealerImprovementModPlugin.ManaCostRegex.Replace(__result, replacement);
            if (!string.Equals(updated, __result, StringComparison.Ordinal))
            {
                __result = updated;
            }
        }
    }
}
