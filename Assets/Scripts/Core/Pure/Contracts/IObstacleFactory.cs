namespace RacingGame
{
    /// <summary>
    /// Immutable value object describing physics and lane placement rules for an obstacle kind.
    /// </summary>
    public readonly struct ObstacleSpec
    {
        public ObstacleKind Kind { get; }
        public float HitRadius { get; }
        public float SpeedMultiplierOnHit { get; }
        public float LateralKnockImpulse { get; }
        public bool OuterLaneOnly { get; }
        public bool AvoidTunnelAndOverpass { get; }

        public ObstacleSpec(
            ObstacleKind kind,
            float hitRadius,
            float speedMultiplierOnHit,
            float lateralKnockImpulse,
            bool outerLaneOnly = false,
            bool avoidTunnelAndOverpass = false)
        {
            Kind = kind;
            HitRadius = CoreMath.Max(0.4f, hitRadius);
            SpeedMultiplierOnHit = CoreMath.Clamp(speedMultiplierOnHit, 0.25f, 0.95f);
            LateralKnockImpulse = CoreMath.Max(0.5f, lateralKnockImpulse);
            OuterLaneOnly = outerLaneOnly;
            AvoidTunnelAndOverpass = avoidTunnelAndOverpass;
        }
    }

    /// <summary>
    /// OCP extension point for obstacle blueprints registered into IObstacleFactory.
    /// </summary>
    public interface IObstacleBlueprint
    {
        ObstacleKind Kind { get; }
        ObstacleSpec BuildSpec(float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse);
    }

    /// <summary>
    /// Creational GoF Factory interface for building obstacle specifications without modifying TrackObstacleSystem.
    /// </summary>
    public interface IObstacleFactory
    {
        void Register(IObstacleBlueprint blueprint);
        bool HasBlueprint(ObstacleKind kind);
        ObstacleSpec CreateSpec(ObstacleKind kind, float defaultHitRadius, float defaultSpeedPenalty, float defaultLateralImpulse);
    }
}
