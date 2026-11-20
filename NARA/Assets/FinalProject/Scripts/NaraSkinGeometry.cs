#if UNITY_EDITOR
using UnityEngine;

namespace Nara.Practice
{
    // Skin in world space from the FBX bind matrices. Rocketbox meshes and their
    // armatures can use different import units, even on the same avatar.
    public sealed class NaraSkinGeometry
    {
        public readonly Transform[] Bones;
        public readonly BoneWeight[] Weights;
        public readonly Vector3[] World;
        readonly Vector3[] vertices;
        readonly Matrix4x4[] bindings, pose;

        public NaraSkinGeometry(SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer.sharedMesh;
            vertices = mesh.vertices;
            Weights = mesh.boneWeights;
            Bones = renderer.bones;
            bindings = mesh.bindposes;
            pose = new Matrix4x4[bindings.Length];
            World = new Vector3[vertices.Length];
        }

        public void Sample()
        {
            for (int i = 0; i < pose.Length; i++)
                pose[i] = Bones[i].localToWorldMatrix * bindings[i];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                BoneWeight w = Weights[i];
                Vector3 world = pose[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
                if (w.weight1 > 0) world += pose[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
                if (w.weight2 > 0) world += pose[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
                if (w.weight3 > 0) world += pose[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                World[i] = world;
            }
        }
    }
}
#endif
