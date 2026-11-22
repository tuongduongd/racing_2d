using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RacingGame.Editor
{
    /// <summary>
    /// Automatically enforces consistent Sprite import settings (PPU, pivots, transparency,
    /// readability, and compression) for all art assets under Assets/Art/.
    /// </summary>
    public sealed class ArtAssetImportPostprocessor : AssetPostprocessor
    {
        private const string ArtRootPrefix = "Assets/Art/";
        private const string BikesPrefix = "Assets/Art/Sprites/Bikes/";
        private const string PropsPrefix = "Assets/Art/Sprites/Props/";
        private const string PreviewsPrefix = "Assets/Art/Previews/";
        private const string MapsPrefix = "Assets/Art/Maps/";
        private const string UiPrefix = "Assets/Art/UI/";

        public const float BikeSpritePixelsPerUnit = 160f;
        public const float BikePivotY = 16f / 352f;

        [MenuItem("Tools/Road Rash/Reimport Stage 1 Art Assets")]
        public static void ReimportAllArtAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
            {
                Debug.LogWarning("[ArtAssetImportPostprocessor] Folder Assets/Art does not exist.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" });
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtAssetImportPostprocessor] Reimported {guids.Length} art texture(s) under Assets/Art.");
        }

        private void OnPreprocessTexture()
        {
            string normalizedPath = (assetPath ?? string.Empty).Replace('\\', '/');
            if (!normalizedPath.StartsWith(ArtRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.mipmapEnabled = false;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.filterMode = FilterMode.Bilinear;

            if (normalizedPath.StartsWith(BikesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = BikeSpritePixelsPerUnit;

                settings.alphaIsTransparency = true;
                settings.readable = true;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, BikePivotY);
                settings.spritePixelsPerUnit = BikeSpritePixelsPerUnit;
                importer.SetTextureSettings(settings);
                return;
            }

            if (normalizedPath.StartsWith(PropsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
                float ppu = ResolvePropPixelsPerUnit(fileName);

                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.spritePixelsPerUnit = ppu;

                settings.alphaIsTransparency = true;
                settings.readable = false;
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
                settings.spritePixelsPerUnit = ppu;
                importer.SetTextureSettings(settings);
                return;
            }

            if (normalizedPath.StartsWith(PreviewsPrefix, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(MapsPrefix, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(UiPrefix, StringComparison.OrdinalIgnoreCase))
            {
                importer.alphaIsTransparency = false;
                importer.isReadable = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.spritePixelsPerUnit = 100f;

                settings.alphaIsTransparency = false;
                settings.readable = false;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spritePixelsPerUnit = 100f;
                importer.SetTextureSettings(settings);
            }
        }

        private static float ResolvePropPixelsPerUnit(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return 100f;
            if (fileName.StartsWith("Prop_NeonSign_", StringComparison.OrdinalIgnoreCase)) return 96f;
            if (string.Equals(fileName, "Prop_StreetLamp", StringComparison.OrdinalIgnoreCase)) return 98f;
            if (string.Equals(fileName, "Obstacle_RoadBarrier", StringComparison.OrdinalIgnoreCase)) return 185f;
            if (string.Equals(fileName, "Obstacle_TrafficCone", StringComparison.OrdinalIgnoreCase)) return 195f;
            if (string.Equals(fileName, "Obstacle_OilBarrel", StringComparison.OrdinalIgnoreCase)) return 186f;
            if (string.Equals(fileName, "Obstacle_ParkedBus", StringComparison.OrdinalIgnoreCase)) return 100f;
            if (string.Equals(fileName, "Prop_PalmTree", StringComparison.OrdinalIgnoreCase)) return 108f;
            if (string.Equals(fileName, "Prop_Tree", StringComparison.OrdinalIgnoreCase)) return 115f;
            if (string.Equals(fileName, "Prop_HighwaySign", StringComparison.OrdinalIgnoreCase)) return 105f;
            return 100f;
        }
    }
}
