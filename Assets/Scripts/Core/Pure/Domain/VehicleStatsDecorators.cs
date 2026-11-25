namespace RacingGame
{
    /// <summary>
    /// Concrete Component for the Structural Decorator pattern: supplies baseline vehicle stats.
    /// </summary>
    public sealed class BaseVehicleStatsProvider : IVehicleStatsProvider
    {
        private readonly PureVehicleStats _baseStats;

        public BaseVehicleStatsProvider(PureVehicleStats baseStats)
        {
            _baseStats = baseStats.Sanitize();
        }

        public PureVehicleStats GetStats()
        {
            return _baseStats;
        }
    }

    /// <summary>
    /// Abstract Structural GoF Decorator wrapping an IVehicleStatsProvider to stack upgrades or role modifiers.
    /// </summary>
    public abstract class VehicleStatsDecorator : IVehicleStatsProvider
    {
        protected readonly IVehicleStatsProvider Inner;

        protected VehicleStatsDecorator(IVehicleStatsProvider inner)
        {
            Inner = inner ?? new BaseVehicleStatsProvider(PureVehicleStats.Default);
        }

        public PureVehicleStats GetStats()
        {
            return ModifyStats(Inner.GetStats()).Sanitize();
        }

        protected abstract PureVehicleStats ModifyStats(PureVehicleStats current);
    }

    public sealed class EngineUpgradeDecorator : VehicleStatsDecorator
    {
        private readonly int _level;
        private readonly float _powerPerLevel;
        private readonly float _maxSpeedPerLevel;

        public EngineUpgradeDecorator(
            IVehicleStatsProvider inner,
            int level,
            float powerPerLevel = 750f,
            float maxSpeedPerLevel = 3.5f)
            : base(inner)
        {
            _level = CoreMath.Max(0, level);
            _powerPerLevel = powerPerLevel;
            _maxSpeedPerLevel = maxSpeedPerLevel;
        }

        protected override PureVehicleStats ModifyStats(PureVehicleStats current)
        {
            current.enginePower += _level * _powerPerLevel;
            current.maxSpeed += _level * _maxSpeedPerLevel;
            return current;
        }
    }

    public sealed class BrakesUpgradeDecorator : VehicleStatsDecorator
    {
        private readonly int _level;
        private readonly float _brakePowerPerLevel;

        public BrakesUpgradeDecorator(
            IVehicleStatsProvider inner,
            int level,
            float brakePowerPerLevel = 1400f)
            : base(inner)
        {
            _level = CoreMath.Max(0, level);
            _brakePowerPerLevel = brakePowerPerLevel;
        }

        protected override PureVehicleStats ModifyStats(PureVehicleStats current)
        {
            current.brakePower += _level * _brakePowerPerLevel;
            return current;
        }
    }

    public sealed class GripUpgradeDecorator : VehicleStatsDecorator
    {
        private readonly int _level;
        private readonly float _gripPerLevel;

        public GripUpgradeDecorator(
            IVehicleStatsProvider inner,
            int level,
            float gripPerLevel = 1.3f)
            : base(inner)
        {
            _level = CoreMath.Max(0, level);
            _gripPerLevel = gripPerLevel;
        }

        protected override PureVehicleStats ModifyStats(PureVehicleStats current)
        {
            current.grip += _level * _gripPerLevel;
            return current;
        }
    }

    public sealed class WeightReductionDecorator : VehicleStatsDecorator
    {
        private readonly int _level;
        private readonly float _massReductionPerLevel;

        public WeightReductionDecorator(
            IVehicleStatsProvider inner,
            int level,
            float massReductionPerLevel = 14f)
            : base(inner)
        {
            _level = CoreMath.Max(0, level);
            _massReductionPerLevel = massReductionPerLevel;
        }

        protected override PureVehicleStats ModifyStats(PureVehicleStats current)
        {
            current.mass -= _level * _massReductionPerLevel;
            return current;
        }
    }

    public sealed class PoliceInterceptorDecorator : VehicleStatsDecorator
    {
        private readonly float _speedBonus;
        private readonly float _engineBonus;
        private readonly float _gripBonus;

        public PoliceInterceptorDecorator(
            IVehicleStatsProvider inner,
            float speedBonus,
            float engineBonus = 450f,
            float gripBonus = 0.8f)
            : base(inner)
        {
            _speedBonus = speedBonus;
            _engineBonus = engineBonus;
            _gripBonus = gripBonus;
        }

        protected override PureVehicleStats ModifyStats(PureVehicleStats current)
        {
            current.maxSpeed += _speedBonus;
            current.enginePower += _engineBonus;
            current.grip += _gripBonus;
            return current;
        }
    }
}
