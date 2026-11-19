using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using static Nara.Practice.Editor.NaraPracticeBuild;

namespace Nara.Practice.Editor
{
    static class UniversityClassroom
    {
        public const string ModelPath = Root + "/Environment/UniversityClassroom.fbx";
        const int FurnitureSets = 10;

        public static GameObject Install(Transform parent)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Environment/ClassroomAtlas.png");
            Texture2D upholstery = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Environment/PanelUpholstery.png");
            if (!asset || !texture || !upholstery)
                throw new BuildFailedException("The classroom model, atlas, and team chair texture are required.");

            var room = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            room.name = "Kelas universitas / tevy Classroom v2";
            Material atlas = Material("Classroom textured surfaces", 0xFFFFFF);
            Material backfaces = Material("Classroom two-sided textiles", 0xFFFFFF, "NARA/DoubleSidedTextured");
            Material glass = Material("Classroom frosted glass", 0xC4D6D8);
            Material lights = Material("Classroom ceiling lights", 0xEAE9DE);
            Material trim = Material("Classroom projection trim", 0x252C30);
            Material projector = Material("Classroom projector housing", 0xD2D4CD);
            Material lens = Material("Classroom projector lens", 0x163037);
            Material laminate = Material("Panel table laminate", 0xECECE5);
            Material steel = Material("Panel powder coated steel", 0x222528);
            Material rubber = Material("Panel rubber feet", 0x161718);
            Material piping = Material("Panel seat piping", 0x1C2435);
            Material fabric = Material("Panel chair upholstery", 0xFFFFFF);
            Material podiumBody = Material("Fitted lectern body", 0x334A5C);
            Material podiumTop = Material("Fitted lectern reading surface", 0x303B3C);
            Material podiumTrim = Material("Fitted lectern accent", 0x2C6964);
            atlas.mainTexture = backfaces.mainTexture = texture;
            fabric.mainTexture = upholstery;
            lights.EnableKeyword("_EMISSION");
            lights.SetColor("_EmissionColor", new Color(.31f, .30f, .25f));
            EditorUtility.SetDirty(atlas);
            EditorUtility.SetDirty(backfaces);
            EditorUtility.SetDirty(lights);
            EditorUtility.SetDirty(fabric);

            var renderers = room.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Count(item => item.name.Contains("padded seat")) != FurnitureSets ||
                renderers.Count(item => item.name.Contains("trapezoidal top")) != FurnitureSets ||
                !renderers.Any(item => item.name == "Presenter lectern") ||
                !renderers.Any(item => item.name == "Lectern confidence monitor"))
                throw new BuildFailedException("The imported classroom is missing team furniture or the lectern.");
            var bounds = renderers[0].bounds;
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(item =>
                    (item ? item.name : "").ToLowerInvariant() switch
                    {
                        "classroom_2side" => backfaces,
                        "classroom_glass" => glass,
                        "classroom_light" => lights,
                        "nara screen frame" => trim,
                        "nara projector body" => projector,
                        "nara projector lens" => lens,
                        "nara furniture laminate" => laminate,
                        "nara furniture steel" => steel,
                        "nara furniture rubber" => rubber,
                        "nara furniture piping" => piping,
                        "nara furniture upholstery" => fabric,
                        "nara matte navy body" => podiumBody,
                        "nara reading surface" => podiumTop,
                        "nara teal front detail" => podiumTrim,
                        "nara confidence bezel" => trim,
                        _ => atlas
                    }).ToArray();
                if (renderer.name == "ceiling" || renderer.name == "shell" || renderer.name == "wall_front")
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (renderer.name.Contains("padded seat") && !renderer.sharedMaterials.Contains(fabric))
                    throw new BuildFailedException("The team upholstery is missing from " + renderer.name);
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                bounds.Encapsulate(renderer.bounds);
            }
            if (bounds.size.x < 8f || bounds.size.z < 8f || bounds.size.y is < 3.45f or > 3.8f)
                throw new BuildFailedException($"Unexpected classroom import scale {bounds.size}.");
            Debug.Log($"NARA_ROOM_MODEL meshes={renderers.Length} furnitureSets={FurnitureSets} size={bounds.size:F2}");
            return room;
        }

        public static Vector3 Point(GameObject room, string name)
        {
            Transform marker = room.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(node => string.Equals(node.name, name, StringComparison.Ordinal));
            if (!marker)
                throw new BuildFailedException("Classroom model is missing landmark " + name);
            return marker.position;
        }
    }
}
