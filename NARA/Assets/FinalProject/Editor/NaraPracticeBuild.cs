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
        public const string ScenePath = Root + "/Scenes/NaraSeminar.unity";
        const string Ga3 = "Assets/GA3";

        [MenuItem("NARA/Final Project/Create practice scene")]
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
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                BuildScene();
            Build(BuildTarget.StandaloneWindows64, "BuildFinalProject/Windows/NARA.exe");
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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environment = new GameObject("Ruang presentasi universitas");
            Transform room = environment.transform;
            GameObject classroom = UniversityClassroom.Install(room);
            Vector3 presenter = UniversityClassroom.Point(classroom, "NaraPresenter");
            Vector3 coachSeat = UniversityClassroom.Point(classroom, "NaraCoachSeat");
            Vector3 screenCenter = UniversityClassroom.Point(classroom, "NaraScreen");
            float screenWidth = 2 * Vector3.Distance(screenCenter, UniversityClassroom.Point(classroom, "NaraScreenEdge"));
            Vector3 frontToAudience = coachSeat - screenCenter;
            Vector3 roomForward = Mathf.Abs(frontToAudience.z) > Mathf.Abs(frontToAudience.x)
                ? Mathf.Sign(frontToAudience.z) * Vector3.forward
                : Mathf.Sign(frontToAudience.x) * Vector3.right;
            Material slideSurface = Material("Slide screen", 0xFFFFFF, "Unlit/Texture");
            Transform screen = Shape(PrimitiveType.Quad, "Slide image", room, screenCenter,
                new Vector3(screenWidth, screenWidth * 9 / 16, 1), slideSurface, false);
            screen.rotation = Quaternion.LookRotation(-roomForward);
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Vector3 monitorCenter = UniversityClassroom.Point(classroom, "NaraConfidenceScreen");
            Vector3 monitorNormal = UniversityClassroom.Point(classroom, "NaraConfidenceNormal") - monitorCenter;
            Transform monitor = Shape(PrimitiveType.Quad, "Confidence slide image", room, monitorCenter,
                new Vector3(.422f, .422f * 9 / 16, 1), slideSurface, false);
            monitor.rotation = Quaternion.LookRotation(-monitorNormal);
            monitor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var deck = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Resources/Nara/Slides/slide_1.png");
            slideSurface.mainTexture = deck;
            EditorUtility.SetDirty(slideSurface);

            int avatarTriangles = RocketboxAudienceBuilder.BuildPrefabs();
            // Classroom markers use the audience's left and right; the picker uses the presenter's view.
            NaraAudience audience = RocketboxAudienceBuilder.Install(room, presenter, coachSeat,
                UniversityClassroom.Point(classroom, "NaraRightSeat"), UniversityClassroom.Point(classroom, "NaraLeftSeat"));
            var voiceObject = new GameObject("NARA voice");
            voiceObject.transform.position = coachSeat + Vector3.up * 1.26f;
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
            camera.fieldOfView = 61;
            camera.nearClipPlane = .065f;
            camera.farClipPlane = 32;
            camera.allowHDR = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorHex(0x172F34);
            camObject.AddComponent<AudioListener>();
            var panel = new GameObject("World-space controls").transform;
            panel.SetParent(position, false);
            panel.localPosition = new Vector3(0, -.18f, 1.68f);

            Directional("Soft daylight", new Vector3(44, -53, 0), ColorHex(0xFFF2D7), .99f, LightShadows.Soft);
            Directional("Window fill", new Vector3(24, 106, 0), ColorHex(0xE0EEFF), .62f, LightShadows.None);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ColorHex(0xAEBEB9);
            RenderSettings.ambientEquatorColor = ColorHex(0x81918B);
            RenderSettings.ambientGroundColor = ColorHex(0x7C7164);
            RenderSettings.reflectionIntensity = .12f;

            var app = new GameObject("NARA session").AddComponent<NaraPractice>();
            app.practiceCamera = camera;
            app.controlsAnchor = panel;
            app.room = environment;
            app.slideScreen = screen.GetComponent<MeshRenderer>();
            app.confidenceScreen = monitor.GetComponent<MeshRenderer>();
            app.desktopPitch = desktopPitch;
            app.voice = voice;
            app.audience = audience;
            app.sceneTriangles = environment.GetComponentsInChildren<MeshFilter>().Sum(filter =>
                Enumerable.Range(0, filter.sharedMesh.subMeshCount).Sum(i => (int)filter.sharedMesh.GetIndexCount(i) / 3)) + avatarTriangles;
            if (app.sceneTriangles > 150000)
                throw new BuildFailedException($"NARA scene uses {app.sceneTriangles} triangles.");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new BuildFailedException("Saving the NARA FP scene failed.");
            AssetDatabase.SaveAssets();
            VerifySceneDependencies();
            Debug.Log($"NARA_FP_SCENE triangles={app.sceneTriangles} panelSeats={NaraAudience.SeatCount} confidenceMonitor=True");
        }

        internal static void VerifySceneDependencies()
        {
            string[] dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            if (!dependencies.Contains(UniversityClassroom.ModelPath))
                throw new BuildFailedException("The university classroom is missing from the FP scene.");
            if (dependencies.Any(path => path.StartsWith(Ga3 + "/Models/scan_", StringComparison.OrdinalIgnoreCase)
                                       || path.Contains("KIRI_captured_surface")))
                throw new BuildFailedException("The FP scene includes a KIRI scan dependency.");
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
                    scenes = new[] { ScenePath },
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

        static void Directional(string name, Vector3 rotation, Color color, float intensity, LightShadows shadow)
        {
            Light light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(rotation);
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadow;
            light.shadowStrength = .78f;
        }
    }
}
