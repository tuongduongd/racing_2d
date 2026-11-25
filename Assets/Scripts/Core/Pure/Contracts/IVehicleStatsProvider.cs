using System;

namespace RacingGame
{
    /// <summary>
    /// Engine-independent vehicle physics stats struct mirroring VehicleStats for pure domain calculations and Decorators.
    /// </summary>
    [Serializable]
    public struct PureVehicleStats
    {
        public float mass;
        public float enginePower;
        public float brakePower;
        public float grip;
        public float drag;
        public float rollingResistance;
        public float maxSpeed;
        public float maxReverseSpeed;
        public float turnRate;

        public static PureVehicleStats Default => new PureVehicleStats
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

        public PureVehicleStats Sanitize()
        {
            PureVehicleStats safe = this;
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
    }

    /// <summary>
    /// Structural GoF Decorator component interface providing vehicle performance stats.
    /// </summary>
    public interface IVehicleStatsProvider
    {
        PureVehicleStats GetStats();
    }
}
