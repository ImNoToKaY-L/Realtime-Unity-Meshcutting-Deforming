using UnityEngine;
using UnityEditor;
using System.Collections;

namespace Obi
{
    public class ObiParticleEditorDrawing : MonoBehaviour
    {
        public static Mesh particlesMesh;
        public static Material particleMaterial;

        private static void CreateParticlesMesh()
        {
            if (particlesMesh == null)
            {
                particlesMesh = new Mesh();
                particlesMesh.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static void CreateParticleMaterials()
        {
            if (!particleMaterial)
            {
                particleMaterial = Resources.Load<Material>("EditorParticle");
            }
        }

        public static void DestroyParticlesMesh()
        {
            GameObject.DestroyImmediate(particlesMesh);
        }

        public static void DrawParticles(Camera cam, ObiActorBlueprint blueprint, bool[] facingCamera, bool[] selectionStatus, int[] sortedIndices)
        {
            CreateParticlesMesh();
            CreateParticleMaterials();

            if (!particleMaterial.SetPass(0))
                return;

            Vector3 camup = cam.transform.up;
            Vector3 camright = cam.transform.right;
            Vector3 camforward = cam.transform.forward;

            //because each vertex needs to be drawn as a quad.
            int particlesPerDrawcall = Constants.maxVertsPerMesh / 4;
            int drawcallCount = blueprint.positions.Length / particlesPerDrawcall + 1;
            particlesPerDrawcall = Mathf.Min(particlesPerDrawcall, blueprint.positions.Length);

            Color regularColor = ObiEditorSettings.GetOrCreateSettings().particleColor;
            Color selectedColor = ObiEditorSettings.GetOrCreateSettings().selectedParticleColor;

            int i = 0;

            for (int m = 0; m < drawcallCount; ++m)
            {

                //Draw all cloth vertices:      
                particlesMesh.Clear();
                Vector3[] vertices = new Vector3[particlesPerDrawcall * 4];
                Vector2[] uv = new Vector2[particlesPerDrawcall * 4];
                Color[] colors = new Color[particlesPerDrawcall * 4];
                int[] triangles = new int[particlesPerDrawcall * 6];

                for (int particlesDrawn = 0; i < blueprint.positions.Length && particlesDrawn < particlesPerDrawcall; ++i, ++particlesDrawn)
                {
                    int sortedIndex = sortedIndices[i];

                    // skip inactive ones:
                    if (!blueprint.IsParticleActive(sortedIndex))
                        continue;

                    int i4 = i * 4;
                    int i41 = i4 + 1;
                    int i42 = i4 + 2;
                    int i43 = i4 + 3;
                    int i6 = i * 6;

                    // get particle size in screen space:
                    float size = HandleUtility.GetHandleSize(blueprint.positions[sortedIndex]) * 0.04f;

                    // get particle color:
                    Color color = selectionStatus[sortedIndex] ? selectedColor : regularColor;
                    color.a = facingCamera[sortedIndex] ? 1 : 0.15f;

                    uv[i4] = Vector2.one;
                    uv[i41] = new Vector2(0, 1);
                    uv[i42] = Vector3.zero;
                    uv[i43] = new Vector2(1, 0);

                    vertices[i4] = blueprint.positions[sortedIndex] + camup * size + camright * size;
                    vertices[i41] = blueprint.positions[sortedIndex] + camup * size - camright * size;
                    vertices[i42] = blueprint.positions[sortedIndex] - camup * size - camright * size;
                    vertices[i43] = blueprint.positions[sortedIndex] - camup * size + camright * size;

                    colors[i4] = color;
                    colors[i41] = color;
                    colors[i42] = color;
                    colors[i43] = color;

                    triangles[i6] = i42;
                    triangles[i6 + 1] = i41;
                    triangles[i6 + 2] = i4;
                    triangles[i6 + 3] = i43;
                    triangles[i6 + 4] = i42;
                    triangles[i6 + 5] = i4;

                }

                particlesMesh.vertices = vertices;
                particlesMesh.triangles = triangles;
                particlesMesh.uv = uv;
                particlesMesh.colors = colors;

                Graphics.DrawMeshNow(particlesMesh, Matrix4x4.identity);
            }
        }

    }

}