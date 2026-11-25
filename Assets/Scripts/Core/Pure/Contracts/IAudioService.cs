namespace RacingGame
{
    /// <summary>
    /// Audio boundary abstraction (ISP/DIP) for SFX, engine synth, and BGM playback.
    /// </summary>
    public interface IAudioService
    {
        bool IsMuted { get; }
        float BgmVolume { get; }
        float SfxVolume { get; }

        void ToggleMute();
        void SetMuted(bool muted);
        void SetBgmVolume(float volume);
        void SetSfxVolume(float volume);
        void SetEngineActive(bool active);
        void SetBgmActive(bool active);
        void UpdateEnginePitch(float speedNormalized, float throttle);

        void PlayCountdown(bool isGo);
        void PlayCheckpoint();
        void PlayLapCompleted();
        void PlayRaceFinished();
        void PlayCollision();
        void PlayUpgradeResult(bool success);
        void PlayKick(bool hit);
    }
}
