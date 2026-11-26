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
        private readonly Vector3[] vertices;
        private readonly Vector2[] uvs;
        private readonly Color32[] colors;
        private readonly int[] triangles;
        private Vector3[] shapeDirections;
        private float[] shapeRippleValues;
        private int vertexCount;
        private int drawnBillows = -1;
        private int sortedCount = -1;
        private int frameCounter;
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
            shapeDirections = shape.ToArray();
            shapeRippleValues = shapeRipples.ToArray();
            billows = new Billow[capacity];
            depths = new float[capacity];
            order = new int[capacity];
            vertices = new Vector3[capacity * shape.Count];
            uvs = new Vector2[capacity * shape.Count];
            colors = new Color32[capacity * shape.Count];

            // Triangle and UV layout depend only on the slot, never on which billow fills it.
            triangles = new int[capacity * faces.Count];
            for (int slot = 0; slot < capacity; slot++)
            {
                int first = slot * shape.Count;
                for (int i = 0; i < faces.Count; i++)
                    triangles[slot * faces.Count + i] = first + faces[i];
                for (int i = 0; i < shape.Count; i++)
                    uvs[first + i] = shapeUv[i];
            }
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

            // Put the farthest translucent billows first for a stable layered silhouette. The camera
            // barely moves relative to the cloud between frames, so re-sorting every third frame
            // (or whenever the billow count changes) gives the same look for a third of the work.
            if (count != sortedCount || ++frameCounter % 3 == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    order[i] = i;
                    depths[i] = -(billows[i].Center - viewer).sqrMagnitude;
                }
                Array.Sort(depths, order, 0, count);
                sortedCount = count;
            }

            for (int i = 0; i < count; i++)
                AppendBillow(i, billows[order[i]], viewer, sun);

            if (count != drawnBillows)
            {
                mesh.Clear();
                drawnBillows = count;
                vertexCount = count * shape.Count;
                mesh.SetVertices(vertices, 0, vertexCount);
                mesh.SetUVs(0, uvs, 0, vertexCount);
                mesh.SetColors(colors, 0, vertexCount);
                mesh.SetTriangles(triangles, 0, count * faces.Count, 0, false);
                mesh.bounds = FixedBounds;
                return;
            }

            mesh.SetVertices(vertices, 0, vertexCount);
            mesh.SetColors(colors, 0, vertexCount);
        }

        // The cloud always stays within a few tens of kilometres of the burst, so a fixed generous
        // box saves recomputing the bounds of tens of thousands of vertices every frame.
        private static readonly Bounds FixedBounds = new Bounds(Vector3.zero, new Vector3(300000f, 300000f, 300000f));

        private void AppendBillow(int slot, Billow billow, Vector3 viewer, Vector3 sun)
        {
            int first = slot * shape.Count;
            float radius = billow.Diameter * 0.5f;
            Quaternion rotation = Quaternion.Euler(billow.Rotation * 0.29f, billow.Rotation, 0f);
            Vector3 axisX = rotation * Vector3.right;
            Vector3 axisY = rotation * Vector3.up;
            Vector3 axisZ = rotation * Vector3.forward;
            Vector3 view = (viewer - billow.Center).normalized;
            int shapeCount = shapeDirections.Length;
            for (int i = 0; i < shapeCount; i++)
            {
                Vector3 local = shapeDirections[i];
                Vector3 direction = axisX * local.x + axisY * local.y + axisZ * local.z;
                float ripple = shapeRippleValues[i];
                vertices[first + i] = billow.Center + direction * radius * ripple;

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
                colors[first + i] = new Color(red, green, blue,
                    billow.Color.a * Mathf.SmoothStep(0f, 1f, facing));
            }
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
