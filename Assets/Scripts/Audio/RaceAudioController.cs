using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Procedural sound effects, engine synth, and Synthwave BGM controller implementing IAudioService
    /// (zero external asset dependencies; persists mute & volume preferences via PlayerPrefs).
    /// </summary>
    public class RaceAudioController : MonoBehaviour, IAudioService
    {
        public const string MutePrefKey = "RacingGame_AudioMuted_v1";
        public const string BgmVolumePrefKey = "RacingGame_BgmVolume_v1";
        public const string SfxVolumePrefKey = "RacingGame_SfxVolume_v1";

        [SerializeField] private bool isMuted;
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.45f;
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.28f;
        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 1.0f;

        private AudioSource _engineSource;
        private AudioSource _sfxSource;
        private AudioSource _bgmSource;

        private AudioClip _engineClip;
        private AudioClip _bgmClip;
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
        private bool _bgmRequested;

        public bool IsMuted => isMuted;
        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;
        public AudioClip BgmClip => _bgmClip;
        public bool IsBgmPlaying => _bgmSource != null && _bgmSource.isPlaying;

        private void Awake()
        {
            LoadPreferences();
            EnsureAudioListener();
            BuildAudioSources();
            GenerateProceduralClips();
        }

        private void OnDestroy()
        {
            DestroyClip(ref _engineClip);
            DestroyClip(ref _bgmClip);
            DestroyClip(ref _beepLowClip);
            DestroyClip(ref _beepHighClip);
            DestroyClip(ref _checkpointClip);
            DestroyClip(ref _lapClip);
            DestroyClip(ref _finishClip);
            DestroyClip(ref _crashClip);
            DestroyClip(ref _buyOkClip);
            DestroyClip(ref _buyFailClip);
            DestroyClip(ref _kickSwingClip);
            DestroyClip(ref _kickHitClip);
        }

        public void LoadPreferences()
        {
            if (PlayerPrefs.HasKey(MutePrefKey))
            {
                isMuted = PlayerPrefs.GetInt(MutePrefKey, 0) != 0;
            }
            if (PlayerPrefs.HasKey(BgmVolumePrefKey))
            {
                bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumePrefKey, bgmVolume));
            }
            if (PlayerPrefs.HasKey(SfxVolumePrefKey))
            {
                sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, sfxVolume));
            }
        }

        public void SavePreferences()
        {
            PlayerPrefs.SetInt(MutePrefKey, isMuted ? 1 : 0);
            PlayerPrefs.SetFloat(BgmVolumePrefKey, Mathf.Clamp01(bgmVolume));
            PlayerPrefs.SetFloat(SfxVolumePrefKey, Mathf.Clamp01(sfxVolume));
            PlayerPrefs.Save();
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            ApplyMuteState();
            SavePreferences();
        }

        public void SetMuted(bool muted)
        {
            isMuted = muted;
            ApplyMuteState();
            SavePreferences();
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (_bgmSource != null)
            {
                _bgmSource.volume = masterVolume * bgmVolume;
            }
            SavePreferences();
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (_sfxSource != null)
            {
                _sfxSource.volume = masterVolume * sfxVolume;
            }
            SavePreferences();
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

        public void SetBgmActive(bool active)
        {
            _bgmRequested = active;
            if (_bgmSource == null) return;

            if (active && !isMuted && bgmVolume > 0.001f)
            {
                _bgmSource.volume = masterVolume * bgmVolume;
                if (!_bgmSource.isPlaying) _bgmSource.Play();
            }
            else
            {
                if (_bgmSource.isPlaying) _bgmSource.Stop();
            }
        }

        public void UpdateEnginePitch(float speedNormalized, float throttle)
        {
            if (_engineSource == null || isMuted) return;
            if (!_engineSource.isPlaying) return;

            float t = Mathf.Clamp01(speedNormalized);
            float targetPitch = Mathf.Lerp(0.75f, 2.05f, t) + throttle * 0.12f;
            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, Time.deltaTime * 10f);
            _engineSource.volume = masterVolume * sfxVolume * Mathf.Lerp(0.22f, 0.45f, t);
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
            _sfxSource.PlayOneShot(clip, masterVolume * sfxVolume * volumeScale);
        }

        private void ApplyMuteState()
        {
            if (_engineSource != null && isMuted && _engineSource.isPlaying)
            {
                _engineSource.Stop();
            }

            if (_bgmSource != null)
            {
                if (isMuted && _bgmSource.isPlaying)
                {
                    _bgmSource.Stop();
                }
                else if (!isMuted && _bgmRequested && bgmVolume > 0.001f && !_bgmSource.isPlaying)
                {
                    _bgmSource.volume = masterVolume * bgmVolume;
                    _bgmSource.Play();
                }
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
            _engineSource.volume = masterVolume * sfxVolume * 0.25f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = masterVolume * sfxVolume;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = masterVolume * bgmVolume;
        }

        private void GenerateProceduralClips()
        {
            _engineClip = CreateEngineLoopClip();
            _engineSource.clip = _engineClip;

            _bgmClip = CreateCyberSynthBgmClip();
            _bgmSource.clip = _bgmClip;

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

        private static AudioClip CreateCyberSynthBgmClip()
        {
            const int sampleRate = 22050;
            const float stepSeconds = 0.125f; // 120 BPM 16th-note pulse -> 32 steps = 4.0s seamless loop
            int stepSamples = Mathf.RoundToInt(sampleRate * stepSeconds);
            const int totalSteps = 32;
            int totalSamples = stepSamples * totalSteps;
            float[] data = new float[totalSamples];

            // Driving Cyber Arcade D-minor Synthwave bass + arpeggio progression (Dm -> Bb -> C -> Am)
            float[] bassNotes =
            {
                73.42f, 73.42f, 146.83f, 73.42f, 73.42f, 73.42f, 146.83f, 73.42f,
                58.27f, 58.27f, 116.54f, 58.27f, 58.27f, 58.27f, 116.54f, 58.27f,
                65.41f, 65.41f, 130.81f, 65.41f, 65.41f, 65.41f, 130.81f, 65.41f,
                55.00f, 55.00f, 110.00f, 55.00f, 55.00f, 65.41f, 110.00f, 65.41f
            };
            float[] leadArp =
            {
                293.66f, 349.23f, 440.00f, 349.23f, 293.66f, 440.00f, 523.25f, 440.00f,
                233.08f, 293.66f, 349.23f, 293.66f, 233.08f, 349.23f, 466.16f, 349.23f,
                261.63f, 329.63f, 392.00f, 329.63f, 261.63f, 392.00f, 523.25f, 392.00f,
                220.00f, 261.63f, 329.63f, 261.63f, 220.00f, 329.63f, 440.00f, 329.63f
            };

            for (int step = 0; step < totalSteps; step++)
            {
                float bassFreq = bassNotes[step];
                float leadFreq = leadArp[step];
                bool isKickBeat = step % 4 == 0;
                bool isSnareBeat = step % 8 == 4;

                for (int i = 0; i < stepSamples; i++)
                {
                    float u = i / (float)stepSamples;
                    float t = (step * stepSamples + i) / (float)sampleRate;

                    float bassEnv = Mathf.Exp(-3.2f * u) * Mathf.Clamp01(u * 20f) * Mathf.Clamp01((1f - u) * 18f);
                    float sawBass = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * 0.65f +
                                    Mathf.Sin(4f * Mathf.PI * bassFreq * t) * 0.25f;

                    float leadEnv = Mathf.Exp(-4.5f * u) * Mathf.Clamp01(u * 25f) * Mathf.Clamp01((1f - u) * 18f);
                    float squareLead = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * leadFreq * t)) * 0.14f;

                    float drum = 0f;
                    if (isKickBeat)
                    {
                        float kickFreq = Mathf.Lerp(115f, 46f, Mathf.Clamp01(u * 2.5f));
                        drum += Mathf.Sin(2f * Mathf.PI * kickFreq * u * stepSeconds) * Mathf.Exp(-8f * u) * 0.35f;
                    }
                    else if (isSnareBeat)
                    {
                        float pseudoNoise = Mathf.Sin(t * 9871f) * 0.5f + Mathf.Cos(t * 6317f) * 0.5f;
                        drum += pseudoNoise * Mathf.Exp(-10f * u) * 0.14f;
                    }

                    data[step * stepSamples + i] = Mathf.Clamp(
                        sawBass * bassEnv * 0.42f + squareLead * leadEnv + drum,
                        -0.85f,
                        0.85f);
                }
            }

            AudioClip clip = AudioClip.Create("CyberSynthBGM", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
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

        private static void DestroyClip(ref AudioClip clip)
        {
            if (clip == null) return;
            if (Application.isPlaying) Destroy(clip);
            else DestroyImmediate(clip);
            clip = null;
        }
    }
}
