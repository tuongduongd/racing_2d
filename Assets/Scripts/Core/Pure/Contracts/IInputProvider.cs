namespace RacingGame
{
    /// <summary>
    /// Input boundary abstraction (ISP/DIP) isolating core drivers and states from UnityEngine.InputSystem.
    /// </summary>
    public interface IInputProvider
    {
        VehicleInput ReadInput();
        bool WasPausePressed();
        bool WasRestartPressed();
        bool WasRecoverPressed();
        bool WasMutePressed();
        void ResetState();
    }
}
