using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class CityMapTests
    {
        private readonly List<Object> _cleanupObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _cleanupObjects.Count - 1; i >= 0; i--)
            {
                if (_cleanupObjects[i] != null)
                {
                    Object.DestroyImmediate(_cleanupObjects[i]);
                }
            }
            _cleanupObjects.Clear();
        }

        private T TrackCleanup<T>(T obj) where T : Object
        {
            _cleanupObjects.Add(obj);
            return obj;
        }

        [Test]
        public void MapTheme_City_HasNightNeonStyleAndAllUrbanPropSpritesExist()
        {
            MapTheme cityTheme = TrackCleanup(MapTheme.CreateCityDistrictPreset());

            Assert.AreEqual("city_district", cityTheme.ThemeId);
            Assert.AreEqual("MapTheme_City", cityTheme.ThemeName);
            Assert.IsTrue(cityTheme.ShowStars);
            Assert.IsFalse(cityTheme.ShowSun);
            Assert.AreEqual(HorizonSilhouetteStyle.NeonCitySkyline, cityTheme.HorizonStyle);
            Assert.IsNotNull(cityTheme.Zones);
            Assert.GreaterOrEqual(cityTheme.Zones.Length, 3);

            var foundProps = new HashSet<RoadsidePropKind>();
            for (int i = 0; i < cityTheme.Zones.Length; i++)
            {
                MapZonePropConfig zone = cityTheme.Zones[i];
                if (zone.leftProps != null)
                {
                    for (int j = 0; j < zone.leftProps.Length; j++) foundProps.Add(zone.leftProps[j]);
                }
                if (zone.rightProps != null)
                {
                    for (int j = 0; j < zone.rightProps.Length; j++) foundProps.Add(zone.rightProps[j]);
                }
            }

            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.SkyscraperTower));
            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.TrafficLight));
            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.NeonSign));
            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.CityBuilding));
            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.HighwaySign));
            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.BusStop));
            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.FireHydrant));
            Assert.IsTrue(foundProps.Contains(RoadsidePropKind.VendingMachine));

            RoadsidePropKind[] urbanKinds =
            {
                RoadsidePropKind.SkyscraperTower,
                RoadsidePropKind.TrafficLight,
                RoadsidePropKind.NeonSign,
                RoadsidePropKind.ParkedBus,
                RoadsidePropKind.CityBuilding,
                RoadsidePropKind.HighwaySign,
                RoadsidePropKind.BusStop,
                RoadsidePropKind.FireHydrant,
                RoadsidePropKind.VendingMachine
            };
            for (int i = 0; i < urbanKinds.Length; i++)
            {
                Sprite s = RoadRashPropSprites.GetPropSprite(urbanKinds[i]);
                Assert.IsNotNull(s, $"Missing sprite for {urbanKinds[i]}");
                Assert.AreEqual(FilterMode.Point, s.texture.filterMode, $"Expected Point filtering on {urbanKinds[i]}");
                Assert.AreEqual(0.5f, s.pivot.x / s.rect.width, 0.01f, $"Expected bottom-center X pivot on {urbanKinds[i]}");
                Assert.AreEqual(0f, s.pivot.y / s.rect.height, 0.01f, $"Expected bottom-center Y pivot on {urbanKinds[i]}");
            }
        }

        [Test]
        public void TrackDefinition_City_AssetAndPreset_Have25To30WaypointsAnd1500To2000mLength()
        {
            MapTheme cityTheme = TrackCleanup(MapTheme.CreateCityPreset());
            TrackDefinition cityDef = TrackCleanup(TrackDefinition.CreateCityPreset(cityTheme));

            Assert.IsNotNull(cityDef.Waypoints);
            Assert.GreaterOrEqual(cityDef.Waypoints.Length, 25);
            Assert.LessOrEqual(cityDef.Waypoints.Length, 30);

            var trackGo = TrackCleanup(new GameObject("CityLengthTrack"));
            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();
            track.ApplyTrackDefinition(cityDef);

            Assert.GreaterOrEqual(track.WaypointCount, 25);
            Assert.LessOrEqual(track.WaypointCount, 30);
            Assert.GreaterOrEqual(track.TotalLength, 1500f);
            Assert.LessOrEqual(track.TotalLength, 2000f);

#if UNITY_EDITOR
            MapTheme themeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<MapTheme>("Assets/ScriptableObjects/Maps/MapTheme_City.asset");
            TrackDefinition trackAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TrackDefinition>("Assets/ScriptableObjects/Maps/TrackDefinition_City.asset");
            Assert.IsNotNull(themeAsset, "Assets/ScriptableObjects/Maps/MapTheme_City.asset must exist.");
            Assert.IsNotNull(trackAsset, "Assets/ScriptableObjects/Maps/TrackDefinition_City.asset must exist.");
            Assert.AreEqual(themeAsset, trackAsset.Theme, "TrackDefinition_City.asset must reference MapTheme_City.asset.");
            Assert.GreaterOrEqual(trackAsset.Waypoints.Length, 25);
            Assert.LessOrEqual(trackAsset.Waypoints.Length, 30);

            track.ApplyTrackDefinition(trackAsset);
            Assert.GreaterOrEqual(track.TotalLength, 1500f);
            Assert.LessOrEqual(track.TotalLength, 2000f);
#endif
        }

        [Test]
        public void CityDistrictTrack_HasSharpIntersections_OverpassElevation_TunnelAndCrosswalks_Across4And6Lanes()
        {
            MapTheme cityTheme = TrackCleanup(MapTheme.CreateCityDistrictPreset());
            TrackDefinition cityDef = TrackCleanup(TrackDefinition.CreateCityDistrictPreset(cityTheme));

            Assert.AreEqual("city_district", cityDef.MapId);
            Assert.IsTrue(cityDef.UnlockedByDefault);
            Assert.AreEqual(4, cityDef.LaneCount);
            Assert.AreEqual(4.0f, cityDef.LaneWidth, 0.001f);
            Assert.AreEqual(16.0f, cityDef.TotalTrackWidth, 0.001f);
            Assert.IsNotNull(cityDef.FeatureZones);

            int overpassCount = 0;
            int tunnelCount = 0;
            int crosswalkCount = 0;
            TrackFeatureZone overpassZone = null;
            TrackFeatureZone tunnelZone = null;

            for (int i = 0; i < cityDef.FeatureZones.Length; i++)
            {
                TrackFeatureZone zone = cityDef.FeatureZones[i];
                if (zone.kind == TrackFeatureKind.Overpass)
                {
                    overpassCount++;
                    overpassZone = zone;
                }
                else if (zone.kind == TrackFeatureKind.Tunnel)
                {
                    tunnelCount++;
                    tunnelZone = zone;
                }
                else if (zone.kind == TrackFeatureKind.Crosswalk)
                {
                    crosswalkCount++;
                }
            }

            Assert.AreEqual(1, overpassCount);
            Assert.AreEqual(1, tunnelCount);
            Assert.AreEqual(3, crosswalkCount);

            var trackGo = TrackCleanup(new GameObject("CityTrack"));
            WaypointTrack track = trackGo.AddComponent<WaypointTrack>();
            track.ApplyTrackDefinition(cityDef);

            Assert.AreEqual(4, track.LaneCount);
            Assert.AreEqual(16.0f, track.TrackWidth, 0.001f);
            Assert.Greater(track.TotalLength, 400f);

            // Check sharp urban intersection corners (90° intersections and sharp zig-zag bends >= 44°)
            int sharpCornerCount = 0;
            int wpCount = track.WaypointCount;
            for (int i = 0; i < wpCount; i++)
            {
                Vector2 dirIn = track.GetTrackDirection(i - 1);
                Vector2 dirOut = track.GetTrackDirection(i);
                float turnAngle = Vector2.Angle(dirIn, dirOut);
                if (turnAngle >= 44f)
                {
                    sharpCornerCount++;
                }
            }
            Assert.GreaterOrEqual(sharpCornerCount, 4, "City district circuit should include multiple sharp urban intersections.");

            // Verify Overpass elevation peak vs its entry edge
            float overpassMidS = ((overpassZone.startNormalized + overpassZone.endNormalized) * 0.5f) * track.TotalLength;
            float overpassStartS = overpassZone.startNormalized * track.TotalLength;
            TrackSample midSample = track.SampleAtDistance(overpassMidS);
            TrackSample startSample = track.SampleAtDistance(overpassStartS);
            Assert.Greater(midSample.elevation - startSample.elevation, 4.0f);

            Assert.IsTrue(track.TryGetFeatureAtDistance(overpassMidS, out TrackFeatureKind detectedOverpass, out float overpassProgress));
            Assert.AreEqual(TrackFeatureKind.Overpass, detectedOverpass);
            Assert.AreEqual(0.5f, overpassProgress, 0.05f);

            float tunnelMidS = ((tunnelZone.startNormalized + tunnelZone.endNormalized) * 0.5f) * track.TotalLength;
            Assert.IsTrue(track.TryGetFeatureAtDistance(tunnelMidS, out TrackFeatureKind detectedTunnel, out _));
            Assert.AreEqual(TrackFeatureKind.Tunnel, detectedTunnel);

            // Verify 6-lane urban highway scaling preserves feature zones & increases TrackWidth cleanly
            TrackDefinition wideCity = TrackCleanup(TrackDefinition.CreateRuntime(
                "city_district_6lane",
                "Thành Phố 6 Làn",
                cityDef.Waypoints,
                6,
                4.0f,
                cityTheme,
                4,
                1.2f,
                18,
                cityDef.FeatureZones));
            track.ApplyTrackDefinition(wideCity);
            Assert.AreEqual(6, track.LaneCount);
            Assert.AreEqual(24.0f, track.TrackWidth, 0.001f);
            Assert.IsTrue(track.TryGetFeatureAtDistance(overpassMidS, out TrackFeatureKind wideOverpass, out _));
            Assert.AreEqual(TrackFeatureKind.Overpass, wideOverpass);
        }

        [Test]
        public void CityDistrict_EnvironmentDecorator_SkipsTunnelAndPlacesCrosswalkSignals()
        {
            MapTheme cityTheme = TrackCleanup(MapTheme.CreateCityDistrictPreset());
            TrackDefinition cityDef = TrackCleanup(TrackDefinition.CreateCityDistrictPreset(cityTheme));

            var host = TrackCleanup(new GameObject("CityEnvHost"));
            WaypointTrack track = host.AddComponent<WaypointTrack>();
            track.ApplyTrackDefinition(cityDef);

            EnvironmentDecorator env = host.AddComponent<EnvironmentDecorator>();
            env.RebuildEnvironment(track, cityTheme, 2026);

            Assert.Greater(env.Decorations.Count, 20);

            int crosswalkApproachSignals = 0;
            bool hasSkyscraper = false;

            for (int i = 0; i < env.Decorations.Count; i++)
            {
                RoadsideDecoration deco = env.Decorations[i];
                bool hasFeature = track.TryGetFeatureAtDistance(deco.TrackDistance, out TrackFeatureKind kind, out _);
                if (hasFeature)
                {
                    Assert.AreNotEqual(TrackFeatureKind.Tunnel, kind, "Outdoor props must not spawn inside the Tunnel.");
                }

                if (deco.Kind == RoadsidePropKind.SkyscraperTower)
                {
                    hasSkyscraper = true;
                }

                if ((hasFeature && kind == TrackFeatureKind.Crosswalk) &&
                    (deco.Kind == RoadsidePropKind.TrafficLight || deco.Kind == RoadsidePropKind.NeonSign))
                {
                    crosswalkApproachSignals++;
                }
            }

            Assert.IsTrue(hasSkyscraper, "City environment should include SkyscraperTower props.");
            Assert.Greater(crosswalkApproachSignals, 0, "Crosswalk approaches should place TrafficLight or NeonSign props.");
        }

        [Test]
        public void CityDistrict_ObstacleSystem_SpawnsParkedBusOnOutermostLaneOnly_NeverInTunnelOrOverpass()
        {
            MapTheme cityTheme = TrackCleanup(MapTheme.CreateCityDistrictPreset());
            TrackDefinition cityDef = TrackCleanup(TrackDefinition.CreateCityDistrictPreset(cityTheme));

            var host = TrackCleanup(new GameObject("CityObstacleHost"));
            WaypointTrack track = host.AddComponent<WaypointTrack>();
            track.ApplyTrackDefinition(cityDef);

            TrackObstacleSystem obsSystem = host.AddComponent<TrackObstacleSystem>();
            obsSystem.ApplyMapSettings(cityDef);
            obsSystem.SpawnObstacles(track, 2026);

            Assert.Greater(obsSystem.Obstacles.Count, 5);

            float outerLaneAbsOffset = Mathf.Abs(track.GetLaneCenterOffset(0));
            float halfTrack = track.TrackWidth * 0.5f;
            int parkedBusCount = 0;

            for (int i = 0; i < obsSystem.Obstacles.Count; i++)
            {
                TrackObstacle obs = obsSystem.Obstacles[i];
                Assert.LessOrEqual(Mathf.Abs(obs.LateralOffset), halfTrack, "Obstacle must stay inside asphalt width.");

                if (obs.Kind == ObstacleKind.ParkedBus)
                {
                    parkedBusCount++;
                    Assert.GreaterOrEqual(Mathf.Abs(obs.LateralOffset), outerLaneAbsOffset - 0.1f,
                        "ParkedBus must be parked near the outermost curb lane.");

                    if (track.TryGetFeatureAtDistance(obs.TrackDistance, out TrackFeatureKind kind, out _))
                    {
                        Assert.AreNotEqual(TrackFeatureKind.Tunnel, kind, "ParkedBus must not spawn inside Tunnel.");
                        Assert.AreNotEqual(TrackFeatureKind.Overpass, kind, "ParkedBus must not spawn on Overpass.");
                    }
                }
            }

            Assert.GreaterOrEqual(parkedBusCount, 1, "City District obstacle spawn should include at least one ParkedBus.");
        }
    }
}
