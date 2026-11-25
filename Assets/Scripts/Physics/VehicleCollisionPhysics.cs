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
    /// Unity Vector2 facade delegating to pure C# CollisionSolverMath.ResolveCircleImpact.
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
            PureCollisionResult p = CollisionSolverMath.ResolveCircleImpact(
                posA.x, posA.y, velA.x, velA.y, massA, radiusA,
                posB.x, posB.y, velB.x, velB.y, massB, radiusB,
                restitution, friction);

            if (!p.Collided)
            {
                return default;
            }

            return new VehicleCollisionResult
            {
                Collided = true,
                Normal = new Vector2(p.NormalX, p.NormalY),
                Tangent = new Vector2(p.TangentX, p.TangentY),
                ContactPoint = new Vector2(p.ContactPointX, p.ContactPointY),
                Penetration = p.Penetration,
                ClosingSpeed = p.ClosingSpeed,
                NormalImpulse = p.NormalImpulse,
                TangentialImpulse = p.TangentialImpulse,
                PositionDeltaA = new Vector2(p.PositionDeltaAX, p.PositionDeltaAY),
                PositionDeltaB = new Vector2(p.PositionDeltaBX, p.PositionDeltaBY),
                VelocityDeltaA = new Vector2(p.VelocityDeltaAX, p.VelocityDeltaAY),
                VelocityDeltaB = new Vector2(p.VelocityDeltaBX, p.VelocityDeltaBY),
                AngularVelocityDeltaA = p.AngularVelocityDeltaA,
                AngularVelocityDeltaB = p.AngularVelocityDeltaB,
                VerticalLaunchVelA = p.VerticalLaunchVelA,
                VerticalLaunchVelB = p.VerticalLaunchVelB
            };
        }
    }
}
