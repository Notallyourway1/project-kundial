using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSundial
{
    internal sealed class SundialCloudMesh
    {
        private const int Sides = 12;
        private const int Bands = 7;

        private struct Billow
        {
            public Vector3 Center;
            public float Diameter;
            public Color Color;
            public float Rotation;
            public float Glow;
            public float Underlight;
        }

        private static readonly Color ShadowTone = new Color(0.40f, 0.42f, 0.55f);
        private static readonly Color LitTone = new Color(1.00f, 0.95f, 0.84f);
        private static readonly Color EmberTone = new Color(1.00f, 0.45f, 0.14f);

        private readonly Billow[] billows;
        private readonly float[] depths;
        private readonly int[] order;
        private readonly List<Vector3> shape = new List<Vector3>();
        private readonly List<float> shapeRipples = new List<float>();
        private readonly List<Vector2> shapeUv = new List<Vector2>();
        private readonly List<int> faces = new List<int>();
        private readonly List<Vector3> vertices;
        private readonly List<Vector2> uvs;
        private readonly List<Color32> colors;
        private readonly List<int> triangles;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;

        internal SundialCloudMesh(Transform parent, Material material, int capacity)
        {
            var node = new GameObject("Sundial animated cloud");
            node.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Sundial cloud billows" };
            mesh.MarkDynamic();
            node.AddComponent<MeshFilter>().sharedMesh = mesh;

            renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            BuildShape();
            billows = new Billow[capacity];
            depths = new float[capacity];
            order = new int[capacity];
            vertices = new List<Vector3>(capacity * shape.Count);
            uvs = new List<Vector2>(capacity * shape.Count);
            colors = new List<Color32>(capacity * shape.Count);
            triangles = new List<int>(capacity * faces.Count);
        }

        internal void SetBillow(int index, Vector3 position, float diameter, Color color, float rotation,
            float glow = 0f, float underlight = 0f)
        {
            billows[index] = new Billow
            {
                Center = position, Diameter = diameter, Color = color, Rotation = rotation,
                Glow = glow, Underlight = underlight
            };
        }

        internal void Draw(int count, Vector3 viewer, Vector3 sun)
        {
            renderer.enabled = count > 0;
            if (count == 0)
                return;

            // Put the farthest translucent billows first for a stable layered silhouette.
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                depths[i] = -(billows[i].Center - viewer).sqrMagnitude;
            }
            Array.Sort(depths, order, 0, count);

            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            for (int i = 0; i < count; i++)
                AppendBillow(billows[order[i]], viewer, sun);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
        }

        private void AppendBillow(Billow billow, Vector3 viewer, Vector3 sun)
        {
            int first = vertices.Count;
            float radius = billow.Diameter * 0.5f;
            Quaternion rotation = Quaternion.Euler(billow.Rotation * 0.29f, billow.Rotation, 0f);
            Vector3 view = (viewer - billow.Center).normalized;
            for (int i = 0; i < shape.Count; i++)
            {
                Vector3 direction = rotation * shape[i];
                float ripple = shapeRipples[i];
                vertices.Add(billow.Center + direction * radius * ripple);
                uvs.Add(shapeUv[i]);

                float facing = Mathf.Clamp01(Vector3.Dot(direction, view));

                // Sunlit cream on the sun side, cool blue-grey in shade, lumpy relief from the ripple.
                float diffuse = Mathf.Clamp01(Vector3.Dot(direction, sun) * 0.5f + 0.5f);
                float lit = Mathf.Clamp01(diffuse * Mathf.Sqrt(diffuse) + (ripple - 1f) * 1.6f);
                if (billow.Underlight > 0f)
                    lit = Mathf.Max(lit, billow.Underlight * Mathf.Clamp01(0.35f - direction.y));

                Color shade = Color.Lerp(ShadowTone, LitTone, lit);
                float ember = billow.Glow * (1f - 0.65f * lit);
                float red = Mathf.Clamp01(billow.Color.r * shade.r + EmberTone.r * ember);
                float green = Mathf.Clamp01(billow.Color.g * shade.g + EmberTone.g * ember);
                float blue = Mathf.Clamp01(billow.Color.b * shade.b + EmberTone.b * ember);
                colors.Add(new Color(red, green, blue,
                    billow.Color.a * Mathf.SmoothStep(0f, 1f, facing)));
            }
            for (int i = 0; i < faces.Count; i++)
                triangles.Add(first + faces[i]);
        }

        private void BuildShape()
        {
            shape.Add(Vector3.up);
            shapeUv.Add(new Vector2(0.5f, 1f));
            for (int ring = 1; ring < Bands; ring++)
            {
                float fraction = (float)ring / Bands;
                float latitude = Mathf.PI * fraction;
                float height = Mathf.Cos(latitude);
                float radius = Mathf.Sin(latitude);
                for (int side = 0; side < Sides; side++)
                {
                    float turn = (float)side / Sides;
                    float angle = 2f * Mathf.PI * turn;
                    shape.Add(new Vector3(radius * Mathf.Cos(angle), height, radius * Mathf.Sin(angle)));
                    shapeUv.Add(new Vector2(turn, 1f - fraction));
                }
            }
            int bottom = shape.Count;
            shape.Add(Vector3.down);
            shapeUv.Add(new Vector2(0.5f, 0f));

            foreach (Vector3 sample in shape)
                shapeRipples.Add(1f + 0.15f * Mathf.Sin(8f * sample.x + 4f * sample.y)
                    + 0.10f * Mathf.Cos(8f * sample.z - 5f * sample.y));

            for (int side = 0; side < Sides; side++)
            {
                int next = (side + 1) % Sides;
                AddFace(0, 1 + next, 1 + side);
                for (int ring = 0; ring < Bands - 2; ring++)
                {
                    int upper = 1 + ring * Sides;
                    int lower = upper + Sides;
                    AddFace(upper + side, upper + next, lower + side);
                    AddFace(upper + next, lower + next, lower + side);
                }
                int last = 1 + (Bands - 2) * Sides;
                AddFace(bottom, last + side, last + next);
            }
        }

        private void AddFace(int a, int b, int c)
        {
            faces.Add(a);
            faces.Add(b);
            faces.Add(c);
        }

        internal void Release()
        {
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
