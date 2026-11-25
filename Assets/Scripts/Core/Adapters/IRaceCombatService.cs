using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Service boundary extracting vehicle-to-vehicle Newtonian collision and kick combat from GameManager (SRP/DIP).
    /// </summary>
    public interface IRaceCombatService
    {
        void ResolveVehicleCollisions(
            IReadOnlyList<Car> allCars,
            float bikeCollisionRadius,
            float collisionRestitution,
            float collisionFriction,
            Car playerCar,
            IAudioService audioService,
            Action<Car, Car, Vector2> onRacersCollided);

        void ResolveKickAttacks(
            IReadOnlyList<Car> allCars,
            float kickReachForward,
            float kickReachLateral,
            float kickLateralImpulse,
            Car playerCar,
            IAudioService audioService,
            Action<Car, Car, Vector2> onRacerKicked);

        int FindBestKickDirection(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            ref int fallbackSide);

        Car TryResolveKickHit(
            Car attacker,
            IReadOnlyList<Car> allCars,
            float forwardReach,
            float lateralReach,
            float lateralImpulse,
            out Vector2 hitPos);
    }
}
