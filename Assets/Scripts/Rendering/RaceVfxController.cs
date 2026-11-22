using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RacingGame
{
    /// <summary>
    /// Manages URP Post-Processing (ACES Tonemapping, Bloom, dynamic Vignette & ChromaticAberration)
    /// and pooled 2D ParticleSystems (off-road dust, kick/collision sparks, high-speed wind streaks).
    /// </summary>
    public class RaceVfxController : MonoBehaviour
    {
        [SerializeField] private VolumeProfile customVolumeProfile = null;

        [Header("URP Bloom Tuning (Neon Night Balance)")]
        [SerializeField, Range(0.8f, 2.0f)] private float bloomThreshold = 1.0f;
        [SerializeField, Range(0f, 1.5f)] private float bloomIntensity = 0.36f;
        [SerializeField, Range(0.1f, 0.9f)] private float bloomScatter = 0.58f;
        [SerializeField, Min(1f)] private float bloomClamp = 4.0f;

        private Camera _camera;
        private Volume _globalVolume;
        private VolumeProfile _runtimeProfile;
        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;

        private ParticleSystem _sparksPs;
        private ParticleSystem _dustPs;
        private ParticleSystem _speedLinesPs;
        private Sprite _softParticleSprite;
        private Texture2D _softParticleTexture;
        private Material _particleMaterial;

        private float _impactImpulse;

        private void OnDestroy()
        {
            DestroyUnityObject(_runtimeProfile);
            _runtimeProfile = null;

            DestroyUnityObject(_particleMaterial);
            _particleMaterial = null;

            DestroyUnityObject(_softParticleSprite);
            _softParticleSprite = null;

            DestroyUnityObject(_softParticleTexture);
            _softParticleTexture = null;
        }

        public void Initialize(Camera cam)
        {
            _camera = cam != null ? cam : Camera.main;
            EnsurePostProcessing();
            EnsureParticleSystems();
        }

        public void UpdateFrameEffects(float dt, float speedNorm, bool isOffRoad, bool isRacing, Vector3 rearTireWorldPos)
        {
            _impactImpulse = Mathf.MoveTowards(_impactImpulse, 0f, dt * 3.5f);

            if (_vignette != null)
            {
                float targetVig = isRacing ? Mathf.Lerp(0.14f, 0.30f, speedNorm) + _impactImpulse * 0.14f : 0.14f;
                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, targetVig, 1f - Mathf.Exp(-10f * dt));
            }

            if (_chromaticAberration != null)
            {
                float speedCa = isRacing && speedNorm > 0.78f ? (speedNorm - 0.78f) * 0.45f : 0f;
                float targetCa = Mathf.Clamp01(speedCa + _impactImpulse * 0.32f);
                _chromaticAberration.intensity.value = Mathf.Lerp(_chromaticAberration.intensity.value, targetCa, 1f - Mathf.Exp(-12f * dt));
            }

            if (_dustPs != null)
            {
                _dustPs.transform.position = rearTireWorldPos;
                var emission = _dustPs.emission;
                bool emitDust = isRacing && isOffRoad && speedNorm > 0.08f;
                emission.rateOverTime = emitDust ? Mathf.Lerp(14f, 38f, speedNorm) : 0f;
            }

            if (_speedLinesPs != null)
            {
                var emission = _speedLinesPs.emission;
                bool emitLines = isRacing && speedNorm > 0.72f;
                float factor = Mathf.InverseLerp(0.72f, 1.0f, speedNorm);
                emission.rateOverTime = emitLines ? Mathf.Lerp(8f, 32f, factor) : 0f;
            }
        }

        public void EmitImpactSparks(Vector3 screenWorldPos, float intensity = 1f)
        {
            _impactImpulse = Mathf.Max(_impactImpulse, Mathf.Clamp01(intensity));
            if (_sparksPs == null) return;

            _sparksPs.transform.position = new Vector3(screenWorldPos.x, screenWorldPos.y, -0.35f);
            int count = Mathf.RoundToInt(Mathf.Lerp(10f, 24f, Mathf.Clamp01(intensity)));
            _sparksPs.Emit(count);
        }

        private void EnsurePostProcessing()
        {
            if (_camera != null)
            {
                UniversalAdditionalCameraData camData = _camera.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null) camData = _camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
                camData.volumeLayerMask = ~0;
            }

            _globalVolume = FindAnyObjectByType<Volume>();
            if (_globalVolume == null)
            {
                GameObject volObj = new GameObject("Global PostProcess Volume");
                volObj.transform.SetParent(transform, false);
                _globalVolume = volObj.AddComponent<Volume>();
                _globalVolume.isGlobal = true;
                _globalVolume.priority = 10f;
            }

            VolumeProfile sourceProfile = customVolumeProfile != null ? customVolumeProfile : _globalVolume.sharedProfile;
            DestroyUnityObject(_runtimeProfile);
            if (sourceProfile == null)
            {
                _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                _runtimeProfile.name = "RuntimeRoadRashVolumeProfile";
            }
            else
            {
                // Clone into runtime instance so PlayMode tuning never mutates project assets unexpectedly
                _runtimeProfile = Instantiate(sourceProfile);
            }
            _globalVolume.profile = _runtimeProfile;
            VolumeProfile profile = _runtimeProfile;

            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.threshold.overrideState = true;
            bloom.threshold.value = bloomThreshold;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = bloomIntensity;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = bloomScatter;
            bloom.clamp.overrideState = true;
            bloom.clamp.value = bloomClamp;

            if (!profile.TryGet(out _vignette))
            {
                _vignette = profile.Add<Vignette>(true);
            }
            _vignette.color.overrideState = true;
            _vignette.color.value = new Color(0.03f, 0.04f, 0.08f);
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = 0.14f;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.35f;

            if (!profile.TryGet(out _chromaticAberration))
            {
                _chromaticAberration = profile.Add<ChromaticAberration>(true);
            }
            _chromaticAberration.intensity.overrideState = true;
            _chromaticAberration.intensity.value = 0f;
        }

        private void EnsureParticleSystems()
        {
            if (_particleMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                _particleMaterial = new Material(shader);
                _particleMaterial.mainTexture = GetSoftCircleTexture();
            }

            if (_sparksPs == null) _sparksPs = CreateSparksSystem();
            if (_dustPs == null) _dustPs = CreateDustSystem();
            if (_speedLinesPs == null) _speedLinesPs = CreateSpeedLinesSystem();
        }

        private ParticleSystem CreateSparksSystem()
        {
            GameObject go = new GameObject("VFX_ImpactSparks");
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.19f);
            main.startColor = RoadRashVisualTheme.SparkHdr;
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = _particleMaterial;
            psr.sortingOrder = 540;
            return ps;
        }

        private ParticleSystem CreateDustSystem()
        {
            GameObject go = new GameObject("VFX_OffRoadDust");
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.50f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startColor = RoadRashVisualTheme.DustTan;
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-70f, 0f, 0f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 0f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = _particleMaterial;
            psr.sortingOrder = 496;
            ps.Play();
            return ps;
        }

        private ParticleSystem CreateSpeedLinesSystem()
        {
            GameObject go = new GameObject("VFX_SpeedStreaks");
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startSize3D = true;
            main.startSizeX = 0.06f;
            main.startSizeY = 0.65f;
            main.startSizeZ = 1f;
            main.startColor = RoadRashVisualTheme.SpeedStreak;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 0.5f, 0.1f);
            go.transform.position = new Vector3(0f, 4.8f, -0.3f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(-22f, -14f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = _particleMaterial;
            psr.sortingOrder = 550;
            ps.Play();
            return ps;
        }

        private Texture2D GetSoftCircleTexture()
        {
            if (_softParticleSprite != null) return _softParticleSprite.texture;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - (size - 1) * 0.5f) / (size * 0.48f);
                    float dy = (y - (size - 1) * 0.5f) / (size * 0.48f);
                    float a = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }

            tex.Apply();
            _softParticleTexture = tex;
            _softParticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return tex;
        }

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
