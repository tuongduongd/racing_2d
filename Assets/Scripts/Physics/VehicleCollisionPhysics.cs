using UnityEngine;

namespace RacingGame
{
    public struct VehicleCollisionResult
    {
        public bool Collided;
        public Vector2 Normal;
        public Vector2 Tangent;
        public Vector2 ContactPoint;
        public float Penetration;
        public float ClosingSpeed;
        public float NormalImpulse;
        public float TangentialImpulse;
        public Vector2 PositionDeltaA;
        public Vector2 PositionDeltaB;
        public Vector2 VelocityDeltaA;
        public Vector2 VelocityDeltaB;
        public float AngularVelocityDeltaA;
        public float AngularVelocityDeltaB;
        public float VerticalLaunchVelA;
        public float VerticalLaunchVelB;
    }

    /// <summary>
    /// Strict Newtonian 2D rigid-body vehicle collision solver:
    /// mass-weighted positional separation, exact normal restitution, Coulomb tangential friction,
    /// 2D angular impulse (I = 0.5 * m * R^2), and energy-bounded ballistic vertical rebound.
    /// </summary>
    public static class VehicleCollisionPhysics
    {
        public static VehicleCollisionResult Resolve(
            Vector2 posA,
            Vector2 velA,
            float massA,
            float radiusA,
            Vector2 posB,
            Vector2 velB,
            float massB,
            float radiusB,
            float restitution = 0.68f,
            float friction = 0.28f)
        {
            float safeRadiusA = Mathf.Max(0.1f, radiusA);
            float safeRadiusB = Mathf.Max(0.1f, radiusB);
            float minDist = safeRadiusA + safeRadiusB;
            Vector2 delta = posB - posA;
            float distSq = delta.sqrMagnitude;

            if (distSq >= minDist * minDist)
            {
                return default;
            }

            float dist = Mathf.Sqrt(distSq);
            Vector2 normal = dist > 0.0001f ? delta / dist : Vector2.right;
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            float penetration = minDist - dist;

            float mA = Mathf.Max(1f, massA);
            float mB = Mathf.Max(1f, massB);
            float invA = 1f / mA;
            float invB = 1f / mB;
            float invSum = invA + invB;

            VehicleCollisionResult res = new VehicleCollisionResult
            {
                Collided = true,
                Normal = normal,
                Tangent = tangent,
                ContactPoint = posA + normal * (safeRadiusA - penetration * 0.5f),
                Penetration = penetration,
                PositionDeltaA = -normal * (penetration * (invA / invSum)),
                PositionDeltaB = normal * (penetration * (invB / invSum))
            };

            Vector2 relVel = velB - velA;
            float vn = Vector2.Dot(relVel, normal);
            if (vn >= 0f)
            {
                return res;
            }

            res.ClosingSpeed = -vn;
            float e = Mathf.Clamp01(restitution);
            float mu = Mathf.Max(0f, friction);

            float jn = -(1f + e) * vn / invSum;
            float vt = Vector2.Dot(relVel, tangent);
            float jt = Mathf.Clamp(-vt / invSum, -mu * jn, mu * jn);

            res.NormalImpulse = jn;
            res.TangentialImpulse = jt;

            Vector2 impulse = normal * jn + tangent * jt;
            res.VelocityDeltaA = -impulse * invA;
            res.VelocityDeltaB = impulse * invB;

            float inertiaA = Mathf.Max(1f, 0.5f * mA * safeRadiusA * safeRadiusA);
            float inertiaB = Mathf.Max(1f, 0.5f * mB * safeRadiusB * safeRadiusB);
            res.AngularVelocityDeltaA = (-safeRadiusA * jt) / inertiaA;
            res.AngularVelocityDeltaB = (-safeRadiusB * jt) / inertiaB;

            float keBefore = 0.5f * (mA * velA.sqrMagnitude + mB * velB.sqrMagnitude);
            float keAfter = 0.5f * (mA * (velA + res.VelocityDeltaA).sqrMagnitude + mB * (velB + res.VelocityDeltaB).sqrMagnitude);
            float dissipatedKE = keBefore - keAfter;
            if (dissipatedKE > 0f)
            {
                if (res.VelocityDeltaA.magnitude > 4.0f)
                {
                    float hopEnergyA = 0.08f * dissipatedKE * (invA / invSum);
                    res.VerticalLaunchVelA = Mathf.Min(Mathf.Sqrt(Mathf.Max(0f, 2f * hopEnergyA / mA)), 4.8f);
                }

                if (res.VelocityDeltaB.magnitude > 4.0f)
                {
                    float hopEnergyB = 0.08f * dissipatedKE * (invB / invSum);
                    res.VerticalLaunchVelB = Mathf.Min(Mathf.Sqrt(Mathf.Max(0f, 2f * hopEnergyB / mB)), 4.8f);
                }
            }

            return res;
        }
    }
}
