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
        private readonly List<Vector3> vertices;
        private readonly List<Vector2> uvs;
        private readonly List<Color32> colors;
        private readonly List<int> triangles;
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
            vertices = new List<Vector3>(capacity * 4);
            uvs = new List<Vector2>(capacity * 4);
            colors = new List<Color32>(capacity * 4);
            triangles = new List<int>(capacity * 6);
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

            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            for (int i = 0; i < count; i++)
            {
                Flare flare = flares[order[i]];
                int first = vertices.Count;
                float radians = flare.Rotation * Mathf.Deg2Rad;
                float cosine = Mathf.Cos(radians);
                float sine = Mathf.Sin(radians);
                Vector3 across = (right * cosine + up * sine) * flare.Diameter;
                Vector3 vertical = (up * cosine - right * sine) * flare.Diameter;

                foreach (Vector2 corner in Corners)
                {
                    vertices.Add(flare.Center + across * (corner.x - 0.5f) + vertical * (corner.y - 0.5f));
                    uvs.Add(corner);
                    colors.Add(flare.Color);
                }

                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
                triangles.Add(first);
                triangles.Add(first + 3);
                triangles.Add(first + 2);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
        }

        internal void Release()
        {
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
