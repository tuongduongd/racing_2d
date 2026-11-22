using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RacingGame.Editor
{
    /// <summary>
    /// Editor utility that binds Stage 1 sliced art assets (multi-pose bike sprites,
    /// preview thumbnails, map banners, roadside prop overrides, and obstacle sprites)
    /// into ScriptableObject assets and scenes without any runtime path hardcoding.
    /// </summary>
    public static class Stage2ArtWiringEditor
    {
        private const string BikesDir = "Assets/Art/Sprites/Bikes";
        private const string PropsDir = "Assets/Art/Sprites/Props";
        private const string PreviewsDir = "Assets/Art/Previews";
        private const string MapsArtDir = "Assets/Art/Maps";

        private const string SkinsDir = "Assets/ScriptableObjects/Skins";
        private const string MapsDataDir = "Assets/ScriptableObjects/Maps";

        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        private static readonly (string assetName, string previewName)[] SkinAssetMap =
        {
            ("Skin_XanhSM.asset", "Preview_XanhSM.png"),
            ("Skin_ShopeeFood.asset", "Preview_ShopeeFood.png"),
            ("Skin_Grab.asset", "Preview_Grab.png"),
            ("Skin_Ahamove.asset", "Preview_Ahamove.png")
        };

        private static readonly (string assetName, string bannerName)[] MapBannerMap =
        {
            ("Map_01_CityMorning.asset", "Banner_Map01_CityMorning.png"),
            ("Map_02_CoastalHighway.asset", "Banner_Map02_CoastalHighway.png"),
            ("Map_03_NeonNightExpress.asset", "Banner_Map03_NeonNightExpress.png"),
            ("Map_04_CityNightDistrict.asset", "Banner_Map04_CityNightDistrict.png")
        };

        private static readonly string[] ThemeAssetNames =
        {
            "MapTheme_Valley.asset",
            "MapTheme_City.asset",
            "Theme_CityMorning.asset",
            "Theme_CoastalHighway.asset",
            "Theme_NeonNightExpress.asset"
        };

        [MenuItem("Tools/Road Rash/Wire Stage 2 Art Assets To Data & Scenes")]
        public static void WireAllStage2ArtAssets()
        {
            BikeSkinData referenceSkin = WireDataAssetsOnly();
            Sprite[] obstacleSprites = LoadObstacleSpriteArray();

            WireScene(LobbyScenePath, referenceSkin, obstacleSprites);
            WireScene(SampleScenePath, referenceSkin, obstacleSprites);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Stage2ArtWiringEditor] Wired Stage 2 sprites, banners, themes, and scenes.");
        }

        public static BikeSkinData WireDataAssetsOnly()
        {
            Sprite upright = LoadRequiredSprite($"{BikesDir}/Bike_XanhSM_Upright.png");
            Sprite leanLeftLight = LoadRequiredSprite($"{BikesDir}/Bike_XanhSM_LeanLeftLight.png");
            Sprite leanLeftDeep = LoadRequiredSprite($"{BikesDir}/Bike_XanhSM_LeanLeftDeep.png");
            Sprite leanRightLight = LoadRequiredSprite($"{BikesDir}/Bike_XanhSM_LeanRightLight.png");
            Sprite leanRightDeep = LoadRequiredSprite($"{BikesDir}/Bike_XanhSM_LeanRightDeep.png");
            Sprite kickLeft = LoadRequiredSprite($"{BikesDir}/Bike_XanhSM_KickLeft.png");

            BikeSkinData referenceSkin = null;

            for (int i = 0; i < SkinAssetMap.Length; i++)
            {
                string skinPath = $"{SkinsDir}/{SkinAssetMap[i].assetName}";
                BikeSkinData skin = AssetDatabase.LoadAssetAtPath<BikeSkinData>(skinPath);
                if (skin == null)
                {
                    Debug.LogWarning($"[Stage2ArtWiringEditor] Missing skin asset: {skinPath}");
                    continue;
                }

                Sprite preview = LoadRequiredSprite($"{PreviewsDir}/{SkinAssetMap[i].previewName}");
                SerializedObject so = new SerializedObject(skin);
                so.FindProperty("customLogoSprite").objectReferenceValue = preview;
                so.FindProperty("customSprite").objectReferenceValue = null;
                so.FindProperty("customTexture").objectReferenceValue = null;
                so.FindProperty("uprightSprite").objectReferenceValue = upright;
                so.FindProperty("leanLeft1Sprite").objectReferenceValue = leanLeftLight;
                so.FindProperty("leanLeft2Sprite").objectReferenceValue = leanLeftDeep;
                so.FindProperty("leanRight1Sprite").objectReferenceValue = leanRightLight;
                so.FindProperty("leanRight2Sprite").objectReferenceValue = leanRightDeep;
                so.FindProperty("kickLeftSprite").objectReferenceValue = kickLeft;
                so.FindProperty("hitStaggerSprite").objectReferenceValue = leanRightDeep;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(skin);

                if (skin.SkinId == DriverBrandSkin.XanhSM)
                {
                    referenceSkin = skin;
                }
            }

            for (int i = 0; i < MapBannerMap.Length; i++)
            {
                string mapPath = $"{MapsDataDir}/{MapBannerMap[i].assetName}";
                TrackDefinition map = AssetDatabase.LoadAssetAtPath<TrackDefinition>(mapPath);
                if (map == null)
                {
                    Debug.LogWarning($"[Stage2ArtWiringEditor] Missing map asset: {mapPath}");
                    continue;
                }

                Sprite banner = LoadRequiredSprite($"{MapsArtDir}/{MapBannerMap[i].bannerName}");
                SerializedObject so = new SerializedObject(map);
                so.FindProperty("previewBannerSprite").objectReferenceValue = banner;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(map);
            }

            PropSpriteOverride[] propOverrides = BuildDefaultPropOverrides();
            for (int i = 0; i < ThemeAssetNames.Length; i++)
            {
                string themePath = $"{MapsDataDir}/{ThemeAssetNames[i]}";
                MapTheme theme = AssetDatabase.LoadAssetAtPath<MapTheme>(themePath);
                if (theme == null)
                {
                    Debug.LogWarning($"[Stage2ArtWiringEditor] Missing theme asset: {themePath}");
                    continue;
                }

                theme.SetPropSpriteOverrides(propOverrides);
                EditorUtility.SetDirty(theme);
            }

            AssetDatabase.SaveAssets();
            return referenceSkin;
        }

        public static void WireSceneObjects(RoadRashRenderer2D renderer, TrackObstacleSystem obstacleSystem, BikeSkinData referenceSkin, Sprite[] obstacleSprites = null)
        {
            if (renderer != null && referenceSkin != null)
            {
                SerializedObject rSo = new SerializedObject(renderer);
                rSo.FindProperty("referenceMultiPoseSkin").objectReferenceValue = referenceSkin;
                rSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }

            if (obstacleSystem != null)
            {
                Sprite[] sprites = obstacleSprites ?? LoadObstacleSpriteArray();
                SerializedObject oSo = new SerializedObject(obstacleSystem);
                SerializedProperty arrProp = oSo.FindProperty("customObstacleSprites");
                arrProp.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                {
                    arrProp.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                }
                oSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obstacleSystem);
            }
        }

        public static Sprite[] LoadObstacleSpriteArray()
        {
            return new[]
            {
                LoadRequiredSprite($"{PropsDir}/Obstacle_TrafficCone.png"), // ObstacleKind.TrafficCone = 0
                LoadRequiredSprite($"{PropsDir}/Obstacle_OilBarrel.png"),   // ObstacleKind.OilBarrel = 1
                LoadRequiredSprite($"{PropsDir}/Obstacle_RoadBarrier.png"), // ObstacleKind.RoadBarrier = 2
                null,                                                       // ObstacleKind.OilSlick = 3 (procedural puddle)
                LoadRequiredSprite($"{PropsDir}/Obstacle_ParkedBus.png")    // ObstacleKind.ParkedBus = 4
            };
        }

        private static PropSpriteOverride[] BuildDefaultPropOverrides()
        {
            Sprite streetLamp = LoadRequiredSprite($"{PropsDir}/Prop_StreetLamp.png");
            Sprite tree = LoadRequiredSprite($"{PropsDir}/Prop_Tree.png");
            Sprite palmTree = LoadRequiredSprite($"{PropsDir}/Prop_PalmTree.png");
            Sprite highwaySign = LoadRequiredSprite($"{PropsDir}/Prop_HighwaySign.png");
            Sprite neonPho = LoadRequiredSprite($"{PropsDir}/Prop_NeonSign_PhoDem.png");
            Sprite neonCafe = LoadRequiredSprite($"{PropsDir}/Prop_NeonSign_CaPhe.png");

            var list = new List<PropSpriteOverride>(8)
            {
                new PropSpriteOverride { kind = RoadsidePropKind.StreetLamp, sprite = streetLamp },
                new PropSpriteOverride { kind = RoadsidePropKind.Tree, sprite = tree },
                new PropSpriteOverride { kind = RoadsidePropKind.PalmTree, sprite = palmTree },
                new PropSpriteOverride { kind = RoadsidePropKind.HighwaySign, sprite = highwaySign },
                new PropSpriteOverride { kind = RoadsidePropKind.NeonSign, sprite = neonPho },
                new PropSpriteOverride { kind = RoadsidePropKind.NeonSign, sprite = neonCafe },
                new PropSpriteOverride { kind = RoadsidePropKind.Billboard, sprite = neonPho },
                new PropSpriteOverride { kind = RoadsidePropKind.Billboard, sprite = neonCafe }
            };
            return list.ToArray();
        }

        private static void WireScene(string scenePath, BikeSkinData referenceSkin, Sprite[] obstacleSprites)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"[Stage2ArtWiringEditor] Scene not found: {scenePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RoadRashRenderer2D renderer = Object.FindAnyObjectByType<RoadRashRenderer2D>();
            TrackObstacleSystem obstacleSystem = Object.FindAnyObjectByType<TrackObstacleSystem>();
            WireSceneObjects(renderer, obstacleSystem, referenceSkin, obstacleSprites);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Sprite LoadRequiredSprite(string assetPath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new System.InvalidOperationException($"[Stage2ArtWiringEditor] Missing required Sprite asset at '{assetPath}'. Run Stage 1 import first.");
            }
            return sprite;
        }
    }
}
