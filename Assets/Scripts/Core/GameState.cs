using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    public enum GameFlowState
    {
        MainMenu,
        Countdown,
        Racing,
        Finished,
        UpgradeShop
    }

    /// <summary>
    /// Abstract State base class implementing the OOP State Pattern for game flow:
    /// MainMenu -> Countdown -> Racing -> Finished -> UpgradeShop -> MainMenu.
    /// </summary>
    public abstract class RaceStateBase
    {
        public abstract GameFlowState StateType { get; }

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
        }
    }

    public class CountdownState : RaceStateBase
    {
        private readonly float _duration;
        private float _remaining;
        private int _lastWholeSecond;

        public float RemainingSeconds => _remaining;
        public override GameFlowState StateType => GameFlowState.Countdown;

        public CountdownState(float durationSeconds = 3.0f)
        {
            _duration = Mathf.Max(0.5f, durationSeconds);
        }

        public override void Enter(GameManager gm)
        {
            gm.ResetRaceGrid();
            _remaining = _duration;
            _lastWholeSecond = Mathf.CeilToInt(_remaining) + 1;
            gm.SetEngineSoundActive(true);
        }

        public override void Update(GameManager gm, float dt)
        {
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
        }

        public override void Update(GameManager gm, float dt)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame)
                {
                    gm.StartRace();
                    return;
                }
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    gm.TransitionTo(GameFlowState.MainMenu);
                    return;
                }
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
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame)
                {
                    gm.StartRace();
                }
                else if (kb.escapeKey.wasPressedThisFrame)
                {
                    gm.TransitionTo(GameFlowState.MainMenu);
                }
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
