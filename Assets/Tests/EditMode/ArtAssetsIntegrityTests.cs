using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RacingGame.Tests
{
    /// <summary>
    /// Verifies Stage 1 art assets under Assets/Art/: existence, Sprite import settings,
    /// PPU/pivots, transparency alpha channels, uniform rear-tire alignment, and dimensions.
    /// </summary>
    public class ArtAssetsIntegrityTests
    {
        private static readonly string[] BikeSpritePaths =
        {
            "Assets/Art/Sprites/Bikes/Bike_XanhSM_Upright.png",
            "Assets/Art/Sprites/Bikes/Bike_XanhSM_LeanRightLight.png",
            "Assets/Art/Sprites/Bikes/Bike_XanhSM_LeanRightDeep.png",
            "Assets/Art/Sprites/Bikes/Bike_XanhSM_LeanLeftLight.png",
            "Assets/Art/Sprites/Bikes/Bike_XanhSM_LeanLeftDeep.png",
            "Assets/Art/Sprites/Bikes/Bike_XanhSM_KickLeft.png"
        };

        private static readonly string[] PropSpritePaths =
        {
            "Assets/Art/Sprites/Props/Prop_NeonSign_PhoDem.png",
            "Assets/Art/Sprites/Props/Prop_NeonSign_CaPhe.png",
            "Assets/Art/Sprites/Props/Prop_StreetLamp.png",
            "Assets/Art/Sprites/Props/Obstacle_RoadBarrier.png",
            "Assets/Art/Sprites/Props/Obstacle_TrafficCone.png",
            "Assets/Art/Sprites/Props/Obstacle_OilBarrel.png",
            "Assets/Art/Sprites/Props/Obstacle_ParkedBus.png",
            "Assets/Art/Sprites/Props/Prop_PalmTree.png",
            "Assets/Art/Sprites/Props/Prop_Tree.png",
            "Assets/Art/Sprites/Props/Prop_HighwaySign.png"
        };

        private static readonly string[] FullFrameArtPaths =
        {
            "Assets/Art/Previews/Preview_XanhSM.png",
            "Assets/Art/Previews/Preview_ShopeeFood.png",
            "Assets/Art/Previews/Preview_Grab.png",
            "Assets/Art/Previews/Preview_Ahamove.png",
            "Assets/Art/Maps/Banner_Map01_CityMorning.png",
            "Assets/Art/Maps/Banner_Map02_CoastalHighway.png",
            "Assets/Art/Maps/Banner_Map03_NeonNightExpress.png",
            "Assets/Art/Maps/Banner_Map04_CityNightDistrict.png",
            "Assets/Art/UI/Bg_Lobby_SaigonNight.png"
        };

        [Test]
        public void BikeRearViewSprites_AllSixPosesExistWithUniformCanvasTransparencyAndPivot()
        {
            const float expectedPivotY = 16f / 352f;

            for (int i = 0; i < BikeSpritePaths.Length; i++)
            {
                string path = BikeSpritePaths[i];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, $"Expected bike sprite at {path}");

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"Expected TextureImporter at {path}");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.IsTrue(importer.isReadable, $"Bike sprite must stay readable for palette swap: {path}");
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.AreEqual(160f, importer.spritePixelsPerUnit, 0.01f, path);

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.AreEqual(0.5f, settings.spritePivot.x, 0.002f, path);
                Assert.AreEqual(expectedPivotY, settings.spritePivot.y, 0.002f, path);

                Texture2D tex = sprite.texture;
                Assert.AreEqual(320, tex.width, path);
                Assert.AreEqual(352, tex.height, path);

                AssertCornerPixelsTransparent(tex, path);

                // Rear tire contact band sits around X=160, Unity texture Y in [16..48] (bottom-up)
                bool foundOpaqueTirePixel = false;
                for (int y = 16; y <= 48 && !foundOpaqueTirePixel; y++)
                {
                    for (int x = 140; x <= 180; x++)
                    {
                        if (tex.GetPixel(x, y).a > 0.9f)
                        {
                            foundOpaqueTirePixel = true;
                            break;
                        }
                    }
                }

                Assert.IsTrue(foundOpaqueTirePixel, $"Expected rear tire aligned at bottom center for {path}");
            }
        }

        [Test]
        public void PropAndObstacleSprites_AllTenSpritesExistWithTransparencyAndGroundPivot()
        {
            for (int i = 0; i < PropSpritePaths.Length; i++)
            {
                string path = PropSpritePaths[i];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, $"Expected prop sprite at {path}");

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"Expected TextureImporter at {path}");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.IsFalse(importer.isReadable, $"Prop sprite should be non-readable for memory efficiency: {path}");
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.GreaterOrEqual(importer.spritePixelsPerUnit, 90f, path);

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.AreEqual(0.5f, settings.spritePivot.x, 0.002f, path);
                Assert.AreEqual(0f, settings.spritePivot.y, 0.002f, path);

                Texture2D temp = LoadRawPngTexture(path);
                try
                {
                    AssertCornerPixelsTransparent(temp, path);
                    AssertHasOpaqueForeground(temp, path);
                }
                finally
                {
                    Object.DestroyImmediate(temp);
                }
            }
        }

        [Test]
        public void PreviewBannerAndLobbyBackdropSprites_AllNineExistWithExpectedDimensions()
        {
            for (int i = 0; i < FullFrameArtPaths.Length; i++)
            {
                string path = FullFrameArtPaths[i];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, $"Expected full-frame sprite at {path}");

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"Expected TextureImporter at {path}");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.IsFalse(importer.isReadable, $"Full-frame sprite should be non-readable: {path}");
                Assert.AreEqual(1376, sprite.texture.width, path);
                Assert.AreEqual(768, sprite.texture.height, path);
            }
        }

        private static Texture2D LoadRawPngTexture(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(bytes), $"Failed to decode PNG at {assetPath}");
            return tex;
        }

        private static void AssertCornerPixelsTransparent(Texture2D tex, string path)
        {
            Assert.LessOrEqual(tex.GetPixel(0, 0).a, 0.01f, $"Bottom-left corner not transparent: {path}");
            Assert.LessOrEqual(tex.GetPixel(tex.width - 1, 0).a, 0.01f, $"Bottom-right corner not transparent: {path}");
            Assert.LessOrEqual(tex.GetPixel(0, tex.height - 1).a, 0.01f, $"Top-left corner not transparent: {path}");
            Assert.LessOrEqual(tex.GetPixel(tex.width - 1, tex.height - 1).a, 0.01f, $"Top-right corner not transparent: {path}");
        }

        private static void AssertHasOpaqueForeground(Texture2D tex, string path)
        {
            Color32[] pixels = tex.GetPixels32();
            int opaqueCount = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a >= 240)
                {
                    opaqueCount++;
                }
            }

            Assert.Greater(opaqueCount, 500, $"Expected opaque foreground pixels in {path}");
        }
    }
}
