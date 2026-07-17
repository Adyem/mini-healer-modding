using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MiniHealerImprovementMod
{
    internal static class CustomArtifactIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite Load(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            Sprite cached;
            if (Cache.TryGetValue(fileName, out cached))
            {
                return cached;
            }

            var assembly = typeof(CustomArtifactIcons).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                return null;
            }

            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = fileName,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    if (!ImageConversion.LoadImage(texture, buffer.ToArray(), true))
                    {
                        UnityEngine.Object.Destroy(texture);
                        return null;
                    }

                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        texture.width);
                    sprite.name = fileName;
                    Cache[fileName] = sprite;
                    return sprite;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MiniHealerImprovementMod] Failed to load embedded icon {fileName}: {ex.Message}");
                return null;
            }
        }
    }
}
