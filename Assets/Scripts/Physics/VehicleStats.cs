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
            enginePower = 6200f,
            brakePower = 8500f,
            grip = 8.5f,
            drag = 0.45f,
            rollingResistance = 0.8f,
            maxSpeed = 62f,
            maxReverseSpeed = 8f,
            turnRate = 2.4f
        };

        /// <summary>
        /// Returns a copy with all parameters clamped to safe physical minimums.
        /// </summary>
        public VehicleStats Sanitize()
        {
            VehicleStats safe = this;
            safe.mass = Mathf.Max(50f, safe.mass);
            safe.enginePower = Mathf.Max(500f, safe.enginePower);
            safe.brakePower = Mathf.Max(500f, safe.brakePower);
            safe.grip = Mathf.Max(1f, safe.grip);
            safe.drag = Mathf.Max(0.05f, safe.drag);
            safe.rollingResistance = Mathf.Max(0f, safe.rollingResistance);
            safe.maxSpeed = Mathf.Max(10f, safe.maxSpeed);
            safe.maxReverseSpeed = Mathf.Max(1f, safe.maxReverseSpeed);
            safe.turnRate = Mathf.Max(0.5f, safe.turnRate);
            return safe;
        }
    }
}
