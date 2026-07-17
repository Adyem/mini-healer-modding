using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MiniHealerImprovementMod
{
    internal sealed class ReferenceEqualityComparer : System.Collections.Generic.IEqualityComparer<object>
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

    internal static class ModHelpers
    {
        internal const string HealPowerColor = "#ff7ac8";
        internal const string LightningColor = "#ffd84a";
        internal const string ShieldColor = "#8bd3ff";
        internal const string HealthColor = "#7ee787";
        internal const string PhysicalColor = "#f0f0f0";
        internal const string DefensiveColor = "#c9d1d9";

        internal static string Colorize(string text, string color)
        {
            return $"<color={color}>{text}</color>";
        }

        internal static string ColorizeByKeyword(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return Colorize(text, GetColorForTooltipText(text));
        }

        internal static string GetColorForAttribute(ArtifactAttribute.AttriubteType attributeType)
        {
            return GetColorForTooltipText(attributeType.ToString());
        }

        internal static string GetColorForTooltipText(string text)
        {
            var normalized = (text ?? string.Empty).ToUpperInvariant();
            if (normalized.Contains("LIGHTNING"))
            {
                return LightningColor;
            }

            if (normalized.Contains("HEALPOWER") || normalized.Contains("HEAL POWER") || normalized.Contains("HEALING"))
            {
                return HealPowerColor;
            }

            if (normalized.Contains("SHIELD"))
            {
                return ShieldColor;
            }

            if (normalized.Contains("HP") || normalized.Contains("HEALTH"))
            {
                return HealthColor;
            }

            if (normalized.Contains("PHYSICAL"))
            {
                return PhysicalColor;
            }

            return DefensiveColor;
        }

        internal static string FormatAttributeRangeLine(ArtifactAttribute.AttriubteType attributeType, float minValue, float maxValue, string label = null)
        {
            var value = Math.Abs(minValue - maxValue) < 0.001f
                ? FormatStatValue(minValue)
                : $"{FormatStatValue(minValue)}-{FormatStatValue(maxValue)}";
            return Colorize($"+{value} {label ?? GetFallbackAttributeLabel(attributeType)}", GetColorForAttribute(attributeType));
        }

        internal static string FormatAtlasStats(IEnumerable<CustomBaseAttributeSpec> specs, string effectText)
        {
            var lines = specs?.Select(spec => FormatAttributeRangeLine(spec.AttributeType, spec.MinValue, spec.MaxValue, spec.Label)).ToList()
                ?? new List<string>();
            if (!string.IsNullOrEmpty(effectText))
            {
                lines.Add(effectText);
            }

            return string.Join("\n", lines.ToArray());
        }

        internal static string ColorizeTerms(string text, params TooltipTerm[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null)
            {
                return text;
            }

            var result = text;
            foreach (var term in terms)
            {
                if (!string.IsNullOrEmpty(term.Text))
                {
                    result = result.Replace(term.Text, Colorize(term.Text, term.Color));
                }
            }

            return result;
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

        internal static void CopyArtifactTemplate(Artifact source, Artifact target)
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

        internal static Artifact EnsureCustomArtifact(
            ArtifactsData data,
            string key,
            Func<ArtifactsData, Artifact> findTemplate,
            Action<Artifact> configure,
            Action<Artifact> wire)
        {
            if (data == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (data.artifactsMap != null
                && data.artifactsMap.TryGetValue(key, out var existingArtifact)
                && existingArtifact != null)
            {
                configure?.Invoke(existingArtifact);
                wire?.Invoke(existingArtifact);
                return existingArtifact;
            }

            var artifact = new Artifact();
            var template = findTemplate?.Invoke(data);
            if (template != null)
            {
                CopyArtifactTemplate(template, artifact);
            }

            configure?.Invoke(artifact);
            wire?.Invoke(artifact);
            ReplaceArtifactCollections(data, artifact);
            return artifact;
        }

        internal static GuardianDropContext GetGuardianContext(
            ArtifactsData data,
            Artifact.ArtifactSlotType slotType,
            IEnumerable<string> excludedKeys,
            string logLabel)
        {
            if (data?.artifactsMap == null)
            {
                return null;
            }

            var excluded = new HashSet<string>(excludedKeys ?? Enumerable.Empty<string>());
            var levelData = LevelDataController.LDM?.levelData;
            var bossData = BossDataController.BDM?.bossData;
            if (levelData?.Levels == null || bossData?.Bosses == null)
            {
                return null;
            }

            foreach (var level in levelData.Levels.Where(item => item != null && item.isGuardian && item.Difficulties != null).OrderBy(item => item.Key))
            {
                foreach (var difficulty in level.Difficulties.Where(item => item?.Bosses != null))
                {
                    foreach (var bossKey in difficulty.Bosses.Where(key => !string.IsNullOrEmpty(key)))
                    {
                        var boss = bossData.Bosses.FirstOrDefault(item => item != null && item.Key == bossKey);
                        if (boss == null)
                        {
                            continue;
                        }

                        var artifacts = ResolveGuardianArtifacts(data, slotType, excluded, difficulty.Loot, boss.depthLoot);
                        if (artifacts.Count == 0)
                        {
                            continue;
                        }

                        var context = new GuardianDropContext
                        {
                            Level = level,
                            LevelDifficulty = difficulty,
                            Boss = boss,
                            Artifacts = artifacts
                        };
                        MiniHealerImprovementModPlugin.LogSource?.LogInfo($"{logLabel} matched guardian {boss.Key} with {artifacts.Count} guardian {slotType.ToString().ToLowerInvariant()} references for balance.");
                        return context;
                    }
                }
            }

            return null;
        }

        internal static List<Artifact> ResolveGuardianArtifacts(
            ArtifactsData data,
            Artifact.ArtifactSlotType slotType,
            ISet<string> excludedKeys,
            params List<string>[] keyLists)
        {
            return keyLists
                .Where(list => list != null)
                .SelectMany(list => list)
                .Where(key => !string.IsNullOrEmpty(key)
                    && !excludedKeys.Contains(key)
                    && data.artifactsMap.TryGetValue(key, out var artifact)
                    && artifact != null
                    && artifact.SlotType == slotType)
                .Select(key => data.artifactsMap[key])
                .GroupBy(artifact => artifact.Key)
                .Select(group => group.First())
                .ToList();
        }

        internal static AttributeRange GetGuardianAttributeRange(
            GuardianDropContext context,
            ArtifactAttribute.AttriubteType attributeType,
            float fallbackMin,
            float fallbackMax,
            Func<string, bool> relatedAttributeFilter)
        {
            var exactRanges = GetGuardianBaseAttributes(context)
                .Where(attribute => attribute.attributeType == attributeType)
                .Select(GetAttributeRange)
                .Where(range => range.IsValid)
                .ToList();
            if (exactRanges.Count > 0)
            {
                return AverageRange(exactRanges);
            }

            var relatedRanges = GetGuardianBaseAttributes(context)
                .Where(attribute => relatedAttributeFilter?.Invoke(attribute.attributeType.ToString()) == true)
                .Select(GetAttributeRange)
                .Where(range => range.IsValid)
                .ToList();
            return relatedRanges.Count > 0 ? AverageRange(relatedRanges) : new AttributeRange(fallbackMin, fallbackMax);
        }

        internal static IEnumerable<ArtifactAttribute> GetGuardianBaseAttributes(GuardianDropContext context)
        {
            if (context?.Artifacts == null || AttributesManager.ATRM == null)
            {
                return Enumerable.Empty<ArtifactAttribute>();
            }

            return context.Artifacts
                .Where(artifact => artifact != null)
                .SelectMany(artifact => AttributesManager.ATRM.getArtifactBaseAttributes(artifact, null, true, false, false) ?? new List<ArtifactAttribute>())
                .Where(attribute => attribute != null);
        }

        internal static AttributeRange GetAttributeRange(ArtifactAttribute attribute)
        {
            if (attribute == null)
            {
                return AttributeRange.Invalid;
            }

            var min = attribute.T3_MIN != 0f || attribute.T3_MAX != 0f ? attribute.T3_MIN : attribute.T1_MIN;
            var max = attribute.T3_MIN != 0f || attribute.T3_MAX != 0f ? attribute.T3_MAX : attribute.T1_MAX;
            return max > 0f && max >= min ? new AttributeRange(min, max) : AttributeRange.Invalid;
        }

        internal static AttributeRange AverageRange(IEnumerable<AttributeRange> ranges)
        {
            var values = ranges?.ToList() ?? new List<AttributeRange>();
            return values.Count == 0
                ? AttributeRange.Invalid
                : new AttributeRange(values.Average(range => range.Min), values.Average(range => range.Max));
        }

        internal static float GetAverageDropWeight(GuardianDropContext context, IEnumerable<string> excludedKeys, float fallback)
        {
            var excluded = new HashSet<string>(excludedKeys ?? Enumerable.Empty<string>());
            var weights = context?.Artifacts?
                .Where(artifact => artifact != null && !excluded.Contains(artifact.Key) && artifact.weight > 0f)
                .Select(artifact => artifact.weight)
                .ToList();
            return weights != null && weights.Count > 0 ? Mathf.Max(0.01f, weights.Average()) : fallback;
        }

        internal static void ReplaceArtifactCollections(ArtifactsData data, Artifact artifact)
        {
            if (data == null || artifact == null)
            {
                return;
            }

            var key = artifact.Key;
            var artifactList = data.ArtifactList != null
                ? new List<Artifact>(data.ArtifactList.Where(item => item != null && item.Key != key))
                : new List<Artifact>((data.Artifacts ?? new Artifact[0]).Where(item => item != null && item.Key != key));
            artifactList.Add(artifact);
            data.ArtifactList = artifactList;

            var artifactArray = data.Artifacts != null
                ? data.Artifacts.Where(item => item != null && item.Key != key).ToList()
                : artifactList.Where(item => item != null && item.Key != key).ToList();
            artifactArray.Add(artifact);
            data.Artifacts = artifactArray.ToArray();

            var artifactMap = data.artifactsMap != null
                ? new Dictionary<string, Artifact>(data.artifactsMap)
                : new Dictionary<string, Artifact>();
            artifactMap[key] = artifact;
            data.artifactsMap = artifactMap;
        }

        internal static List<string> AppendKeyCopy(List<string> source, string key)
        {
            var result = source != null ? new List<string>(source) : new List<string>();
            if (!result.Contains(key))
            {
                result.Add(key);
            }

            return result;
        }

        internal static ArtifactAttribute CreateBaseAttribute(ArtifactAttribute.AttriubteType attributeType, float minValue, float maxValue)
        {
            var baseAttribute = AttributesManager.ATRM?.getBaseAttributByType(attributeType);
            if (baseAttribute == null)
            {
                return null;
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
            return baseAttribute;
        }

        internal static void AddOrReplaceBaseAttribute(List<ArtifactAttribute> attributes, ArtifactAttribute.AttriubteType attributeType, float minValue, float maxValue)
        {
            if (attributes == null)
            {
                return;
            }

            attributes.RemoveAll(attribute => attribute != null && attribute.attributeType == attributeType && attribute.addedType == ArtifactAttribute.AddedType.BASE);
            var baseAttribute = CreateBaseAttribute(attributeType, minValue, maxValue);
            if (baseAttribute != null)
            {
                attributes.Add(baseAttribute);
            }
        }

        internal static void AddOrReplaceBaseAttributes(List<ArtifactAttribute> attributes, IEnumerable<CustomBaseAttributeSpec> specs)
        {
            if (attributes == null || specs == null)
            {
                return;
            }

            foreach (var spec in specs)
            {
                AddOrReplaceBaseAttribute(attributes, spec.AttributeType, spec.MinValue, spec.MaxValue);
            }
        }

        internal static void RemoveSeededRollAttributes(List<ArtifactSaveAttribute> attributes, IEnumerable<ArtifactAttribute.AttriubteType> attributeTypes)
        {
            if (attributes == null || attributeTypes == null)
            {
                return;
            }

            var attributeTypeSet = new HashSet<ArtifactAttribute.AttriubteType>(attributeTypes);
            attributes.RemoveAll(attribute =>
                attribute != null
                && attributeTypeSet.Contains(attribute.attributeType)
                && (attribute.addedType == ArtifactAttribute.AddedType.ROLL_BASE || attribute.addedType == ArtifactAttribute.AddedType.ROLL));
        }

        internal static void AppendUniqueDescription(ref List<string> descriptions, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            descriptions = descriptions ?? new List<string>();
            if (!descriptions.Contains(text))
            {
                descriptions.Add(text);
            }
        }

        internal static void ConfigureCustomArtifact(Artifact artifact, ArtifactDataController controller, CustomArtifactSpec spec)
        {
            if (artifact == null)
            {
                return;
            }

            artifact.ArtifactName = spec.Name;
            artifact.Key = spec.Key;
            artifact.Rarity = spec.Rarity;
            artifact.SlotType = spec.SlotType;
            artifact.Type = spec.Type;
            artifact.MutationPoolType = spec.MutationPoolTypes != null
                ? new List<Artifact.ArtifactCharacterType>(spec.MutationPoolTypes)
                : new List<Artifact.ArtifactCharacterType>();
            artifact.HiddenItemLevel = spec.HiddenItemLevel;
            artifact.DropRate = spec.DropRate;
            artifact.weight = spec.DropRate;
            artifact.isEquippable = true;
            artifact.isMutateable = true;
            artifact.isAugmentable = true;
            artifact.isDiscoverable = true;
            artifact.isDepth = spec.IsDepth;
            artifact.isDivine = spec.IsDivine;
            artifact.linkedDivineArtifactKey = string.Empty;
            artifact.linkedNormalArtifactKey = string.Empty;
            artifact.droppedBossName = spec.DroppedBossName;
            artifact.droppedLevelName = spec.DroppedLevelName;
            artifact.PurchaseMat = ResolveGreaterAlchemyShardKey() ?? spec.PurchaseMaterialFallbackKey;
            artifact.PurchasePrice = spec.PurchasePrice;
            artifact.specialDesc = string.Empty;
            // Custom items must override the cloned template icon; otherwise the generated icon
            // is silently ignored because every cloned artifact already has an Icon assigned.
            artifact.Icon = spec.FallbackIcon ?? artifact.Icon ?? controller?.DEFAULT_ITEM_ICON;

            artifact.possibleMutationAttributes = artifact.possibleMutationAttributes ?? new List<ArtifactAttribute.AttriubteType>();
            artifact.possibleRolledAttributes = artifact.possibleRolledAttributes ?? new List<ArtifactAttribute.AttriubteType>();
            artifact.tempRolledSavedAttributes = artifact.tempRolledSavedAttributes ?? new List<ArtifactSaveAttribute>();
            artifact.tempRolledSockets = artifact.tempRolledSockets ?? new List<Socket>();
            artifact.artifactVFXSpawnType = artifact.artifactVFXSpawnType ?? new List<Artifact.ArtifactVFXSpawnType>();
            EnsureAttributePools(artifact, spec.BaseAttributeTypes);
            RemoveSeededRollAttributes(artifact.tempRolledSavedAttributes, spec.BaseAttributeTypes);

            if (controller?.baseArtifactSearchStringMap != null && !string.IsNullOrEmpty(spec.SearchText))
            {
                controller.baseArtifactSearchStringMap[spec.Key] = spec.SearchText;
            }
        }

        internal static void EnsureAttributePools(Artifact artifact, IEnumerable<ArtifactAttribute.AttriubteType> attributeTypes)
        {
            if (artifact == null || attributeTypes == null)
            {
                return;
            }

            artifact.possibleRolledAttributes = artifact.possibleRolledAttributes ?? new List<ArtifactAttribute.AttriubteType>();
            artifact.possibleMutationAttributes = artifact.possibleMutationAttributes ?? new List<ArtifactAttribute.AttriubteType>();
            foreach (var attributeType in attributeTypes)
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

        internal static bool TryEnsureSaveAttributes(ArtifactSaveInfo saveInfo, string artifactKey, IEnumerable<ArtifactAttribute.AttriubteType> attributeTypes)
        {
            if (saveInfo?.ArtifactKey != artifactKey)
            {
                return false;
            }

            saveInfo.SaveAttributes = saveInfo.SaveAttributes ?? new List<ArtifactSaveAttribute>();
            saveInfo.AttributeUpgrade = saveInfo.AttributeUpgrade ?? new List<ArtifactAttrUpgradeSaveInfo>();
            RemoveSeededRollAttributes(saveInfo.SaveAttributes, attributeTypes);
            return true;
        }

        internal static bool TryEnsureSaveAttributes(Artifact artifact, string artifactKey, List<ArtifactSaveAttribute> attributes, IEnumerable<ArtifactAttribute.AttriubteType> attributeTypes)
        {
            if (artifact?.Key != artifactKey)
            {
                return false;
            }

            RemoveSeededRollAttributes(attributes, attributeTypes);
            return true;
        }

        internal static bool TryApplyFixedBaseAttributes(Artifact artifact, string artifactKey, ArtifactSaveInfo saveInfo, ref List<ArtifactAttribute> attributes, IEnumerable<CustomBaseAttributeSpec> specs, IEnumerable<ArtifactAttribute.AttriubteType> cleanupTypes)
        {
            if (artifact?.Key != artifactKey || AttributesManager.ATRM == null)
            {
                return false;
            }

            attributes = new List<ArtifactAttribute>();
            TryEnsureSaveAttributes(saveInfo, artifactKey, cleanupTypes);
            AddOrReplaceBaseAttributes(attributes, specs);
            return true;
        }

        internal static bool TryGetCustomPurchaseMaterial(Artifact artifact, string artifactKey, string fallbackMaterialKey, ref StackableMaterial result)
        {
            if (artifact?.Key != artifactKey)
            {
                return true;
            }

            result = MaterialDataController.MATDM?.getMaterialByKey(artifact.PurchaseMat)
                ?? MaterialDataController.MATDM?.getMaterialByKey(fallbackMaterialKey);
            return result == null;
        }

        internal static void RefreshAtlasSubtitle(ItemAtlasUIManager manager, string artifactKey, string fallbackSubtitle)
        {
            var selectedArtifact = GetFieldValue(manager, "selectedArtifact") as Artifact;
            if (selectedArtifact?.Key != artifactKey)
            {
                return;
            }

            SetTextField(manager, "UniqueText", ArtifactDataController.ADM?.getArtifactTypeDescByArtifact(selectedArtifact, null) ?? fallbackSubtitle);
        }

        internal static string ResolveGreaterAlchemyShardKey()
        {
            var materials = MaterialDataController.MATDM?.materialData?.Materials;
            if (materials == null)
            {
                return null;
            }

            return materials
                .Where(material => material != null)
                .Select(material => new
                {
                    Material = material,
                    Search = $"{material.Key} {material.Name} {material.Description}".ToLowerInvariant()
                })
                .Where(item => item.Search.Contains("alch") && item.Search.Contains("shard"))
                .OrderByDescending(item => item.Search.Contains("greater"))
                .ThenByDescending(item => item.Search.Contains("large") || item.Search.Contains("glorious"))
                .Select(item => item.Material.Key)
                .FirstOrDefault();
        }

        internal static void SetTextField(object owner, string fieldName, string text)
        {
            var textComponent = GetFieldValue(owner, fieldName);
            if (textComponent == null)
            {
                return;
            }

            var textProperty = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            textProperty?.SetValue(textComponent, text, null);
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
            Debug.LogError($"[Mini Healer Improvement Mod] Item atlas refresh failed. slot={slot}, subtypes={subtypes}, error={exception}");
        }

        private static string FormatStatValue(float value)
        {
            return Math.Abs(value - Math.Round(value)) < 0.001f
                ? ((int)Math.Round(value)).ToString()
                : value.ToString("0.##");
        }

        private static string GetFallbackAttributeLabel(ArtifactAttribute.AttriubteType attributeType)
        {
            var name = attributeType.ToString()
                .Replace("INCREASE_", string.Empty)
                .Replace("DECREASE_", string.Empty)
                .Replace("_FLAT", string.Empty)
                .Replace("_PERCENT", "%")
                .Replace("_", " ")
                .ToLowerInvariant();
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
        }
    }

    internal sealed class GuardianDropContext
    {
        internal Level Level;
        internal LevelDifficultyData LevelDifficulty;
        internal Boss Boss;
        internal List<Artifact> Artifacts;
    }

    internal struct AttributeRange
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

    internal readonly struct CustomBaseAttributeSpec
    {
        internal CustomBaseAttributeSpec(ArtifactAttribute.AttriubteType attributeType, float minValue, float maxValue, string label = null)
        {
            AttributeType = attributeType;
            MinValue = minValue;
            MaxValue = maxValue;
            Label = label;
        }

        internal ArtifactAttribute.AttriubteType AttributeType { get; }
        internal float MinValue { get; }
        internal float MaxValue { get; }
        internal string Label { get; }
    }

    internal readonly struct TooltipTerm
    {
        internal TooltipTerm(string text, string color)
        {
            Text = text;
            Color = color;
        }

        internal string Text { get; }
        internal string Color { get; }
    }

    internal sealed class CustomArtifactSpec
    {
        internal string Key { get; set; }
        internal string Name { get; set; }
        internal Artifact.RarityType Rarity { get; set; }
        internal Artifact.ArtifactSlotType SlotType { get; set; }
        internal Artifact.ArtifactType Type { get; set; }
        internal List<Artifact.ArtifactCharacterType> MutationPoolTypes { get; set; }
        internal int HiddenItemLevel { get; set; }
        internal float DropRate { get; set; }
        internal bool IsDepth { get; set; }
        internal bool IsDivine { get; set; }
        internal string DroppedBossName { get; set; }
        internal string DroppedLevelName { get; set; }
        internal string PurchaseMaterialFallbackKey { get; set; }
        internal int PurchasePrice { get; set; }
        internal Sprite FallbackIcon { get; set; }
        internal ArtifactAttribute.AttriubteType[] BaseAttributeTypes { get; set; }
        internal string SearchText { get; set; }
    }
}
