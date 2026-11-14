using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class Loop8KickAndGraphicsTests
    {
        private class StubDriver : Driver
        {
            public VehicleInput NextInput;
            public override VehicleInput DecideInput(Car car, WaypointTrack track) => NextInput;
        }

        [Test]
        public void Car_KickStartsEnforcesCooldownAndReceiveKickStaggers()
        {
            var trackGo = new GameObject("TestTrack");
            var carGo = new GameObject("TestCar");
            var driverGo = new GameObject("TestDriver");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();
                var car = carGo.AddComponent<Car>();
                var driver = driverGo.AddComponent<StubDriver>();
                driver.NextInput = new VehicleInput(1f, 0f, 0f, false);

                car.Initialize(driver, track.GetWaypoint(0), 0f, track);
                Assert.IsTrue(car.CanKick);

                Assert.IsTrue(car.TryStartKick(1, 0.30f, 0.55f));
                Assert.IsTrue(car.IsKicking);
                Assert.AreEqual(1, car.KickDirection);
                Assert.IsFalse(car.CanKick, "Cannot start a second kick while active/cooling down.");

                for (int i = 0; i < 40; i++)
                {
                    car.StepPhysics(1f / 60f, track);
                }

                Assert.IsFalse(car.IsKicking, "Kick animation should finish within ~0.30s.");
                Assert.IsTrue(car.CanKick, "Cooldown should expire after ~0.55s.");

                float speedBefore = car.Physics.Speed;
                car.ReceiveKick(1, new Vector2(0f, -10f), 0.45f);
                Assert.IsTrue(car.IsStaggered);
                Assert.Less(car.Physics.Speed, speedBefore + 15f);
            }
            finally
            {
                Object.DestroyImmediate(trackGo);
                Object.DestroyImmediate(carGo);
                Object.DestroyImmediate(driverGo);
            }
        }

        [Test]
        public void VisualGenerators_CreateBikePoseSpritesPropsAndMinimap()
        {
            BikeSpriteSet set = RoadRashBikeSprites.GetOrCreateSet(RoadRashVisualTheme.PlayerJacketBlue);
            Assert.IsNotNull(set);
            Assert.IsNotNull(set.Upright0);
            Assert.IsNotNull(set.Upright1);
            Assert.IsNotNull(set.LeanLeft1);
            Assert.IsNotNull(set.LeanLeft2);
            Assert.IsNotNull(set.LeanRight1);
            Assert.IsNotNull(set.LeanRight2);
            Assert.IsNotNull(set.KickLeft);
            Assert.IsNotNull(set.KickRight);
            Assert.IsNotNull(set.HitStagger);
            Assert.IsNotNull(RoadRashBikeSprites.GetShadowSprite());
            Assert.IsNotNull(RoadRashBikeSprites.GetTailLightSprite());

            foreach (RoadsidePropKind kind in System.Enum.GetValues(typeof(RoadsidePropKind)))
            {
                Assert.IsNotNull(RoadRashPropSprites.GetPropSprite(kind), $"Prop sprite missing for {kind}");
            }

            Assert.IsNotNull(RoadRashUiSprites.GetDashboardSprite());
            Assert.IsNotNull(RoadRashUiSprites.GetGaugeDialSprite());
            Assert.IsNotNull(RoadRashUiSprites.GetNeedleSprite());

            var trackGo = new GameObject("MapTrack");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();
                Texture2D map = RoadRashUiSprites.BuildTrackMinimapTexture(track, 128, out _, out float maxDim);
                Assert.IsNotNull(map);
                Assert.AreEqual(128, map.width);
                Assert.Greater(maxDim, 10f);
                Object.DestroyImmediate(map);
            }
            finally
            {
                Object.DestroyImmediate(trackGo);
            }
        }

        [Test]
        public void GameManager_KickCombatTargetsNearestSideAndKnocksOpponentSideways()
        {
            var trackGo = new GameObject("TestTrack");
            var playerGo = new GameObject("Player");
            var aiGo = new GameObject("Opponent");
            var drv1Go = new GameObject("Drv1");
            var drv2Go = new GameObject("Drv2");
            try
            {
                var track = trackGo.AddComponent<WaypointTrack>();
                track.BuildDefaultCircuit();

                var player = playerGo.AddComponent<Car>();
                var ai = aiGo.AddComponent<Car>();
                var drv1 = drv1Go.AddComponent<StubDriver>();
                var drv2 = drv2Go.AddComponent<StubDriver>();
                drv1.NextInput = new VehicleInput(1f, 0f, 0f, false);
                drv2.NextInput = new VehicleInput(1f, 0f, 0f, false);

                // Heading = 0 (+X forward, -Y right): place opponent 2.2m to the right (-Y)
                Vector2 pStart = track.GetWaypoint(0);
                player.Initialize(drv1, pStart, 0f, track);
                ai.Initialize(drv2, pStart + new Vector2(0.5f, -2.2f), 0f, track);

                var allCars = new[] { player, ai };
                int fallback = -1;
                int chosenDir = GameManager.FindBestKickDirection(player, allCars, 3.5f, 3.6f, ref fallback);
                Assert.AreEqual(1, chosenDir, "Should auto-aim kick to the right (+1) where opponent rides.");

                Assert.IsTrue(player.TryStartKick(chosenDir, 0.34f, 0.58f));
                // Advance into active kick hit window
                for (int i = 0; i < 8; i++)
                {
                    player.StepPhysics(1f / 60f, track);
                }

                Car hit = GameManager.TryResolveKickHit(player, allCars, 3.5f, 3.6f, 14f, out _);
                Assert.AreSame(ai, hit, "Kick within reach should hit opponent.");
                Assert.IsTrue(ai.IsStaggered);
                Assert.Less(ai.Physics.Velocity.y, -5f, "Opponent should receive lateral knockback impulse.");

                // Same swing must not hit twice
                Car secondHit = GameManager.TryResolveKickHit(player, allCars, 3.5f, 3.6f, 14f, out _);
                Assert.IsNull(secondHit);
            }
            finally
            {
                Object.DestroyImmediate(trackGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(aiGo);
                Object.DestroyImmediate(drv1Go);
                Object.DestroyImmediate(drv2Go);
            }
        }

        [Test]
        public void PropSprites_RegenerateWhenDestroyedWithoutDomainReload()
        {
            foreach (RoadsidePropKind kind in System.Enum.GetValues(typeof(RoadsidePropKind)))
            {
                Sprite first = RoadRashPropSprites.GetPropSprite(kind);
                Assert.IsTrue(first != null, $"Initial sprite missing for {kind}");

                Object.DestroyImmediate(first);
                Assert.IsTrue(first == null, $"Destroyed Unity Sprite should evaluate equal to null for {kind}");

                Sprite regenerated = RoadRashPropSprites.GetPropSprite(kind);
                Assert.IsTrue(regenerated != null, $"Regenerated sprite missing for {kind}");
                Assert.IsTrue(regenerated.texture != null, $"Regenerated texture missing for {kind}");
            }
        }
    }
}
