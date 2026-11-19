using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Nara.Practice.Editor
{
    public static class NaraQuestBuild
    {
        const BuildTargetGroup Group = BuildTargetGroup.Android;

        [MenuItem("NARA/Final Project/Build Quest APK")]
        public static void BuildQuest()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!BuildPipeline.IsBuildTargetSupported(Group, BuildTarget.Android))
                throw new BuildFailedException("Android Build Support, SDK/NDK, and OpenJDK are required in Unity 6000.3.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch the active platform to Android before building NARA.");

            ConfigureQuest();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NaraPracticeBuild.ScenePath) == null)
                NaraPracticeBuild.BuildScene();
            EditorUserBuildSettings.buildAppBundle = false;
            NaraPracticeBuild.Build(BuildTarget.Android, "BuildFinalProject/Quest/NARA.apk");
        }

        static void ConfigureQuest()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget all))
            {
                const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
                all = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (!all)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/XR"))
                        AssetDatabase.CreateFolder("Assets", "XR");
                    all = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(all, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, all, true);
            }
            if (!all.HasManagerSettingsForBuildTarget(Group))
                all.CreateDefaultManagerSettingsForBuildTarget(Group);
            XRGeneralSettings general = all.SettingsForBuildTarget(Group);
            general.InitManagerOnStart = true;
            if (!general.Manager.activeLoaders.Any(loader => loader is OpenXRLoader) &&
                !XRPackageMetadataStore.AssignLoader(general.Manager, typeof(OpenXRLoader).FullName, Group))
                throw new BuildFailedException("Unity could not assign the Android OpenXR loader.");

            FeatureHelpers.RefreshFeatures(Group);
            OpenXRSettings xr = OpenXRSettings.GetSettingsForBuildTargetGroup(Group);
            MetaQuestFeature headset = xr?.GetFeature<MetaQuestFeature>();
            OculusTouchControllerProfile touch = xr?.GetFeature<OculusTouchControllerProfile>();
            if (!headset || !touch)
                throw new BuildFailedException("The Meta Quest and Touch Controller features are unavailable.");
            headset.enabled = true;
            headset.AddTargetDevice("quest2", "Quest 2", true);
            headset.AddTargetDevice("eureka", "Quest 3", true);
            touch.enabled = true;
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(all);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(headset);
            EditorUtility.SetDirty(touch);
            EditorUtility.SetDirty(xr);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "id.ac.its.nara.practice");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            AssetDatabase.SaveAssets();
            Debug.Log("NARA_QUEST Android ARM64, OpenXR, Meta Quest, Oculus Touch");
        }
    }
}
