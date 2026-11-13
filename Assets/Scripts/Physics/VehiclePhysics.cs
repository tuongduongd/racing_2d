using System;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// 2D vehicle physics simulation (engine, braking, aerodynamic drag, lateral tire grip,
    /// and speed-dependent steering). Independent of who is driving (player or AI).
    /// </summary>
    [Serializable]
    public class VehiclePhysics
    {
        [Header("State")]
        public Vector2 position;
        public Vector2 velocity;

        [Tooltip("Heading angle in radians (0 = +X, PI/2 = +Y).")]
        public float heading;

        [Header("Vehicle Parameters")]
        public float mass = 220f;
        public float enginePower = 6200f;
        public float brakePower = 8500f;
        public float grip = 8.5f;
        public float drag = 0.45f;
        public float rollingResistance = 0.8f;
        public float maxSpeed = 62f;
        public float maxReverseSpeed = 8f;
        public float turnRate = 2.4f;

        // Current frame inputs (set via ApplyThrottle / ApplyBrake / ApplySteering)
        private float _throttleInput;
        private float _brakeInput;
        private float _steeringInput;

        // Surface modifiers (e.g., off-road grass slows down and reduces grip)
        private float _surfaceDragMultiplier = 1f;
        private float _surfaceGripMultiplier = 1f;
        private float _surfaceMaxSpeedMultiplier = 1f;

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
            heading = startHeadingRad;
            _throttleInput = 0f;
            _brakeInput = 0f;
            _steeringInput = 0f;
            _surfaceDragMultiplier = 1f;
            _surfaceGripMultiplier = 1f;
            _surfaceMaxSpeedMultiplier = 1f;
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

        public void Update(float dt)
        {
            if (dt <= 0f) return;

            float safeMass = Mathf.Max(1f, mass);
            Vector2 fwd = Forward;
            Vector2 right = Right;

            float vForward = Vector2.Dot(velocity, fwd);
            float vLateral = Vector2.Dot(velocity, right);

            // 1. Speed-dependent steering (no spinning in place at zero speed)
            float absForward = Mathf.Abs(vForward);
            float steerSpeedFactor = Mathf.Clamp01(absForward / 6f);
            // Slightly reduce turn rate at extreme top speed for stability
            float highSpeedStability = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(absForward / maxSpeed));
            float steerDirection = vForward >= -0.1f ? 1f : -1f;

            heading -= _steeringInput * turnRate * steerSpeedFactor * highSpeedStability * steerDirection * dt;
            heading = NormalizeAngle(heading);

            // Recompute basis vectors after steering
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
                    // Gentle reverse when nearly stopped and holding brake
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

            // Clamp forward / reverse speed
            float effectiveMaxSpeed = maxSpeed * _surfaceMaxSpeedMultiplier;
            vForward = Mathf.Clamp(vForward, -maxReverseSpeed, effectiveMaxSpeed);

            // 4. Lateral tire grip (exponential damping of sideways slip)
            float effectiveGrip = grip * _surfaceGripMultiplier;
            vLateral *= Mathf.Exp(-effectiveGrip * dt);

            // Reconstruct world-space velocity and integrate position
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
