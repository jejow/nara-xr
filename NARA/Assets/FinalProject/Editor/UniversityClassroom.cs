using System;
using System.Collections.Generic;
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
        public const string If101ModelPath = Root + "/Environment/IF101Classroom.fbx";
        const string TwoSided = "NARA/DoubleSidedTextured";

        public static GameObject Install(Transform parent, bool if101)
        {
            string path = if101 ? If101ModelPath : ModelPath;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset)
                throw new BuildFailedException($"The classroom model at {path} has not been imported.");
            var room = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            room.name = if101 ? NaraPractice.IfClassroomName : "Kelas universitas / tevy Classroom v2";
            var renderers = room.GetComponentsInChildren<MeshRenderer>();
            int desks = renderers.Count(item => item.name.StartsWith("Table white trapezoidal top", StringComparison.Ordinal));
            int chairs = renderers.Count(item => if101 ? item.name.StartsWith("IF101 wood chair", StringComparison.Ordinal)
                                                       : item.name.Contains("padded seat"));
            int expectedDesks = if101 ? 0 : NaraAudience.ClassroomSeatCount;
            if (desks != expectedDesks || chairs != NaraAudience.ClassroomSeatCount)
                throw new BuildFailedException($"{room.name} must have {expectedDesks} desks and 24 chairs.");
            bool fittings = if101
                ? renderers.Count(item => item.name.StartsWith("Dry wipe board face", StringComparison.Ordinal)) == 2 &&
                  renderers.Count(item => item.name.StartsWith("IF101 roller blind", StringComparison.Ordinal)) == 6 &&
                  renderers.Any(item => item.name == "Projection screen backing") &&
                  renderers.All(item => !item.name.StartsWith("Teacher desk", StringComparison.Ordinal))
                : renderers.Any(item => item.name == "Presenter lectern") &&
                  renderers.Any(item => item.name == "Lectern confidence monitor") &&
                  renderers.Any(item => item.name == "Lectern microphone capsule");
            if (!fittings)
                throw new BuildFailedException($"The architectural or lectern fittings are missing from {room.name}.");

            var materials = if101 ? If101Materials() : UniversityMaterials();
            var bounds = (if101 ? renderers.First(item => item.name == "IF101 ceramic floor") : renderers[0]).bounds;
            foreach (MeshRenderer renderer in renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string source = slots[i] ? slots[i].name : "";
                    if (!materials.TryGetValue(source, out Material material))
                        throw new BuildFailedException($"Unmapped material '{source}' on {renderer.name} in {room.name}.");
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
                InstallSightBlocker(renderer);
                if (renderer.name is "ceiling" or "shell" or "wall_front" ||
                    renderer.name.StartsWith("IF101 wall", StringComparison.Ordinal) ||
                    renderer.name.StartsWith("IF101 ceiling", StringComparison.Ordinal) ||
                    renderer.name.StartsWith("IF101 roller blind", StringComparison.Ordinal))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (!if101 && renderer.name.Contains("padded seat") && !slots.Contains(materials["Nara furniture upholstery"]))
                    throw new BuildFailedException("The team upholstery is missing from " + renderer.name);
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                if (!renderer.name.StartsWith("IF101 exterior ", StringComparison.Ordinal))
                    bounds.Encapsulate(renderer.bounds);
            }
            Vector3 size = bounds.size;
            bool validSize = if101 ? size.x is >= 8.9f and <= 9.3f && size.z is >= 7f and <= 7.4f
                                  : size.x >= 8f && size.z >= 8f;
            if (!validSize || size.y is < 3.4f or > 3.8f)
                throw new BuildFailedException($"Unexpected classroom import scale {size} in {room.name}.");
            Debug.Log($"NARA_ROOM_MODEL name={room.name} meshes={renderers.Length} seats={chairs} teamDesks={desks} size={size:F2}");
            return room;
        }

        // The room FBXs intentionally ship without physics. Preserve openings in the
        // joined architecture and use inexpensive boxes for individual solid fittings.
        static void InstallSightBlocker(MeshRenderer item)
        {
            string name = item.name;
            if (name is "shell" or "wall_front" or "ceiling" or "shelves" or "door_geo")
            {
                item.gameObject.AddComponent<MeshCollider>().sharedMesh = item.GetComponent<MeshFilter>().sharedMesh;
                return;
            }
            if (name.StartsWith("IF101 wall", StringComparison.Ordinal) ||
                name.StartsWith("IF101 ceiling", StringComparison.Ordinal) ||
                name.StartsWith("IF101 roller blind", StringComparison.Ordinal) ||
                name.StartsWith("Milky window glazing", StringComparison.Ordinal) ||
                name.StartsWith("Satin window film", StringComparison.Ordinal) ||
                name.StartsWith("Orange portal", StringComparison.Ordinal) ||
                name.StartsWith("Lectern ", StringComparison.Ordinal) ||
                name.StartsWith("Table white trapezoidal top", StringComparison.Ordinal) ||
                name is "Steel storage cabinet" or "Presenter lectern" or "Ceiling projector" or
                    "Projection screen backing" or "Pull-down projection screen" or "IF101 ceramic floor")
            {
                var bounds = item.GetComponent<MeshFilter>().sharedMesh.bounds;
                var block = item.gameObject.AddComponent<BoxCollider>();
                block.center = bounds.center;
                block.size = Vector3.Max(bounds.size, Vector3.one * .001f);
            }
        }

        static Dictionary<string, Material> UniversityMaterials()
        {
            Material atlas = Surface("Classroom textured surfaces", 0xFFFFFF, .26f, "ClassroomAtlas.png");
            Normal(atlas, "ClassroomNormal.png", .73f);
            var packed = Texture("ClassroomSurface.png", TextureImporterType.Default, linear: true);
            FinishMap(atlas, packed, .86f);
            atlas.SetTexture("_OcclusionMap", packed);
            atlas.SetFloat("_OcclusionStrength", .78f);
            var backfaces = Surface("Classroom two-sided textiles", 0xFFFFFF, .09f, "ClassroomAtlas.png", shader: TwoSided);
            Normal(backfaces, "ClassroomNormal.png", .62f);
            Material trim = Surface("Classroom projection trim", 0x252C30, .28f);
            var laminate = Surface("Panel table laminate", 0xEEECE4, .36f);
            Normal(laminate, "DeskLaminateNormal.png", .25f);
            laminate.mainTextureScale = new Vector2(6f, 6f);
            return new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase)
            {
                ["classroom"] = atlas,
                ["classroom_screen"] = atlas,
                ["classroom_2side"] = backfaces,
                ["classroom_glass"] = Surface("Classroom frosted glass", 0xC4D6D8, .43f),
                ["classroom_light"] = Surface("Classroom ceiling lights", 0xEAE9DE, .32f, emission: .36f),
                ["Nara screen frame"] = trim,
                ["Nara confidence bezel"] = trim,
                ["Nara projector body"] = Surface("Classroom projector housing", 0xD2D4CD, .28f),
                ["Nara furniture laminate"] = laminate,
                ["Nara furniture steel"] = Surface("Panel powder coated steel", 0x24282B, .29f, metallic: .10f),
                ["Nara furniture rubber"] = Surface("Panel rubber feet", 0x16181B, .12f),
                ["Nara furniture upholstery"] = Surface("Panel chair upholstery", 0xFFFFFF, .11f, "PanelUpholstery.png"),
                ["Nara lectern oak"] = Surface("Lectern oak", 0xFFFFFF, .30f, "LecternOak.png"),
                ["Nara lectern graphite"] = Surface("Lectern graphite", 0x373E41, .24f),
                ["Nara reading surface"] = Surface("Fitted lectern reading surface", 0x303B3C, .17f),
                ["Nara lectern hardware"] = Surface("Lectern microphone hardware", 0x282B30, .35f, metallic: .25f)
            };
        }

        static Dictionary<string, Material> If101Materials()
        {
            var ceramic = Surface("IF101 ceramic tile", 0xFFFFFF, .49f, "IF101Floor.png");
            Normal(ceramic, "IF101FloorNormal.png", .70f);
            FinishMap(ceramic, Texture("IF101FloorSurface.png", TextureImporterType.Default, linear: true), .93f);
            var curtain = Surface("IF101 patterned curtain", 0xFFFFFF, .08f, "IF101Curtain.png", .14f, TwoSided);
            return new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase)
            {
                ["IF101 wall paint"] = Surface("IF101 wall paint", 0xFFFFFF, .16f, "IF101Paint.png"),
                ["IF101 ceiling paint"] = Surface("IF101 ceiling paint", 0xDADCCD, .06f),
                ["IF101 ceramic tile"] = ceramic,
                ["IF101 varnished wood"] = Surface("IF101 varnished wood", 0xFFFFFF, .43f, "IF101Wood.png"),
                ["IF101 patterned curtain"] = curtain,
                ["IF101 aluminum"] = Surface("IF101 aluminum", 0xA7A79D, .38f, metallic: .58f),
                ["IF101 cable tray steel"] = Surface("IF101 cable tray steel", 0x484E4B, .35f, metallic: .26f),
                ["IF101 whiteboard"] = Surface("IF101 whiteboard", 0xFFFFFF, .48f, "IF101Board.png"),
                ["IF101 entry orange"] = Surface("IF101 entry orange", 0xAD4A28, .24f),
                ["IF101 frosted glass"] = Surface("IF101 frosted glass", 0xB5C3CF, .42f),
                ["IF101 high window glass"] = Surface("IF101 high window glass", 0xC9D7CF, .42f, emission: .16f),
                ["IF101 fixture housing"] = Surface("IF101 fixture housing", 0xD0CFC1, .28f),
                ["IF101 fluorescent tubes"] = Surface("IF101 fluorescent tubes", 0xFAF9F1, .34f, emission: .77f),
                ["IF101 equipment black"] = Surface("IF101 equipment black", 0x24282A, .26f),
                ["IF101 clock red"] = Surface("IF101 clock red", 0xE33932, .21f, emission: .65f),
                ["IF101 storage cabinet"] = Surface("IF101 storage cabinet", 0x636D64, .24f),
                ["IF101 olive platform"] = Surface("IF101 olive platform", 0x323D22, .06f),
                ["IF101 courtyard clay roof"] = Surface("IF101 courtyard clay roof", 0x9B5039, .13f),
                ["IF101 courtyard paving"] = Surface("IF101 courtyard paving", 0xAD9280, .15f),
                ["IF101 courtyard foliage"] = Surface("IF101 courtyard foliage", 0x7A8C51, .10f)
            };
        }

        static Material Surface(string name, int color, float smoothness, string texture = null, float emission = 0,
            string shader = "Standard", float metallic = 0)
        {
            Material material = Material(name, color, shader);
            material.color = ColorHex(color);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (texture != null)
                material.mainTexture = Texture(texture);
            if (shader == TwoSided)
            {
                material.doubleSidedGI = true;
                material.SetFloat("_TransmissionTint", emission);
            }
            else if (emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", ColorHex(color) * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Normal(Material material, string filename, float strength)
        {
            material.SetTexture("_BumpMap", Texture(filename, TextureImporterType.NormalMap, linear: true));
            material.SetFloat("_BumpScale", strength);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        static void FinishMap(Material material, Texture2D packed, float smoothnessScale)
        {
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_GlossMapScale", smoothnessScale);
            material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material);
        }

        static Texture2D Texture(string filename, TextureImporterType type = TextureImporterType.Default, bool linear = false)
        {
            string path = Root + "/Environment/" + filename;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new BuildFailedException($"The classroom texture {filename} has not been imported.");
            if (linear && (importer.textureType != type || importer.sRGBTexture || importer.anisoLevel != 4))
            {
                importer.textureType = type;
                importer.sRGBTexture = false;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Vector3[] AudiencePoints(GameObject room)
        {
            var points = new Vector3[NaraAudience.ClassroomSeatCount];
            for (int row = 0; row < NaraAudience.Rows; row++)
            for (int column = 0; column < NaraAudience.SeatsPerRow; column++)
                points[row * NaraAudience.SeatsPerRow + column] =
                    Point(room, $"NaraAudienceSeat_R{row + 1:00}_C{column + 1:00}");
            return points;
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
