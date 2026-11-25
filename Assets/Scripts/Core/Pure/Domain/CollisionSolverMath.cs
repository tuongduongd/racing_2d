namespace RacingGame
{
    public struct PureCollisionResult
    {
        public bool Collided;
        public float NormalX;
        public float NormalY;
        public float TangentX;
        public float TangentY;
        public float ContactPointX;
        public float ContactPointY;
        public float Penetration;
        public float ClosingSpeed;
        public float NormalImpulse;
        public float TangentialImpulse;
        public float PositionDeltaAX;
        public float PositionDeltaAY;
        public float PositionDeltaBX;
        public float PositionDeltaBY;
        public float VelocityDeltaAX;
        public float VelocityDeltaAY;
        public float VelocityDeltaBX;
        public float VelocityDeltaBY;
        public float AngularVelocityDeltaA;
        public float AngularVelocityDeltaB;
        public float VerticalLaunchVelA;
        public float VerticalLaunchVelB;
    }

    /// <summary>
    /// Pure C# Newtonian 2D rigid-body collision solver (zero UnityEngine dependency).
    /// </summary>
    public static class CollisionSolverMath
    {
        public static PureCollisionResult ResolveCircleImpact(
            float posAX, float posAY, float velAX, float velAY, float massA, float radiusA,
            float posBX, float posBY, float velBX, float velBY, float massB, float radiusB,
            float restitution = 0.68f,
            float friction = 0.28f)
        {
            float safeRadiusA = CoreMath.Max(0.1f, radiusA);
            float safeRadiusB = CoreMath.Max(0.1f, radiusB);
            float minDist = safeRadiusA + safeRadiusB;
            float dx = posBX - posAX;
            float dy = posBY - posAY;
            float distSq = dx * dx + dy * dy;

            if (distSq >= minDist * minDist)
            {
                return default;
            }

            float dist = CoreMath.Sqrt(distSq);
            float nx = dist > 0.0001f ? dx / dist : 1f;
            float ny = dist > 0.0001f ? dy / dist : 0f;
            float tx = -ny;
            float ty = nx;
            float penetration = minDist - dist;

            float mA = CoreMath.Max(1f, massA);
            float mB = CoreMath.Max(1f, massB);
            float invA = 1f / mA;
            float invB = 1f / mB;
            float invSum = invA + invB;

            PureCollisionResult res = new PureCollisionResult
            {
                Collided = true,
                NormalX = nx,
                NormalY = ny,
                TangentX = tx,
                TangentY = ty,
                ContactPointX = posAX + nx * (safeRadiusA - penetration * 0.5f),
                ContactPointY = posAY + ny * (safeRadiusA - penetration * 0.5f),
                Penetration = penetration,
                PositionDeltaAX = -nx * (penetration * (invA / invSum)),
                PositionDeltaAY = -ny * (penetration * (invA / invSum)),
                PositionDeltaBX = nx * (penetration * (invB / invSum)),
                PositionDeltaBY = ny * (penetration * (invB / invSum))
            };

            float relVelX = velBX - velAX;
            float relVelY = velBY - velAY;
            float vn = relVelX * nx + relVelY * ny;
            if (vn >= 0f)
            {
                return res;
            }

            res.ClosingSpeed = -vn;
            float e = CoreMath.Clamp01(restitution);
            float mu = CoreMath.Max(0f, friction);

            float jn = -(1f + e) * vn / invSum;
            float vt = relVelX * tx + relVelY * ty;
            float jt = CoreMath.Clamp(-vt / invSum, -mu * jn, mu * jn);

            res.NormalImpulse = jn;
            res.TangentialImpulse = jt;

            float impX = nx * jn + tx * jt;
            float impY = ny * jn + ty * jt;
            res.VelocityDeltaAX = -impX * invA;
            res.VelocityDeltaAY = -impY * invA;
            res.VelocityDeltaBX = impX * invB;
            res.VelocityDeltaBY = impY * invB;

            float inertiaA = CoreMath.Max(1f, 0.5f * mA * safeRadiusA * safeRadiusA);
            float inertiaB = CoreMath.Max(1f, 0.5f * mB * safeRadiusB * safeRadiusB);
            res.AngularVelocityDeltaA = (-safeRadiusA * jt) / inertiaA;
            res.AngularVelocityDeltaB = (-safeRadiusB * jt) / inertiaB;

            float keBefore = 0.5f * (mA * (velAX * velAX + velAY * velAY) + mB * (velBX * velBX + velBY * velBY));
            float postAX = velAX + res.VelocityDeltaAX;
            float postAY = velAY + res.VelocityDeltaAY;
            float postBX = velBX + res.VelocityDeltaBX;
            float postBY = velBY + res.VelocityDeltaBY;
            float keAfter = 0.5f * (mA * (postAX * postAX + postAY * postAY) + mB * (postBX * postBX + postBY * postBY));
            float dissipatedKE = keBefore - keAfter;

            if (dissipatedKE > 0f)
            {
                float dvAMag = CoreMath.Sqrt(res.VelocityDeltaAX * res.VelocityDeltaAX + res.VelocityDeltaAY * res.VelocityDeltaAY);
                if (dvAMag > 4.0f)
                {
                    float hopEnergyA = 0.08f * dissipatedKE * (invA / invSum);
                    res.VerticalLaunchVelA = CoreMath.Min(CoreMath.Sqrt(CoreMath.Max(0f, 2f * hopEnergyA / mA)), 4.8f);
                }

                float dvBMag = CoreMath.Sqrt(res.VelocityDeltaBX * res.VelocityDeltaBX + res.VelocityDeltaBY * res.VelocityDeltaBY);
                if (dvBMag > 4.0f)
                {
                    float hopEnergyB = 0.08f * dissipatedKE * (invB / invSum);
                    res.VerticalLaunchVelB = CoreMath.Min(CoreMath.Sqrt(CoreMath.Max(0f, 2f * hopEnergyB / mB)), 4.8f);
                }
            }

            return res;
        }
    }
}
