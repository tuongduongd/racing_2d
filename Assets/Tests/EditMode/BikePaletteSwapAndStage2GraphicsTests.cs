using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RacingGame.Tests
{
    /// <summary>
    /// Stage 2 EditMode tests verifying cached multi-pose palette swapping,
    /// safe fallbacks on null/non-readable sources, and Stage 2 art wiring on data assets.
    /// </summary>
    public class BikePaletteSwapAndStage2GraphicsTests
    {
        [Test]
        public void BikePaletteSwapper_CachesSameInstanceForAllFivePalettesAndRemapsBrandColors()
        {
            BikePaletteSwapper.ClearCache();
            try
            {
                BikeSkinData xanhSm = AssetDatabase.LoadAssetAtPath<BikeSkinData>("Assets/ScriptableObjects/Skins/Skin_XanhSM.asset");
                Assert.IsNotNull(xanhSm, "Skin_XanhSM.asset should exist.");
                Assert.IsTrue(xanhSm.HasMultiPoseSprites, "Skin_XanhSM.asset should have multi-pose sprites wired.");

                BikePaletteKind[] palettes =
                {
                    BikePaletteKind.XanhSM,
                    BikePaletteKind.ShopeeFood,
                    BikePaletteKind.Grab,
                    BikePaletteKind.Ahamove,
                    BikePaletteKind.Police
                };

                var firstSets = new BikeSpriteSet[palettes.Length];
                for (int i = 0; i < palettes.Length; i++)
                {
                    BikeSpriteSet set = BikePaletteSwapper.GetOrCreateSet(palettes[i], xanhSm);
                    Assert.IsNotNull(set, $"Expected BikeSpriteSet for {palettes[i]}");
                    Assert.IsNotNull(set.Upright0);
                    Assert.IsNotNull(set.Upright1);
                    Assert.IsNotNull(set.LeanLeft1);
                    Assert.IsNotNull(set.LeanLeft2);
                    Assert.IsNotNull(set.LeanRight1);
                    Assert.IsNotNull(set.LeanRight2);
                    Assert.IsNotNull(set.KickLeft);
                    Assert.IsNotNull(set.KickRight);
                    Assert.IsNotNull(set.HitStagger);
                    Assert.IsTrue(set.FlipKickRight, $"Multi-pose set for {palettes[i]} should enable FlipKickRight.");
                    firstSets[i] = set;
                }

                Assert.AreEqual(5, BikePaletteSwapper.CachedEntryCount, "Expected exactly 5 cached palette entries.");

                // Second lookup must return exact cached instances (zero extra texture/sprite allocations)
                for (int i = 0; i < palettes.Length; i++)
                {
                    BikeSpriteSet second = BikePaletteSwapper.GetOrCreateSet(palettes[i], xanhSm);
                    Assert.AreSame(firstSets[i], second, $"Cache miss/reallocation detected for {palettes[i]}");
                }
                Assert.AreEqual(5, BikePaletteSwapper.CachedEntryCount);

                // Verify actual color remapping in the brand jacket/delivery-box torso region
                Color xanhAvg = SampleCenterRegionColor(firstSets[0].Upright0.texture);
                Color shopeeAvg = SampleCenterRegionColor(firstSets[1].Upright0.texture);
                Color grabAvg = SampleCenterRegionColor(firstSets[2].Upright0.texture);
                Color ahamoveAvg = SampleCenterRegionColor(firstSets[3].Upright0.texture);
                Color policeAvg = SampleCenterRegionColor(firstSets[4].Upright0.texture);

                Assert.Greater(shopeeAvg.r, shopeeAvg.b + 0.15f, "ShopeeFood palette should shift toward orange-red.");
                Assert.Greater(grabAvg.g, grabAvg.r + 0.12f, "Grab palette should shift toward emerald green.");
                Assert.Greater(grabAvg.g, grabAvg.b + 0.08f, "Grab palette should stay green-dominant over blue.");
                Assert.Greater(ahamoveAvg.r, ahamoveAvg.b + 0.12f, "Ahamove palette should shift toward vivid orange.");
                float policeDiff = Mathf.Abs(policeAvg.r - xanhAvg.r) + Mathf.Abs(policeAvg.g - xanhAvg.g) + Mathf.Abs(policeAvg.b - xanhAvg.b);
                Assert.Greater(policeDiff, 0.15f, "Police palette should differ clearly from XanhSM cyan.");

                // ClearCache safely disposes owned textures and rebuilds cleanly on next query
                BikePaletteSwapper.ClearCache();
                Assert.AreEqual(0, BikePaletteSwapper.CachedEntryCount);

                BikeSpriteSet rebuilt = BikePaletteSwapper.GetOrCreateSet(BikePaletteKind.ShopeeFood, xanhSm);
                Assert.IsNotNull(rebuilt);
                Assert.IsNotNull(rebuilt.Upright0);
                Assert.AreEqual(1, BikePaletteSwapper.CachedEntryCount);
            }
            finally
            {
                BikePaletteSwapper.ClearCache();
            }
        }

        [Test]
        public void BikePaletteSwapper_FallsBackSafelyWhenSourceIsNullOrNotReadable()
        {
            BikePaletteSwapper.ClearCache();
            Texture2D nonReadableTex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Sprite nonReadableSprite = null;
            BikeSkinData noPoseSkin = BikeSkinData.CreateDefault(DriverBrandSkin.Grab);

            try
            {
                BikeSpriteSet nullSkinSet = BikePaletteSwapper.GetOrCreateSet((BikeSkinData)null);
                Assert.IsNotNull(nullSkinSet);
                Assert.IsNotNull(nullSkinSet.Upright0);
                Assert.IsFalse(nullSkinSet.FlipKickRight);

                BikeSpriteSet nullSourcePolice = BikePaletteSwapper.GetOrCreateSet(BikePaletteKind.Police, (BikeSkinData)null);
                Assert.IsNotNull(nullSourcePolice);
                Assert.IsNotNull(nullSourcePolice.Upright0);
                Assert.IsFalse(nullSourcePolice.FlipKickRight);

                BikeSpriteSet noPoseSet = BikePaletteSwapper.GetOrCreateSet(noPoseSkin);
                Assert.IsNotNull(noPoseSet);
                Assert.IsNotNull(noPoseSet.Upright0);
                Assert.IsFalse(noPoseSet.FlipKickRight);

                nonReadableTex.Apply(false, true);
                nonReadableSprite = Sprite.Create(nonReadableTex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 32f);

                BikeSpriteSet nonReadableFallback = BikePaletteSwapper.GetOrCreateSet(
                    BikePaletteKind.ShopeeFood,
                    nonReadableSprite,
                    null, null, null, null, null, null);
                Assert.IsNotNull(nonReadableFallback);
                Assert.IsNotNull(nonReadableFallback.Upright0);
                Assert.IsFalse(nonReadableFallback.FlipKickRight);
                Assert.AreEqual(0, BikePaletteSwapper.CachedEntryCount, "Fallback paths should not pollute swapper cache.");
            }
            finally
            {
                BikePaletteSwapper.ClearCache();
                if (nonReadableSprite != null) Object.DestroyImmediate(nonReadableSprite);
                Object.DestroyImmediate(nonReadableTex);
                Object.DestroyImmediate(noPoseSkin);
            }
        }

        [Test]
        public void Stage2ArtWiring_SkinsMapsThemesAndObstaclesAreConfiguredCorrectly()
        {
            string[] skinPaths =
            {
                "Assets/ScriptableObjects/Skins/Skin_XanhSM.asset",
                "Assets/ScriptableObjects/Skins/Skin_ShopeeFood.asset",
                "Assets/ScriptableObjects/Skins/Skin_Grab.asset",
                "Assets/ScriptableObjects/Skins/Skin_Ahamove.asset"
            };

            for (int i = 0; i < skinPaths.Length; i++)
            {
                BikeSkinData skin = AssetDatabase.LoadAssetAtPath<BikeSkinData>(skinPaths[i]);
                Assert.IsNotNull(skin, skinPaths[i]);
                Assert.IsNotNull(skin.CustomLogoSprite, $"Preview missing on {skinPaths[i]}");
                Assert.IsTrue(skin.HasMultiPoseSprites, $"Multi-pose sprites missing on {skinPaths[i]}");
                Assert.IsNull(skin.GetOverrideSprite(), $"Single customSprite/texture should stay null so multi-pose + TC-39 behavior holds: {skinPaths[i]}");
            }

            string[] mapPaths =
            {
                "Assets/ScriptableObjects/Maps/Map_01_CityMorning.asset",
                "Assets/ScriptableObjects/Maps/Map_02_CoastalHighway.asset",
                "Assets/ScriptableObjects/Maps/Map_03_NeonNightExpress.asset",
                "Assets/ScriptableObjects/Maps/Map_04_CityNightDistrict.asset"
            };

            for (int i = 0; i < mapPaths.Length; i++)
            {
                TrackDefinition map = AssetDatabase.LoadAssetAtPath<TrackDefinition>(mapPaths[i]);
                Assert.IsNotNull(map, mapPaths[i]);
                Assert.IsNotNull(map.PreviewBannerSprite, $"Banner sprite missing on {mapPaths[i]}");
            }

            string[] themePaths =
            {
                "Assets/ScriptableObjects/Maps/Theme_CityMorning.asset",
                "Assets/ScriptableObjects/Maps/Theme_CoastalHighway.asset",
                "Assets/ScriptableObjects/Maps/Theme_NeonNightExpress.asset",
                "Assets/ScriptableObjects/Maps/MapTheme_City.asset"
            };

            var rng = new System.Random(42);
            RoadsidePropKind[] expectedOverrideKinds =
            {
                RoadsidePropKind.StreetLamp,
                RoadsidePropKind.Tree,
                RoadsidePropKind.PalmTree,
                RoadsidePropKind.NeonSign,
                RoadsidePropKind.HighwaySign
            };

            for (int i = 0; i < themePaths.Length; i++)
            {
                MapTheme theme = AssetDatabase.LoadAssetAtPath<MapTheme>(themePaths[i]);
                Assert.IsNotNull(theme, themePaths[i]);
                for (int k = 0; k < expectedOverrideKinds.Length; k++)
                {
                    Assert.IsNotNull(
                        theme.PickOverrideSpriteForKind(expectedOverrideKinds[k], rng),
                        $"Expected override sprite for {expectedOverrideKinds[k]} on {themePaths[i]}");
                }
            }

            var obsGo = new GameObject("TestObstacleWiring");
            try
            {
                TrackObstacleSystem obsSys = obsGo.AddComponent<TrackObstacleSystem>();
                obsSys.SetCustomObstacleSprites(new[]
                {
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Props/Obstacle_TrafficCone.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Props/Obstacle_OilBarrel.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Props/Obstacle_RoadBarrier.png"),
                    null,
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Props/Obstacle_ParkedBus.png")
                });

                TrackObstacle cone = obsSys.AddManualObstacle(ObstacleKind.TrafficCone, 100f, 0f);
                TrackObstacle oilSlick = obsSys.AddManualObstacle(ObstacleKind.OilSlick, 150f, 0f);
                Assert.IsNotNull(cone.CustomSprite);
                Assert.IsNull(oilSlick.CustomSprite, "OilSlick slot should stay null and fall back cleanly.");
                Assert.IsNotNull(TrackObstacleSystem.GetObstacleSprite(oilSlick));
            }
            finally
            {
                Object.DestroyImmediate(obsGo);
            }
        }

        private static Color SampleCenterRegionColor(Texture2D tex)
        {
            int x0 = Mathf.RoundToInt(tex.width * 0.35f);
            int x1 = Mathf.RoundToInt(tex.width * 0.65f);
            int y0 = Mathf.RoundToInt(tex.height * 0.45f);
            int y1 = Mathf.RoundToInt(tex.height * 0.72f);

            float sumR = 0f, sumG = 0f, sumB = 0f;
            int count = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Color c = tex.GetPixel(x, y);
                    if (c.a < 0.8f) continue;
                    Color.RGBToHSV(c, out _, out float s, out float v);
                    if (s < 0.15f || v < 0.2f) continue;
                    sumR += c.r;
                    sumG += c.g;
                    sumB += c.b;
                    count++;
                }
            }

            Assert.Greater(count, 50, "Expected sufficient saturated center pixels for palette verification.");
            return new Color(sumR / count, sumG / count, sumB / count, 1f);
        }
    }
}
