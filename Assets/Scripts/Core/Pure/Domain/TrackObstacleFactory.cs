using System.Collections.Generic;

namespace RacingGame
{
    public sealed class TrafficConeBlueprint : IObstacleBlueprint
    {
        public ObstacleKind Kind => ObstacleKind.TrafficCone;

        public ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse)
        {
            return new ObstacleSpec(
                Kind,
                defaultHitRadius * 0.90f,
                CoreMath.Clamp(defaultSpeedPenalty + 0.14f, 0.4f, 0.9f),
                defaultLateralImpulse * 0.65f,
                backwardKnockbackMeters: 1.8f,
                backwardVelocityImpulse: 9.5f);
        }
    }

    public sealed class OilBarrelBlueprint : IObstacleBlueprint
    {
        public ObstacleKind Kind => ObstacleKind.OilBarrel;

        public ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse)
        {
            return new ObstacleSpec(
                Kind,
                defaultHitRadius * 1.05f,
                defaultSpeedPenalty,
                defaultLateralImpulse * 1.15f,
                backwardKnockbackMeters: 2.8f,
                backwardVelocityImpulse: 13.5f);
        }
    }

    public sealed class RoadBarrierBlueprint : IObstacleBlueprint
    {
        public ObstacleKind Kind => ObstacleKind.RoadBarrier;

        public ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse)
        {
            return new ObstacleSpec(
                Kind,
                defaultHitRadius * 1.25f,
                CoreMath.Clamp(defaultSpeedPenalty - 0.08f, 0.35f, 0.85f),
                defaultLateralImpulse * 1.25f,
                backwardKnockbackMeters: 3.6f,
                backwardVelocityImpulse: 16.5f);
        }
    }

    public sealed class OilSlickBlueprint : IObstacleBlueprint
    {
        public ObstacleKind Kind => ObstacleKind.OilSlick;

        public ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse)
        {
            return new ObstacleSpec(
                Kind,
                defaultHitRadius * 1.15f,
                CoreMath.Clamp(defaultSpeedPenalty + 0.06f, 0.45f, 0.85f),
                0f,
                isSurfaceHazard: true);
        }
    }

    public sealed class ParkedBusBlueprint : IObstacleBlueprint
    {
        public ObstacleKind Kind => ObstacleKind.ParkedBus;

        public ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse)
        {
            return new ObstacleSpec(
                Kind,
                defaultHitRadius * 1.32f,
                CoreMath.Clamp(defaultSpeedPenalty - 0.18f, 0.32f, 0.72f),
                defaultLateralImpulse * 1.35f,
                outerLaneOnly: true,
                avoidTunnelAndOverpass: true,
                backwardKnockbackMeters: 4.2f,
                backwardVelocityImpulse: 18.5f);
        }
    }

    public sealed class PotholeBlueprint : IObstacleBlueprint
    {
        public ObstacleKind Kind => ObstacleKind.Pothole;

        public ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse)
        {
            return new ObstacleSpec(
                Kind,
                defaultHitRadius * 1.10f,
                CoreMath.Clamp(defaultSpeedPenalty, 0.45f, 0.80f),
                0f,
                isSurfaceHazard: true);
        }
    }

    /// <summary>
    /// Creational GoF Factory (Registry-based Factory Method) creating obstacle specs from IObstacleBlueprint.
    /// Supports Open-Closed Principle: new obstacle blueprints register dynamically without modifying callers.
    /// </summary>
    public sealed class TrackObstacleFactory : IObstacleFactory
    {
        private readonly Dictionary<ObstacleKind, IObstacleBlueprint> _blueprints =
            new Dictionary<ObstacleKind, IObstacleBlueprint>();

        public TrackObstacleFactory()
        {
            Register(new TrafficConeBlueprint());
            Register(new OilBarrelBlueprint());
            Register(new RoadBarrierBlueprint());
            Register(new OilSlickBlueprint());
            Register(new ParkedBusBlueprint());
            Register(new PotholeBlueprint());
        }

        public void Register(IObstacleBlueprint blueprint)
        {
            if (blueprint == null) return;
            _blueprints[blueprint.Kind] = blueprint;
        }

        public bool HasBlueprint(ObstacleKind kind)
        {
            return _blueprints.ContainsKey(kind);
        }

        public ObstacleSpec CreateSpec(
            ObstacleKind kind,
            float defaultHitRadius,
            float defaultSpeedPenalty,
            float defaultLateralImpulse)
        {
            if (_blueprints.TryGetValue(kind, out IObstacleBlueprint blueprint) && blueprint != null)
            {
                return blueprint.BuildSpec(defaultHitRadius, defaultSpeedPenalty, defaultLateralImpulse);
            }

            return new ObstacleSpec(
                kind,
                defaultHitRadius,
                defaultSpeedPenalty,
                defaultLateralImpulse);
        }
    }
}
