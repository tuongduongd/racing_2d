using System;

namespace RacingGame
{
    /// <summary>
    /// Base domain exception for Road Rash 2D game rules, persistence, and configuration integrity.
    /// </summary>
    public abstract class GameDomainException : Exception
    {
        public string ErrorCode { get; }

        protected GameDomainException(string errorCode, string message, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "DOMAIN_ERROR" : errorCode;
        }
    }

    /// <summary>
    /// Thrown when a persisted JSON save file or mid-race snapshot is malformed, unreadable, or missing required fields.
    /// </summary>
    public sealed class SaveDataCorruptedException : GameDomainException
    {
        public string FilePath { get; }

        public SaveDataCorruptedException(string filePath, string message, Exception innerException = null)
            : base("SAVE_CORRUPTED", message, innerException)
        {
            FilePath = filePath ?? string.Empty;
        }
    }

    /// <summary>
    /// Thrown when an external track JSON or balance configuration violates structural domain invariants.
    /// </summary>
    public sealed class InvalidTrackConfigurationException : GameDomainException
    {
        public string ConfigIdentifier { get; }

        public InvalidTrackConfigurationException(string configIdentifier, string message, Exception innerException = null)
            : base("INVALID_TRACK_CONFIG", message, innerException)
        {
            ConfigIdentifier = configIdentifier ?? string.Empty;
        }
    }

    /// <summary>
    /// Thrown when an upgrade or unlock transaction violates wallet/level domain invariants in strict mode.
    /// </summary>
    public sealed class InvalidUpgradeTransactionException : GameDomainException
    {
        public UpgradeType Upgrade { get; }

        public InvalidUpgradeTransactionException(UpgradeType upgrade, string message)
            : base("INVALID_UPGRADE_TX", message)
        {
            Upgrade = upgrade;
        }
    }

    /// <summary>
    /// Thrown when a game flow state transition or action violates the state machine guard rules.
    /// </summary>
    public sealed class InvalidRaceStateTransitionException : GameDomainException
    {
        public GameFlowState FromState { get; }
        public GameFlowState TargetState { get; }

        public InvalidRaceStateTransitionException(GameFlowState fromState, GameFlowState targetState, string message)
            : base("INVALID_STATE_TRANSITION", message)
        {
            FromState = fromState;
            TargetState = targetState;
        }
    }
}
