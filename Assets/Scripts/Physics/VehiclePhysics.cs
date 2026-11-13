using System;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// 2D vehicle physics simulation (engine, braking, aerodynamic drag, lateral tire grip,
    /// and speed-dependent steering).
    /// Encapsulates state and parameters; independent of who is driving (Player or AI).
    /// </summary>
    [Serializable]
    public class VehiclePhysics
    {
        [Header("State")]
        [SerializeField] private Vector2 position;
        [SerializeField] private Vector2 velocity;

        [Tooltip("Heading angle in radians (0 = +X, PI/2 = +Y).")]
        [SerializeField] private float heading;

        [Header("Vehicle Parameters")]
        [SerializeField] private float mass = 220f;
        [SerializeField] private float enginePower = 6200f;
        [SerializeField] private float brakePower = 8500f;
        [SerializeField] private float grip = 8.5f;
        [SerializeField] private float drag = 0.45f;
        [SerializeField] private float rollingResistance = 0.8f;
        [SerializeField] private float maxSpeed = 62f;
        [SerializeField] private float maxReverseSpeed = 8f;
        [SerializeField] private float turnRate = 2.4f;

        private float _throttleInput;
        private float _brakeInput;
        private float _steeringInput;

        private float _surfaceDragMultiplier = 1f;
        private float _surfaceGripMultiplier = 1f;
        private float _surfaceMaxSpeedMultiplier = 1f;

        public Vector2 Position => position;
        public Vector2 Velocity => velocity;
        public float Heading => heading;

        public float Mass => mass;
        public float EnginePower => enginePower;
        public float BrakePower => brakePower;
        public float Grip => grip;
        public float Drag => drag;
        public float MaxSpeed => maxSpeed;
        public float TurnRate => turnRate;

        public Vector2 Forward => new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
        public Vector2 Right => new Vector2(Mathf.Sin(heading), -Mathf.Cos(heading));

        public float Speed => velocity.magnitude;
        public float ForwardSpeed => Vector2.Dot(velocity, Forward);
        public float LateralSpeed => Vector2.Dot(velocity, Right);
        public float SpeedKmh => ForwardSpeed * 3.6f;

        public void Reset(Vector2 startPosition, float startHeadingRad)
        {
            position = startPosition;
            velocity = Vector2.zero;
            heading = NormalizeAngle(startHeadingRad);
            _throttleInput = 0f;
            _brakeInput = 0f;
            _steeringInput = 0f;
            _surfaceDragMultiplier = 1f;
            _surfaceGripMultiplier = 1f;
            _surfaceMaxSpeedMultiplier = 1f;
        }

        public void ConfigureBaseStats(float newMass, float newEnginePower, float newBrakePower, float newGrip, float newMaxSpeed)
        {
            mass = Mathf.Max(50f, newMass);
            enginePower = Mathf.Max(500f, newEnginePower);
            brakePower = Mathf.Max(500f, newBrakePower);
            grip = Mathf.Max(1f, newGrip);
            maxSpeed = Mathf.Max(10f, newMaxSpeed);
        }

        public void ApplyThrottle(float amount)
        {
            _throttleInput = Mathf.Clamp01(amount);
        }

        public void ApplyBrake(float amount)
        {
            _brakeInput = Mathf.Clamp01(amount);
        }

        public void ApplySteering(float amount)
        {
            _steeringInput = Mathf.Clamp(amount, -1f, 1f);
        }

        public void SetSurfaceModifier(float dragMultiplier, float gripMultiplier, float maxSpeedMultiplier)
        {
            _surfaceDragMultiplier = Mathf.Max(0.1f, dragMultiplier);
            _surfaceGripMultiplier = Mathf.Max(0.1f, gripMultiplier);
            _surfaceMaxSpeedMultiplier = Mathf.Clamp(maxSpeedMultiplier, 0.1f, 1.5f);
        }

        /// <summary>
        /// Applies an external position correction (e.g., track boundary or collision separation).
        /// </summary>
        public void TranslatePosition(Vector2 delta)
        {
            position += delta;
        }

        /// <summary>
        /// Sets position directly in controlled track-projection / respawn scenarios.
        /// </summary>
        public void SetPosition(Vector2 newPosition)
        {
            position = newPosition;
        }

        /// <summary>
        /// Applies an impulse/velocity adjustment (e.g., vehicle-to-vehicle bump).
        /// </summary>
        public void ApplyVelocityImpulse(Vector2 deltaVelocity)
        {
            velocity += deltaVelocity;
        }

        /// <summary>
        /// Scales current velocity (e.g., wall or collision damping).
        /// </summary>
        public void ScaleVelocity(float factor)
        {
            velocity *= Mathf.Clamp01(factor);
        }

        public void Update(float dt)
        {
            if (dt <= 0f) return;

            float safeMass = Mathf.Max(1f, mass);
            Vector2 fwd = Forward;
            Vector2 right = Right;

            float vForward = Vector2.Dot(velocity, fwd);
            float vLateral = Vector2.Dot(velocity, right);

            // 1. Speed-dependent steering (cannot spin in place at zero speed)
            float absForward = Mathf.Abs(vForward);
            float steerSpeedFactor = Mathf.Clamp01(absForward / 6f);
            float highSpeedStability = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(absForward / maxSpeed));
            float steerDirection = vForward >= -0.1f ? 1f : -1f;

            heading -= _steeringInput * turnRate * steerSpeedFactor * highSpeedStability * steerDirection * dt;
            heading = NormalizeAngle(heading);

            fwd = Forward;
            right = Right;

            // 2. Longitudinal acceleration (throttle & brake)
            float engineAccel = (_throttleInput * enginePower) / safeMass;
            float forwardAccel = engineAccel;

            if (_brakeInput > 0.01f)
            {
                float brakeDecel = (_brakeInput * brakePower) / safeMass;
                if (vForward > 0.5f)
                {
                    forwardAccel -= brakeDecel;
                }
                else if (vForward < -0.5f)
                {
                    forwardAccel += brakeDecel * 0.6f;
                }
                else if (_throttleInput < 0.05f)
                {
                    forwardAccel -= brakeDecel * 0.25f;
                }
            }

            vForward += forwardAccel * dt;

            // 3. Aerodynamic drag & rolling resistance
            float effectiveDrag = drag * _surfaceDragMultiplier;
            float dragDecel = (effectiveDrag * vForward * Mathf.Abs(vForward)) / safeMass;
            float rollingDecel = rollingResistance * _surfaceDragMultiplier * Mathf.Sign(vForward);

            if (Mathf.Abs(vForward) > 0.05f)
            {
                float totalResist = (dragDecel + rollingDecel) * dt;
                if (Mathf.Abs(totalResist) > Mathf.Abs(vForward) && _throttleInput < 0.01f && _brakeInput < 0.01f)
                {
                    vForward = 0f;
                }
                else
                {
                    vForward -= totalResist;
                }
            }
            else if (_throttleInput < 0.01f && _brakeInput < 0.01f)
            {
                vForward = 0f;
            }

            // Clamp longitudinal speed
            float effectiveMaxSpeed = maxSpeed * _surfaceMaxSpeedMultiplier;
            vForward = Mathf.Clamp(vForward, -maxReverseSpeed, effectiveMaxSpeed);

            // 4. Lateral tire grip (exponential damping of sideways slip)
            float effectiveGrip = grip * _surfaceGripMultiplier;
            vLateral *= Mathf.Exp(-effectiveGrip * dt);

            velocity = fwd * vForward + right * vLateral;
            position += velocity * dt;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > Mathf.PI) angle -= Mathf.PI * 2f;
            while (angle < -Mathf.PI) angle += Mathf.PI * 2f;
            return angle;
        }
    }
}
