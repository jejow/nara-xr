using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nara.Practice.Editor
{
    static class RocketboxAudienceBuilder
    {
        const string Source = NaraPracticeBuild.Root + "/Characters/Rocketbox";
        const string Prefabs = NaraPracticeBuild.Root + "/Resources/Nara/Avatars";

        public static int BuildPrefabs()
        {
            AvatarProfile[] profiles = AvatarCatalog.Load().entries;
            if (profiles.Length != 24 || profiles.Select(item => item.id).Distinct().Count() != profiles.Length)
                throw new BuildFailedException("Expected 24 distinct Rocketbox profiles.");
            Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int[] counts = profiles.Select(BuildPrefab).OrderByDescending(value => value).ToArray();
            return counts.Take(NaraAudience.SeatCount).Sum();
        }

        static int BuildPrefab(AvatarProfile profile)
        {
            string directory = $"{Source}/{profile.id}";
            string path = $"{directory}/Actor.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model)
                throw new BuildFailedException("Missing actor asset " + path);
            var root = new GameObject(profile.label);
            try
            {
                var skinned = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                foreach (Animator animator in skinned.GetComponentsInChildren<Animator>(true))
                    animator.enabled = false;
                foreach (SkinnedMeshRenderer render in skinned.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    render.sharedMaterials = render.sharedMaterials.Select(mat => BuildMaterial(directory, mat)).ToArray();
                    render.skinnedMotionVectors = false;
                    render.shadowCastingMode = ShadowCastingMode.On;
                }
                var puppet = root.AddComponent<NaraAvatarPuppet>();
                puppet.FitToSeat(skinned.transform);
                if (puppet.Triangles is < 3000 or > 20000)
                    throw new BuildFailedException($"Invalid triangle count in {profile.id}: {puppet.Triangles}.");
                ValidateSeatedMesh(skinned, puppet, profile.id);
                if (!PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/{profile.id}.prefab"))
                    throw new BuildFailedException("Could not save avatar " + profile.id);
                Debug.Log($"NARA_AVATAR {profile.id} triangles={puppet.Triangles} face=verified raisedHand=verified");
                return puppet.Triangles;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ValidateSeatedMesh(GameObject model, NaraAvatarPuppet puppet, string id)
        {
            if (puppet.Head.position.y is < 1.0f or > 1.5f)
                throw new BuildFailedException("Seat pose head is outside expected reach in " + id);
            var baked = new Mesh();
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            float seatedWrist = puppet.RightWrist.position.y;
            try
            {
                foreach (float lift in new[] { 0f, .5f, 1f })
                {
                    puppet.PoseRightArm(lift);
                    foreach (SkinnedMeshRenderer renderer in renderers)
                    {
                        renderer.BakeMesh(baked);
                        if (baked.vertexCount == 0)
                            throw new BuildFailedException("Unable to skin the seated avatar " + id);
                        Bounds bounds = renderer.localBounds;
                        bounds.Encapsulate(baked.bounds);
                        renderer.localBounds = bounds;
                        baked.Clear();
                    }
                }
                if (puppet.RightWrist.position.y < Mathf.Max(seatedWrist + .25f, puppet.Head.position.y - .18f))
                    throw new BuildFailedException("The raised hand is too low in " + id);
            }
            finally
            {
                puppet.PoseRightArm(0);
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        static Material BuildMaterial(string directory, Material original)
        {
            if (!original)
                throw new BuildFailedException("Missing material in " + directory);
            string materialName = original.name;
            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{directory}/{materialName}_color.png");
            if (!color)
                throw new BuildFailedException($"Missing color texture for {directory}/{materialName}.");
            string path = $"{directory}/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool hasOpacity = materialName.EndsWith("_opacity", StringComparison.OrdinalIgnoreCase);
            Shader shader = Shader.Find(hasOpacity ? "NARA/Avatar Cutout" : "Standard");
            if (!shader)
                throw new BuildFailedException("Avatar surface shader is unavailable.");
            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = color;
            mat.color = Color.white;
            mat.SetFloat("_Glossiness", materialName.EndsWith("_head", StringComparison.OrdinalIgnoreCase) ? .24f : .16f);
            if (hasOpacity)
                mat.SetFloat("_Cutoff", .41f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static NaraAudience Install(Transform room, Vector3 presenter, Vector3 coach, Vector3 left, Vector3 right)
        {
            var root = new GameObject("Rocketbox audience").transform;
            root.SetParent(room, false);
            var audience = root.gameObject.AddComponent<NaraAudience>();
            Vector3[] locations = { coach, left, right };
            string[] names = { "NARA questioner", "Audience left", "Audience right" };
            audience.seats = new Transform[NaraAudience.SeatCount];
            for (int i = 0; i < locations.Length; i++)
            {
                Transform seat = new GameObject(names[i]).transform;
                seat.SetParent(root, false);
                Vector3 awayFromPresenter = Vector3.ProjectOnPlane(locations[i] - presenter, Vector3.up);
                seat.SetPositionAndRotation(locations[i], Quaternion.LookRotation(awayFromPresenter));
                audience.seats[i] = seat;
            }

            return audience;
        }
    }
}
