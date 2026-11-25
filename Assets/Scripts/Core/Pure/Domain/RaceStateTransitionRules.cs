namespace RacingGame
{
    /// <summary>
    /// Pure state machine guards enforcing valid GameFlowState transitions and action permissions.
    /// </summary>
    public static class RaceStateTransitionRules
    {
        public static bool CanPause(GameFlowState currentState)
        {
            return currentState == GameFlowState.Countdown || currentState == GameFlowState.Racing;
        }

        public static bool CanRestart(GameFlowState currentState, bool isPaused)
        {
            return (!isPaused && currentState == GameFlowState.Racing) || currentState == GameFlowState.Finished;
        }

        public static bool CanTransition(GameFlowState from, GameFlowState to)
        {
            if (from == to) return true;

            switch (from)
            {
                case GameFlowState.MainMenu:
                    return to == GameFlowState.Countdown
                        || to == GameFlowState.UpgradeShop
                        || to == GameFlowState.Racing;
                case GameFlowState.UpgradeShop:
                    return to == GameFlowState.MainMenu
                        || to == GameFlowState.Countdown;
                case GameFlowState.Countdown:
                    return to == GameFlowState.Racing
                        || to == GameFlowState.MainMenu
                        || to == GameFlowState.Countdown;
                case GameFlowState.Racing:
                    return to == GameFlowState.Finished
                        || to == GameFlowState.MainMenu
                        || to == GameFlowState.Countdown;
                case GameFlowState.Finished:
                    return to == GameFlowState.Countdown
                        || to == GameFlowState.UpgradeShop
                        || to == GameFlowState.MainMenu;
                default:
                    return false;
            }
        }

        public static void ValidateTransitionOrThrow(GameFlowState from, GameFlowState to)
        {
            if (!CanTransition(from, to))
            {
                throw new InvalidRaceStateTransitionException(
                    from,
                    to,
                    $"Guard blocked invalid state transition from '{from}' to '{to}'.");
            }
        }
    }
}
