using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Concrete combat and inter-vehicle collision resolver extracted from GameManager (SRP).
    /// </summary>
    public sealed class RaceCombatSystem : IRaceCombatService
    {
        public void ResolveVehicleCollisions(
            IReadOnlyList<Car> allCars,
            float bikeCollisionRadius,
            float collisionRestitution,
            float collisionFriction,
            Car playerCar,
            IAudioService audioService,
            Action<Car, Car, Vector2> onRacersCollided)
        {
            if (allCars == null) return;

            for (int i = 0; i < allCars.Count; i++)
            {
                Car a = allCars[i];
                if (a == null) continue;

                for (int j = i + 1; j < allCars.Count; j++)
                {
                    Car b = allCars[j];
                    if (b == null) continue;

                    VehicleCollisionResult res = VehicleCollisionPhysics.Resolve(
                        a.Physics.Position, a.Physics.Velocity, a.Physics.Mass, bikeCollisionRadius,
                        b.Physics.Position, b.Physics.Velocity, b.Physics.Mass, bikeCollisionRadius,
                        collisionRestitution, collisionFriction);

                    if (!res.Collided) continue;

                    a.ApplyPhysicalCollisionImpact(
                        res.PositionDeltaA, res.VelocityDeltaA, res.AngularVelocityDeltaA, res.VerticalLaunchVelA);
                    b.ApplyPhysicalCollisionImpact(
                        res.PositionDeltaB, res.VelocityDeltaB, res.AngularVelocityDeltaB, res.VerticalLaunchVelB);

                    if (res.NormalImpulse > 120f)
                    {
                        onRacersCollided?.Invoke(a, b, res.ContactPoint);

                        if ((a == playerCar || b == playerCar) && audioService != null)
                        {
                            audioService.PlayCollision();
                        }
                    }
                }
            }
        }

        public void ResolveKickAttacks(
            IReadOnlyList<Car> allCars,
            float kickReachForward,
            float kickReachLateral,
            float kickLateralImpulse,
            Car playerCar,
            IAudioService audioService,
            Action<Car, Car, Vector2> onRacerKicked)
        {
            if (allCars == null) return;

            for (int i = 0; i < allCars.Count; i++)
            {
                Car attacker = allCars[i];
                Car hitTarget = TryResolveKickHit(
                    attacker, allCars, kickReachForward, kickReachLateral, kickLateralImpulse, out Vector2 hitPos);
                if (hitTarget != null)
                {
                    onRacerKicked?.Invoke(attacker, hitTarget, hitPos);

                    if ((attacker == playerCar || hitTarget == playerCar) && audioService != null)
                    {
                        audioService.PlayKick(true);
                    }
                }
            }
        }

        public int FindBestKickDirection(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            ref int fallbackSide)
        {
            return FindBestKickDirectionStatic(attacker, allCars, forwardReach, lateralReach, ref fallbackSide);
        }

        public Car TryResolveKickHit(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            float lateralImpulse,
            out Vector2 hitPos)
        {
            return TryResolveKickHitStatic(attacker, allCars, forwardReach, lateralReach, lateralImpulse, out hitPos);
        }

        public static int FindBestKickDirectionStatic(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            ref int fallbackSide)
        {
            if (attacker == null || allCars == null)
            {
                fallbackSide = -fallbackSide;
                return fallbackSide;
            }

            Vector2 fwd = attacker.Physics.Forward;
            Vector2 right = attacker.Physics.Right;
            float bestDistSq = float.MaxValue;
            int bestSide = 0;

            for (int i = 0; i < allCars.Count; i++)
            {
                Car target = allCars[i];
                if (target == null || target == attacker) continue;

                Vector2 rel = target.Physics.Position - attacker.Physics.Position;
                float localFwd = Vector2.Dot(rel, fwd);
                float localLat = Vector2.Dot(rel, right);

                if (Mathf.Abs(localFwd) <= forwardReach + 0.8f && Mathf.Abs(localLat) <= lateralReach + 1.0f)
                {
                    float d2 = rel.sqrMagnitude;
                    if (d2 < bestDistSq)
                    {
                        bestDistSq = d2;
                        bestSide = localLat >= 0f ? 1 : -1;
                    }
                }
            }

            if (bestSide != 0) return bestSide;
            if (Mathf.Abs(attacker.LastInput.steering) > 0.15f)
            {
                return attacker.LastInput.steering > 0f ? 1 : -1;
            }

            fallbackSide = -fallbackSide;
            return fallbackSide;
        }

        public static Car TryResolveKickHitStatic(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            float lateralImpulse,
            out Vector2 hitPos)
        {
            hitPos = Vector2.zero;
            if (attacker == null || allCars == null || !attacker.IsKicking || attacker.HasKickHitThisSwing) return null;
            if (attacker.KickProgress < 0.16f || attacker.KickProgress > 0.88f) return null;

            Vector2 fwd = attacker.Physics.Forward;
            Vector2 right = attacker.Physics.Right;
            Car bestTarget = null;
            float bestDistSq = float.MaxValue;

            for (int j = 0; j < allCars.Count; j++)
            {
                Car target = allCars[j];
                if (target == null || target == attacker) continue;

                Vector2 rel = target.Physics.Position - attacker.Physics.Position;
                float localFwd = Vector2.Dot(rel, fwd);
                float sideLat = Vector2.Dot(rel, right) * attacker.KickDirection;

                if (Mathf.Abs(localFwd) <= forwardReach && sideLat >= -0.45f && sideLat <= lateralReach)
                {
                    float d2 = rel.sqrMagnitude;
                    if (d2 < bestDistSq)
                    {
                        bestDistSq = d2;
                        bestTarget = target;
                    }
                }
            }

            if (bestTarget == null) return null;

            Vector2 impulse = right * (attacker.KickDirection * lateralImpulse);
            bestTarget.ReceiveKick(attacker.KickDirection, impulse, 0.45f);
            attacker.ApplyCollisionPush(Vector2.zero, -right * (attacker.KickDirection * 2.4f));
            attacker.MarkKickHit();

            hitPos = (attacker.Physics.Position + bestTarget.Physics.Position) * 0.5f;
            return bestTarget;
        }
    }
}
