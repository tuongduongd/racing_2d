using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    /// <summary>
    /// Procedural sound effects & engine synth controller (zero external asset dependencies).
    /// Encapsulates AudioSources and synthesized AudioClips.
    /// </summary>
    public class RaceAudioController : MonoBehaviour
    {
        [SerializeField] private bool isMuted;
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.45f;

        private AudioSource _engineSource;
        private AudioSource _sfxSource;

        private AudioClip _engineClip;
        private AudioClip _beepLowClip;
        private AudioClip _beepHighClip;
        private AudioClip _checkpointClip;
        private AudioClip _lapClip;
        private AudioClip _finishClip;
        private AudioClip _crashClip;
        private AudioClip _buyOkClip;
        private AudioClip _buyFailClip;
        private AudioClip _kickSwingClip;
        private AudioClip _kickHitClip;

        private float _lastCollisionSoundTime;

        public bool IsMuted => isMuted;

        private void Awake()
        {
            EnsureAudioListener();
            BuildAudioSources();
            GenerateProceduralClips();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                ToggleMute();
            }
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            ApplyMuteState();
        }

        public void SetMuted(bool muted)
        {
            isMuted = muted;
            ApplyMuteState();
        }

        public void SetEngineActive(bool active)
        {
            if (_engineSource == null) return;
            if (active && !isMuted)
            {
                if (!_engineSource.isPlaying) _engineSource.Play();
            }
            else
            {
                if (_engineSource.isPlaying) _engineSource.Stop();
            }
        }

        public void UpdateEnginePitch(float speedNormalized, float throttle)
        {
            if (_engineSource == null || isMuted) return;
            if (!_engineSource.isPlaying) return;

            float t = Mathf.Clamp01(speedNormalized);
            float targetPitch = Mathf.Lerp(0.75f, 2.05f, t) + throttle * 0.12f;
            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, Time.deltaTime * 10f);
            _engineSource.volume = masterVolume * Mathf.Lerp(0.22f, 0.45f, t);
        }

        public void PlayCountdown(bool isGo)
        {
            PlaySfx(isGo ? _beepHighClip : _beepLowClip, 0.8f);
        }

        public void PlayCheckpoint()
        {
            PlaySfx(_checkpointClip, 0.45f);
        }

        public void PlayLapCompleted()
        {
            PlaySfx(_lapClip, 0.75f);
        }

        public void PlayRaceFinished()
        {
            PlaySfx(_finishClip, 0.85f);
        }

        public void PlayCollision()
        {
            if (Time.time - _lastCollisionSoundTime < 0.25f) return;
            _lastCollisionSoundTime = Time.time;
            PlaySfx(_crashClip, 0.65f);
        }

        public void PlayUpgradeResult(bool success)
        {
            PlaySfx(success ? _buyOkClip : _buyFailClip, 0.7f);
        }

        public void PlayKick(bool hit)
        {
            PlaySfx(hit ? _kickHitClip : _kickSwingClip, hit ? 0.85f : 0.5f);
        }

        private void PlaySfx(AudioClip clip, float volumeScale)
        {
            if (isMuted || _sfxSource == null || clip == null) return;
            _sfxSource.PlayOneShot(clip, masterVolume * volumeScale);
        }

        private void ApplyMuteState()
        {
            if (_engineSource != null && isMuted && _engineSource.isPlaying)
            {
                _engineSource.Stop();
            }
        }

        private static void EnsureAudioListener()
        {
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                Camera cam = Camera.main;
                if (cam != null) cam.gameObject.AddComponent<AudioListener>();
            }
        }

        private void BuildAudioSources()
        {
            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.loop = true;
            _engineSource.playOnAwake = false;
            _engineSource.volume = masterVolume * 0.25f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = masterVolume;
        }

        private void GenerateProceduralClips()
        {
            _engineClip = CreateEngineLoopClip();
            _engineSource.clip = _engineClip;

            _beepLowClip = CreateToneClip("BeepLow", 440f, 0.14f, 0.5f);
            _beepHighClip = CreateToneClip("BeepHigh", 880f, 0.28f, 0.6f);
            _checkpointClip = CreateToneClip("Checkpoint", 660f, 0.09f, 0.4f);
            _lapClip = CreateArpeggioClip("LapDone", new[] { 523.25f, 659.25f, 783.99f }, 0.09f);
            _finishClip = CreateArpeggioClip("RaceFinish", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f);
            _crashClip = CreateNoiseThudClip("Crash", 0.18f);
            _buyOkClip = CreateArpeggioClip("BuyOk", new[] { 587.33f, 880f }, 0.08f);
            _buyFailClip = CreateToneClip("BuyFail", 190f, 0.18f, 0.5f);
            _kickSwingClip = CreateKickSwingClip("KickSwing", 0.14f);
            _kickHitClip = CreateNoiseThudClip("KickHit", 0.22f);
        }

        private static AudioClip CreateEngineLoopClip()
        {
            const int sampleRate = 22050;
            const int samples = sampleRate / 2;
            float[] data = new float[samples];
            float baseFreq = 64f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float fundamental = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float harmonic2 = Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t) * 0.45f;
                float harmonic3 = Mathf.Sin(2f * Mathf.PI * baseFreq * 3f * t) * 0.22f;
                data[i] = (fundamental + harmonic2 + harmonic3) * 0.25f;
            }

            AudioClip clip = AudioClip.Create("EngineLoop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateToneClip(string name, float freq, float duration, float amp)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(256, Mathf.RoundToInt(sampleRate * duration));
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float env = 1f - (i / (float)samples);
                float t = i / (float)sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * amp * env;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateArpeggioClip(string name, float[] freqs, float noteDuration)
        {
            const int sampleRate = 22050;
            int noteSamples = Mathf.RoundToInt(sampleRate * noteDuration);
            int totalSamples = noteSamples * freqs.Length;
            float[] data = new float[totalSamples];

            for (int n = 0; n < freqs.Length; n++)
            {
                float freq = freqs[n];
                for (int i = 0; i < noteSamples; i++)
                {
                    float env = 1f - (i / (float)noteSamples);
                    float t = i / (float)sampleRate;
                    data[n * noteSamples + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f * env;
                }
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNoiseThudClip(string name, float duration)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rng = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float env = Mathf.Pow(1f - (i / (float)samples), 1.6f);
                float t = i / (float)sampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.5f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
                data[i] = (thud + noise) * 0.55f * env;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateKickSwingClip(string name, float duration)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(256, Mathf.RoundToInt(sampleRate * duration));
            float[] data = new float[samples];
            System.Random rng = new System.Random(107);

            for (int i = 0; i < samples; i++)
            {
                float u = i / (float)samples;
                float env = Mathf.Sin(u * Mathf.PI);
                float freq = Mathf.Lerp(320f, 135f, u);
                float t = i / (float)sampleRate;
                float whoosh = ((float)rng.NextDouble() * 2f - 1f) * 0.25f + Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;
                data[i] = whoosh * env;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
