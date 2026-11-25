using System;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Shared baseline vehicle physics parameters used as the single source of truth
    /// by both VehiclePhysics and CarUpgradeSystem.
    /// </summary>
    [Serializable]
    public struct VehicleStats
    {
        [Min(50f)] public float mass;
        [Min(500f)] public float enginePower;
        [Min(500f)] public float brakePower;
        [Min(1f)] public float grip;
        [Min(0.05f)] public float drag;
        [Min(0f)] public float rollingResistance;
        [Min(10f)] public float maxSpeed;
        [Min(1f)] public float maxReverseSpeed;
        [Min(0.5f)] public float turnRate;

        public static VehicleStats Default => new VehicleStats
        {
            mass = 220f,
            enginePower = 4800f,
            brakePower = 7800f,
            grip = 9.2f,
            drag = 0.48f,
            rollingResistance = 0.8f,
            maxSpeed = 48f,
            maxReverseSpeed = 8f,
            turnRate = 2.4f
        };

        /// <summary>
        /// Returns a copy with all parameters clamped to safe physical minimums.
        /// </summary>
        public VehicleStats Sanitize()
        {
            VehicleStats safe = this;
            safe.mass = CoreMath.Max(50f, safe.mass);
            safe.enginePower = CoreMath.Max(500f, safe.enginePower);
            safe.brakePower = CoreMath.Max(500f, safe.brakePower);
            safe.grip = CoreMath.Max(1f, safe.grip);
            safe.drag = CoreMath.Max(0.05f, safe.drag);
            safe.rollingResistance = CoreMath.Max(0f, safe.rollingResistance);
            safe.maxSpeed = CoreMath.Max(10f, safe.maxSpeed);
            safe.maxReverseSpeed = CoreMath.Max(1f, safe.maxReverseSpeed);
            safe.turnRate = CoreMath.Max(0.5f, safe.turnRate);
            return safe;
        }

        public PureVehicleStats ToPure()
        {
            return new PureVehicleStats
            {
                mass = mass,
                enginePower = enginePower,
                brakePower = brakePower,
                grip = grip,
                drag = drag,
                rollingResistance = rollingResistance,
                maxSpeed = maxSpeed,
                maxReverseSpeed = maxReverseSpeed,
                turnRate = turnRate
            }.Sanitize();
        }

        public static VehicleStats FromPure(in PureVehicleStats pure)
        {
            return new VehicleStats
            {
                mass = pure.mass,
                enginePower = pure.enginePower,
                brakePower = pure.brakePower,
                grip = pure.grip,
                drag = pure.drag,
                rollingResistance = pure.rollingResistance,
                maxSpeed = pure.maxSpeed,
                maxReverseSpeed = pure.maxReverseSpeed,
                turnRate = pure.turnRate
            }.Sanitize();
        }
    }
}
