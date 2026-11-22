using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RacingGame.Tests
{
    /// <summary>
    /// Stage 3 EditMode tests verifying toggleable CRT scanline PlayerPrefs persistence,
    /// camera/canvas-free safety, runtime material cleanup, and calibrated URP Bloom defaults.
    /// </summary>
    public class VfxAndCrtScanlineTests
    {
        [Test]
        public void CrtScanlineController_SavesAndLoadsStateViaPlayerPrefsWithoutCameraOrCanvas()
        {
            bool hadKey = PlayerPrefs.HasKey(CrtScanlineController.PlayerPrefsKey);
            int previousVal = PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, 1);

            GameObject go1 = null;
            GameObject go2 = null;

            try
            {
                Shader crtShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CrtScanlineOverlay.shader");
                Assert.IsNotNull(crtShader, "Expected CrtScanlineOverlay.shader at Assets/Shaders/CrtScanlineOverlay.shader");

                PlayerPrefs.DeleteKey(CrtScanlineController.PlayerPrefsKey);
                PlayerPrefs.Save();

                go1 = new GameObject("TestCrtController_1");
                CrtScanlineController crt1 = go1.AddComponent<CrtScanlineController>();
                var observedStates = new List<bool>();
                crt1.CrtStateChanged += observedStates.Add;

                // Must safely load default state even without Camera or Canvas in the scene
                crt1.LoadSavedPreference();
                Assert.IsTrue(crt1.IsEnabled);

                crt1.SetEnabled(false);
                Assert.IsFalse(crt1.IsEnabled);
                Assert.AreEqual(0, PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, -1));

                bool toggled = crt1.ToggleCrt();
                Assert.IsTrue(toggled);
                Assert.IsTrue(crt1.IsEnabled);
                Assert.AreEqual(1, PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, -1));
                CollectionAssert.AreEqual(new[] { true, false, true }, observedStates);

                // Persist disabled and verify a fresh controller instance loads 'false' cleanly
                crt1.SetEnabled(false);
                crt1.ReleaseRuntimeResources();
                Assert.IsFalse(crt1.HasRuntimeMaterial, "Runtime material must be cleaned up on release.");
                Object.DestroyImmediate(go1);
                go1 = null;

                go2 = new GameObject("TestCrtController_2");
                CrtScanlineController crt2 = go2.AddComponent<CrtScanlineController>();
                crt2.LoadSavedPreference();
                Assert.IsFalse(crt2.IsEnabled, "Fresh controller must load persisted disabled state from PlayerPrefs.");

                crt2.ToggleCrt();
                Assert.IsTrue(crt2.IsEnabled);
                Assert.AreEqual(1, PlayerPrefs.GetInt(CrtScanlineController.PlayerPrefsKey, -1));
                crt2.ReleaseRuntimeResources();
                Assert.IsFalse(crt2.HasRuntimeMaterial);
            }
            finally
            {
                if (go1 != null)
                {
                    var c1 = go1.GetComponent<CrtScanlineController>();
                    if (c1 != null) c1.ReleaseRuntimeResources();
                    Object.DestroyImmediate(go1);
                }
                if (go2 != null)
                {
                    var c2 = go2.GetComponent<CrtScanlineController>();
                    if (c2 != null) c2.ReleaseRuntimeResources();
                    Object.DestroyImmediate(go2);
                }

                if (hadKey)
                {
                    PlayerPrefs.SetInt(CrtScanlineController.PlayerPrefsKey, previousVal);
                }
                else
                {
                    PlayerPrefs.DeleteKey(CrtScanlineController.PlayerPrefsKey);
                }
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void RaceVfxController_DefaultVolumeProfileBloomIsCalibratedAndSparksDoNotThrow()
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/DefaultVolumeProfile.asset");
            Assert.IsNotNull(subAssets, "Expected DefaultVolumeProfile.asset");

            Object bloomObj = null;
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] != null && subAssets[i].name == "Bloom")
                {
                    bloomObj = subAssets[i];
                    break;
                }
            }

            Assert.IsNotNull(bloomObj, "Expected Bloom component sub-asset in DefaultVolumeProfile.asset");
            SerializedObject bloomSo = new SerializedObject(bloomObj);
            float threshold = bloomSo.FindProperty("threshold.m_Value").floatValue;
            float intensity = bloomSo.FindProperty("intensity.m_Value").floatValue;
            float scatter = bloomSo.FindProperty("scatter.m_Value").floatValue;
            float clamp = bloomSo.FindProperty("clamp.m_Value").floatValue;

            Assert.GreaterOrEqual(threshold, 0.95f);
            Assert.LessOrEqual(threshold, 1.2f);
            Assert.Greater(intensity, 0f);
            Assert.LessOrEqual(intensity, 0.6f);
            Assert.Greater(scatter, 0.3f);
            Assert.LessOrEqual(scatter, 0.75f);
            Assert.LessOrEqual(clamp, 10f);

            GameObject vfxGo = new GameObject("TestRaceVfxController");
            try
            {
                RaceVfxController vfx = vfxGo.AddComponent<RaceVfxController>();
                Assert.DoesNotThrow(() => vfx.EmitImpactSparks(Vector3.zero, 1f));
                Assert.DoesNotThrow(() => vfx.UpdateFrameEffects(1f / 60f, 0.8f, false, true, Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(vfxGo);
            }
        }
    }
}
