using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RacingGame
{
    /// <summary>
    /// Standalone toggleable CRT scanline & soft vignette overlay controller.
    /// Zero Update() allocations, persists state via PlayerPrefs, and exposes a clean
    /// public API (IsEnabled / ToggleCrt / SetEnabled) for UI buttons without coupling.
    /// </summary>
    public class CrtScanlineController : MonoBehaviour
    {
        public const string PlayerPrefsKey = "racing2d_crt_enabled";
        public const string ShaderName = "RacingGame/CrtScanlineOverlay";

        private static readonly int ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int ScanlineDensityId = Shader.PropertyToID("_ScanlineDensity");
        private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        [Header("CRT Overlay Settings")]
        [SerializeField] private bool defaultEnabled = true;
        [SerializeField] private Shader crtShader;
        [SerializeField, Range(0f, 0.5f)] private float scanlineIntensity = 0.16f;
        [SerializeField, Range(120f, 720f)] private float scanlineDensity = 320f;
        [SerializeField, Range(0f, 0.6f)] private float vignetteStrength = 0.16f;
        [SerializeField, Range(0f, 1f)] private float overallOpacity = 1.0f;
        [SerializeField] private int canvasSortingOrder = 95;

        private GameObject _overlayCanvasObj;
        private RawImage _overlayImage;
        private Material _runtimeMaterial;
        private bool _stateInitialized;

        public bool IsEnabled { get; private set; }
        public bool HasRuntimeMaterial => _runtimeMaterial != null;

        public event Action<bool> CrtStateChanged;

        private void Awake()
        {
            LoadSavedPreference();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeResources();
        }

        public void ReleaseRuntimeResources()
        {
            DestroyUnityObject(_runtimeMaterial);
            _runtimeMaterial = null;

            DestroyUnityObject(_overlayCanvasObj);
            _overlayCanvasObj = null;
            _overlayImage = null;
        }

        public void LoadSavedPreference()
        {
            bool saved = PlayerPrefs.GetInt(PlayerPrefsKey, defaultEnabled ? 1 : 0) != 0;
            ApplyState(saved, persist: false);
        }

        public void SetEnabled(bool enabled)
        {
            ApplyState(enabled, persist: true);
        }

        public bool ToggleCrt()
        {
            SetEnabled(!IsEnabled);
            return IsEnabled;
        }

        private void ApplyState(bool enabled, bool persist)
        {
            bool changed = !_stateInitialized || IsEnabled != enabled;
            _stateInitialized = true;
            IsEnabled = enabled;

            if (persist)
            {
                PlayerPrefs.SetInt(PlayerPrefsKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (enabled)
            {
                EnsureOverlayBuilt();
            }

            if (_overlayCanvasObj != null && _overlayCanvasObj.activeSelf != enabled)
            {
                _overlayCanvasObj.SetActive(enabled);
            }

            if (enabled)
            {
                ApplyMaterialProperties();
            }

            if (changed)
            {
                CrtStateChanged?.Invoke(IsEnabled);
            }
        }

        private void EnsureOverlayBuilt()
        {
            Shader resolvedShader = crtShader != null ? crtShader : Shader.Find(ShaderName);
            if (resolvedShader == null)
            {
                return;
            }

            if (_runtimeMaterial == null)
            {
                _runtimeMaterial = new Material(resolvedShader)
                {
                    name = "Runtime_CrtScanlineOverlayMat",
                    hideFlags = HideFlags.DontSave
                };
            }

            if (_overlayCanvasObj == null)
            {
                _overlayCanvasObj = new GameObject("CrtScanlineOverlayCanvas");
                _overlayCanvasObj.transform.SetParent(transform, false);

                Canvas canvas = _overlayCanvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = canvasSortingOrder;
                canvas.pixelPerfect = false;

                CanvasScaler scaler = _overlayCanvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject imgObj = new GameObject("CrtScanlineQuad");
                imgObj.transform.SetParent(_overlayCanvasObj.transform, false);

                RectTransform rt = imgObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                _overlayImage = imgObj.AddComponent<RawImage>();
                _overlayImage.texture = Texture2D.whiteTexture;
                _overlayImage.color = Color.white;
                _overlayImage.raycastTarget = false;
                _overlayImage.material = _runtimeMaterial;
            }
            else if (_overlayImage != null && _overlayImage.material != _runtimeMaterial)
            {
                _overlayImage.material = _runtimeMaterial;
            }
        }

        private void ApplyMaterialProperties()
        {
            if (_runtimeMaterial == null) return;
            _runtimeMaterial.SetFloat(ScanlineIntensityId, scanlineIntensity);
            _runtimeMaterial.SetFloat(ScanlineDensityId, scanlineDensity);
            _runtimeMaterial.SetFloat(VignetteStrengthId, vignetteStrength);
            _runtimeMaterial.SetFloat(OpacityId, overallOpacity);
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
