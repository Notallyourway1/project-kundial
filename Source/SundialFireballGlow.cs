using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSundial
{
    internal sealed class SundialFireballGlow
    {
        private static readonly Vector2[] Corners =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
        };

        private struct Flare
        {
            public Vector3 Center;
            public float Diameter;
            public Color Color;
            public float Rotation;
        }

        private readonly Flare[] flares;
        private readonly float[] depth;
        private readonly int[] order;
        private readonly Vector3[] vertices;
        private readonly Vector2[] uvs;
        private readonly Color32[] colors;
        private readonly int[] triangles;
        private int drawnFlares = -1;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;

        internal SundialFireballGlow(Transform parent, Material material, int capacity)
        {
            var node = new GameObject("Sundial textured fireball halo");
            node.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Sundial fireball quads" };
            mesh.MarkDynamic();
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            flares = new Flare[capacity];
            depth = new float[capacity];
            order = new int[capacity];
            vertices = new Vector3[capacity * 4];
            uvs = new Vector2[capacity * 4];
            colors = new Color32[capacity * 4];
            triangles = new int[capacity * 6];

            // Triangles and UVs depend only on the slot, so they are filled in once.
            for (int slot = 0; slot < capacity; slot++)
            {
                int first = slot * 4;
                int t = slot * 6;
                triangles[t] = first;
                triangles[t + 1] = first + 2;
                triangles[t + 2] = first + 1;
                triangles[t + 3] = first;
                triangles[t + 4] = first + 3;
                triangles[t + 5] = first + 2;
                for (int k = 0; k < 4; k++)
                    uvs[first + k] = Corners[k];
            }
        }

        internal void SetFlare(int index, Vector3 center, float diameter, Color color, float rotation)
        {
            flares[index] = new Flare { Center = center, Diameter = diameter, Color = color, Rotation = rotation };
        }

        internal void Draw(int count, Vector3 eye, Vector3 right, Vector3 up)
        {
            renderer.enabled = count > 0;
            if (!renderer.enabled)
                return;

            for (int i = 0; i < count; i++)
            {
                depth[i] = -(flares[i].Center - eye).sqrMagnitude;
                order[i] = i;
            }
            Array.Sort(depth, order, 0, count);

            for (int i = 0; i < count; i++)
            {
                Flare flare = flares[order[i]];
                int first = i * 4;
                float radians = flare.Rotation * Mathf.Deg2Rad;
                float cosine = Mathf.Cos(radians);
                float sine = Mathf.Sin(radians);
                Vector3 across = (right * cosine + up * sine) * flare.Diameter;
                Vector3 vertical = (up * cosine - right * sine) * flare.Diameter;
                Color32 color = flare.Color;

                for (int k = 0; k < 4; k++)
                {
                    Vector2 corner = Corners[k];
                    vertices[first + k] = flare.Center + across * (corner.x - 0.5f) + vertical * (corner.y - 0.5f);
                    colors[first + k] = color;
                }
            }

            int vertexCount = count * 4;
            if (count != drawnFlares)
            {
                mesh.Clear();
                drawnFlares = count;
                mesh.SetVertices(vertices, 0, vertexCount);
                mesh.SetUVs(0, uvs, 0, vertexCount);
                mesh.SetColors(colors, 0, vertexCount);
                mesh.SetTriangles(triangles, 0, count * 6, 0, false);
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(300000f, 300000f, 300000f));
                return;
            }

            mesh.SetVertices(vertices, 0, vertexCount);
            mesh.SetColors(colors, 0, vertexCount);
        }

        internal void Release()
        {
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
