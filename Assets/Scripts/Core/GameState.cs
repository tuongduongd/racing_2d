using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Abstract State base class implementing the OOP State Pattern for game flow:
    /// MainMenu -> Countdown -> Racing -> Finished -> UpgradeShop -> MainMenu.
    /// Reads player triggers strictly through IInputProvider (zero Keyboard.current coupling).
    /// </summary>
    public abstract class RaceStateBase
    {
        public abstract GameFlowState StateType { get; }
        public virtual float RemainingCountdown => 0f;

        public virtual void Enter(GameManager gm) { }
        public virtual void Update(GameManager gm, float dt) { }
        public virtual void FixedUpdate(GameManager gm, float fixedDt) { }
        public virtual void Exit(GameManager gm) { }
    }

    public class MainMenuState : RaceStateBase
    {
        public override GameFlowState StateType => GameFlowState.MainMenu;

        public override void Enter(GameManager gm)
        {
            gm.ResetRaceGrid();
            gm.SetEngineSoundActive(false);
            gm.SetBgmActive(true);
        }
    }

    public class CountdownState : RaceStateBase
    {
        private readonly float _duration;
        private float _remaining;
        private int _lastWholeSecond;

        public float RemainingSeconds => _remaining;
        public override float RemainingCountdown => _remaining;
        public override GameFlowState StateType => GameFlowState.Countdown;

        public CountdownState(float durationSeconds = 3.0f)
        {
            _duration = Mathf.Max(0.5f, durationSeconds);
        }

        public override void Enter(GameManager gm)
        {
            if (!gm.ConsumeSkipNextGridResetOnEnter())
            {
                gm.ResetRaceGrid();
            }
            _remaining = _duration;
            _lastWholeSecond = Mathf.CeilToInt(_remaining) + 1;
            gm.SetEngineSoundActive(true);
            gm.SetBgmActive(true);
        }

        public override void Update(GameManager gm, float dt)
        {
            IInputProvider input = gm.InputProvider;
            if (input != null && input.WasPausePressed())
            {
                gm.TogglePause();
                return;
            }

            _remaining -= dt;

            int whole = Mathf.CeilToInt(_remaining);
            if (whole > 0 && whole < _lastWholeSecond)
            {
                _lastWholeSecond = whole;
                gm.PlayCountdownBeep(false);
            }

            if (_remaining <= 0f)
            {
                gm.PlayCountdownBeep(true);
                gm.TransitionTo(GameFlowState.Racing);
            }
        }
    }

    public class RacingState : RaceStateBase
    {
        public override GameFlowState StateType => GameFlowState.Racing;

        public override void Enter(GameManager gm)
        {
            gm.SetEngineSoundActive(true);
            gm.SetBgmActive(true);
        }

        public override void Update(GameManager gm, float dt)
        {
            IInputProvider input = gm.InputProvider;
            if (input == null) return;

            if (input.WasPausePressed())
            {
                gm.TogglePause();
                return;
            }

            if (!gm.IsPaused && input.WasRestartPressed())
            {
                gm.StartRace();
                return;
            }

            if (!gm.IsPaused && input.WasRecoverPressed())
            {
                gm.RecoverPlayerToTrack(automatic: false);
            }
        }

        public override void FixedUpdate(GameManager gm, float fixedDt)
        {
            gm.StepRaceSimulation(fixedDt);

            if (gm.IsPlayerFinished())
            {
                gm.TransitionTo(GameFlowState.Finished);
            }
        }
    }

    public class FinishedState : RaceStateBase
    {
        public override GameFlowState StateType => GameFlowState.Finished;

        public override void Enter(GameManager gm)
        {
            gm.RecordPlayerRaceFinish();
            gm.SetEngineSoundActive(false);
        }

        public override void Update(GameManager gm, float dt)
        {
            IInputProvider input = gm.InputProvider;
            if (input == null) return;

            if (input.WasRestartPressed())
            {
                gm.StartRace();
            }
            else if (input.WasPausePressed())
            {
                gm.TransitionTo(GameFlowState.MainMenu);
            }
        }

        public override void FixedUpdate(GameManager gm, float fixedDt)
        {
            // Keep AI racers running smoothly until they cross the finish line
            gm.StepRaceSimulation(fixedDt);
        }
    }

    public class UpgradeShopState : RaceStateBase
    {
        public override GameFlowState StateType => GameFlowState.UpgradeShop;

        public override void Enter(GameManager gm)
        {
            gm.SetEngineSoundActive(false);
            gm.ApplyPlayerUpgrades();
        }
    }
}
