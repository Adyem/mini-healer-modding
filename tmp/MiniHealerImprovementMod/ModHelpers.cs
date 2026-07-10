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
    }
}
