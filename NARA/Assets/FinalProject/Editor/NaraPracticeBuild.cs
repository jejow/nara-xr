using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nara.Practice.Editor
{
    public static class NaraPracticeBuild
    {
        internal const string Root = "Assets/FinalProject";
        public const string ScenePath = Root + "/Scenes/" + NaraPractice.UniversityScene + ".unity";
        public const string If101ScenePath = Root + "/Scenes/" + NaraPractice.If101Scene + ".unity";
        static readonly string[] ScenePaths = { ScenePath, If101ScenePath };
        const string Ga3 = "Assets/GA3";

        [MenuItem("NARA/Final Project/Create practice scenes")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildScene();
        }

        [MenuItem("NARA/Final Project/Build Windows")]
        public static void BuildWindows()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (ScenePaths.Any(scene => !AssetDatabase.LoadAssetAtPath<SceneAsset>(scene)))
                BuildScene();
            string output = Application.isBatchMode ? Environment.GetEnvironmentVariable("NARA_WINDOWS_BUILD_PATH") : null;
            Build(BuildTarget.StandaloneWindows64,
                string.IsNullOrWhiteSpace(output) ? "BuildFinalProject/Windows/NARA.exe" : Path.GetFullPath(output));
        }

        public static void RebuildWindows()
        {
            BuildScene();
            BuildWindows();
        }

        internal static void BuildScene()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int avatarTriangles = RocketboxAudienceBuilder.BuildPrefabs();
            BuildClassroomScene(If101ScenePath, true, avatarTriangles);
            BuildClassroomScene(ScenePath, false, avatarTriangles);
            EditorBuildSettings.scenes = ScenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            AssetDatabase.SaveAssets();
            VerifySceneDependencies();
        }

        static void BuildClassroomScene(string scenePath, bool if101, int avatarTriangles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environment = new GameObject("Ruang presentasi universitas");
            Transform room = environment.transform;
            GameObject classroom = UniversityClassroom.Install(room, if101);
            Vector3 presenter = UniversityClassroom.Point(classroom, "NaraPresenter");
            Vector3[] seats = UniversityClassroom.AudiencePoints(classroom);
            Vector3 screenCenter = UniversityClassroom.Point(classroom, "NaraScreen");
            float screenWidth = 2 * Vector3.Distance(screenCenter, UniversityClassroom.Point(classroom, "NaraScreenEdge"));
            Vector3 frontRowCenter = (seats[0] + seats[NaraAudience.SeatsPerRow - 1]) * .5f;
            Vector3 frontToAudience = frontRowCenter - screenCenter;
            Vector3 roomForward = Mathf.Abs(frontToAudience.z) > Mathf.Abs(frontToAudience.x)
                ? Mathf.Sign(frontToAudience.z) * Vector3.forward
                : Mathf.Sign(frontToAudience.x) * Vector3.right;
            Material slideSurface = Material("Slide screen", 0xFFFFFF, "Unlit/Texture");
            Transform screen = Shape(PrimitiveType.Quad, "Slide image", room, screenCenter,
                new Vector3(screenWidth, screenWidth * 9 / 16, 1), slideSurface, false);
            screen.rotation = Quaternion.LookRotation(-roomForward);
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            MeshRenderer monitor = null;
            if (!if101)
            {
                Vector3 monitorCenter = UniversityClassroom.Point(classroom, "NaraConfidenceScreen");
                Vector3 monitorNormal = UniversityClassroom.Point(classroom, "NaraConfidenceNormal") - monitorCenter;
                Transform monitorQuad = Shape(PrimitiveType.Quad, "Confidence slide image", room, monitorCenter,
                    new Vector3(.422f, .422f * 9 / 16, 1), slideSurface, false);
                monitorQuad.rotation = Quaternion.LookRotation(-monitorNormal);
                monitor = monitorQuad.GetComponent<MeshRenderer>();
                monitor.shadowCastingMode = ShadowCastingMode.Off;
            }
            var deck = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Resources/Nara/Slides/slide_1.png");
            slideSurface.mainTexture = deck;
            EditorUtility.SetDirty(slideSurface);

            Lighting(room, if101);
            NaraAudience audience = RocketboxAudienceBuilder.Install(room, roomForward, seats);
            var voiceObject = new GameObject("NARA voice");
            voiceObject.transform.position = seats[0] + Vector3.up * 1.26f;
            var voice = voiceObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = .55f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 2.5f;
            voice.maxDistance = 14f;

            var position = new GameObject("Presenter origin").transform;
            Vector3 aim = UniversityClassroom.Point(classroom, "NaraSightline");
            Vector3 sightline = aim - presenter;
            Vector3 level = Vector3.ProjectOnPlane(sightline, Vector3.up);
            float desktopPitch = Mathf.Atan2(-sightline.y, level.magnitude) * Mathf.Rad2Deg;
            position.SetPositionAndRotation(presenter, Quaternion.LookRotation(level));
            var camObject = new GameObject("Main Camera");
            camObject.tag = "MainCamera";
            camObject.transform.SetParent(position, false);
            camObject.transform.localRotation = Quaternion.Euler(desktopPitch, 0, 0);
            Camera camera = camObject.AddComponent<Camera>();
            camera.fieldOfView = 65;
            camera.nearClipPlane = .065f;
            camera.farClipPlane = 32;
            camera.allowHDR = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorHex(0x172F34);
            camObject.AddComponent<AudioListener>();
            var panel = new GameObject("World-space controls").transform;
            panel.SetParent(position, false);
            panel.localPosition = new Vector3(0, -.18f, 1.68f);

            var app = new GameObject("NARA session").AddComponent<NaraPractice>();
            app.practiceCamera = camera;
            app.controlsAnchor = panel;
            app.room = environment;
            app.slideScreen = screen.GetComponent<MeshRenderer>();
            app.confidenceScreen = monitor;
            app.desktopPitch = desktopPitch;
            app.voice = voice;
            app.audience = audience;
            app.sceneTriangles = environment.GetComponentsInChildren<MeshFilter>().Sum(filter =>
                Enumerable.Range(0, filter.sharedMesh.subMeshCount).Sum(i => (int)filter.sharedMesh.GetIndexCount(i) / 3)) + avatarTriangles;
            if (app.sceneTriangles > NaraPractice.SceneTriangleBudget)
                throw new BuildFailedException($"NARA scene uses {app.sceneTriangles} triangles.");
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new BuildFailedException("Saving the NARA FP scene failed.");
            Debug.Log($"NARA_FP_SCENE scene={scene.name} triangles={app.sceneTriangles} audienceSeats={audience.SeatCount}");
        }

        internal static void VerifySceneDependencies()
        {
            VerifySceneDependencies(ScenePath, UniversityClassroom.ModelPath);
            VerifySceneDependencies(If101ScenePath, UniversityClassroom.If101ModelPath);
        }

        static void VerifySceneDependencies(string scenePath, string modelPath)
        {
            string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
            if (!dependencies.Contains(modelPath))
                throw new BuildFailedException($"Classroom geometry is missing from {scenePath}.");
            if (dependencies.Any(path => path.StartsWith(Ga3 + "/Models/scan_", StringComparison.OrdinalIgnoreCase)
                                       || path.Contains("KIRI_captured_surface")))
                throw new BuildFailedException("The FP scene includes a KIRI scan dependency.");
            foreach (string path in dependencies.Where(path => path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Shader shader = material ? material.shader : null;
                if (!shader || shader.name == "Hidden/InternalErrorShader")
                    throw new BuildFailedException($"The scene material at {path} has a missing shader.");
                if (ShaderUtil.ShaderHasError(shader))
                    throw new BuildFailedException($"The shader {shader.name} used by {path} has compilation errors.");
            }
        }

        internal static void Build(BuildTarget target, string path)
        {
            VerifySceneDependencies();
            FeatureHelpers.RefreshFeatures(BuildPipeline.GetBuildTargetGroup(target));
            AssetDatabase.SaveAssets();
            string company = PlayerSettings.companyName;
            string product = PlayerSettings.productName;
            FullScreenMode fullscreen = PlayerSettings.fullScreenMode;
            var http = PlayerSettings.insecureHttpOption;
            int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                PlayerSettings.companyName = "yang penting ada nama";
                PlayerSettings.productName = "NARA";
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 800;
                if (target == BuildTarget.StandaloneWindows64)
                    PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = ScenePaths,
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.DetailedBuildReport
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"NARA player build did not succeed ({report.summary.result}).");
                if (report.packedAssets.SelectMany(asset => asset.contents)
                    .Any(asset => (asset.sourceAssetPath ?? "").StartsWith(Ga3 + "/Models/scan_", StringComparison.OrdinalIgnoreCase)))
                    throw new BuildFailedException("A KIRI asset was found in the packed player.");
                Debug.Log($"NARA_FP_BUILD {target} {report.summary.totalSize} bytes {Path.GetFullPath(path)}");
            }
            finally
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                PlayerSettings.fullScreenMode = fullscreen;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.insecureHttpOption = http;
                AssetDatabase.SaveAssets();
            }
        }

        internal static Material Material(string name, int color, string shaderName = "Standard")
        {
            string path = $"{Root}/Materials/{name}.mat";
            Material item = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!item)
            {
                Shader shader = Shader.Find(shaderName);
                if (!shader)
                    throw new BuildFailedException($"Shader {shaderName} is unavailable.");
                item = new Material(shader);
                if (item.HasProperty("_Color"))
                    item.color = ColorHex(color);
                if (item.HasProperty("_Glossiness"))
                    item.SetFloat("_Glossiness", .22f);
                AssetDatabase.CreateAsset(item, path);
            }
            return item;
        }

        internal static Color ColorHex(int rgb) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        internal static Transform Shape(PrimitiveType primitive, string name, Transform parent, Vector3 position,
            Vector3 scale, Material material, bool fixedGeometry = true)
        {
            GameObject item = GameObject.CreatePrimitive(primitive);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (fixedGeometry)
                GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic);
            return item.transform;
        }

        static void Lighting(Transform room, bool if101)
        {
            Directional("Daylight", if101 ? new Vector3(48, -63, 0) : new Vector3(44, -53, 0),
                ColorHex(if101 ? 0xFFFAED : 0xFFF4E1), if101 ? .85f : .93f, LightShadows.Soft);
            Directional(if101 ? "Fluorescent fill" : "Window fill",
                if101 ? new Vector3(57, 115, 0) : new Vector3(24, 106, 0),
                ColorHex(if101 ? 0xECF1F9 : 0xE0EEFF), if101 ? .48f : .54f, LightShadows.None);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ColorHex(0xB4C4C4);
            RenderSettings.ambientEquatorColor = ColorHex(0x87918E);
            RenderSettings.ambientGroundColor = ColorHex(0x817466);
            RenderSettings.reflectionIntensity = .35f;

            var probe = new GameObject("Interior reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(room);
            probe.transform.localPosition = new Vector3(0, 1.55f, 0);
            probe.size = new Vector3(if101 ? 8.9f : 8.5f, 3.4f, if101 ? 7f : 8.3f);
            probe.boxProjection = true;
            probe.mode = ReflectionProbeMode.Custom;
            probe.resolution = 128;
            probe.hdr = true;
            probe.shadowDistance = 9f;
            probe.farClipPlane = 24f;
            probe.intensity = .56f;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = ColorHex(0xB2BDC0);
            string path = Root + (if101 ? "/Environment/IF101Reflection.exr" : "/Environment/UniversityReflection.exr");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null &&
                Lightmapping.BakeReflectionProbe(probe, path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            probe.enabled = probe.customBakedTexture;
            Debug.Log($"NARA_ROOM_REFLECTION room={(if101 ? NaraPractice.If101Scene : NaraPractice.UniversityScene)} baked={probe.enabled}");
        }

        static void Directional(string name, Vector3 rotation, Color color, float intensity, LightShadows shadow)
        {
            Light light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(rotation);
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadow;
            light.shadowStrength = .78f;
            light.shadowBias = .035f;
            light.shadowNormalBias = .26f;
        }
    }
}
