using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSundial
{
    // A small batch of textured quads for the vacuum burst: camera-facing discs (flash, rings, wisps),
    // flat discs (the glow on the planet) and stretched ribbons (debris streaks). Every quad lives in
    // one mesh and one draw call. The triangles are built once; each frame only vertices and colours change.
    internal sealed class SundialBurstQuads
    {
        // Regions of the procedural atlas made by CreateAtlas.
        internal static readonly Rect GlowRegion = new Rect(0f, 0.5f, 0.5f, 0.5f);
        internal static readonly Rect RingRegion = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
        internal static readonly Rect StreakRegion = new Rect(0f, 0f, 1f, 0.5f);

        // The ring in the atlas peaks at this fraction of the quad's half-size.
        internal const float RingPeak = 0.90f;

        private readonly Vector3[] vertices;
        private readonly Vector2[] uvs;
        private readonly Color32[] colors;
        private readonly int capacity;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private int count;

        internal SundialBurstQuads(Transform parent, Material material, int capacity)
        {
            this.capacity = capacity;
            vertices = new Vector3[capacity * 4];
            uvs = new Vector2[capacity * 4];
            colors = new Color32[capacity * 4];

            var triangles = new int[capacity * 6];
            for (int i = 0; i < capacity; i++)
            {
                int v = i * 4;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var node = new GameObject("Sundial vacuum burst");
            node.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Sundial vacuum burst quads" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        internal void Begin()
        {
            count = 0;
        }

        // A disc that always faces the camera.
        internal void Billboard(Vector3 center, float radius, Rect region, Color color, Vector3 right, Vector3 up)
        {
            Quad(center - right * radius - up * radius, center + right * radius - up * radius,
                center + right * radius + up * radius, center - right * radius + up * radius, region, color);
        }

        // A disc lying flat in the local horizontal plane (the burst's own ground plane).
        internal void Flat(Vector3 center, float radius, Rect region, Color color)
        {
            Quad(center + new Vector3(-radius, 0f, -radius), center + new Vector3(radius, 0f, -radius),
                center + new Vector3(radius, 0f, radius), center + new Vector3(-radius, 0f, radius), region, color);
        }

        // A ribbon from tail to head that turns to face the camera.
        internal void Streak(Vector3 tail, Vector3 head, float halfWidth, Vector3 eye, Color color)
        {
            Vector3 axis = head - tail;
            if (axis.sqrMagnitude < 1e-4f)
                return;

            Vector3 side = Vector3.Cross(axis, eye - (head + tail) * 0.5f);
            if (side.sqrMagnitude < 1e-8f)
                return;
            side = side.normalized * halfWidth;

            // Atlas ribbon: u runs tail (0) to head (1), v across.
            Rect r = StreakRegion;
            int v = count * 4;
            if (count >= capacity)
                return;
            vertices[v] = tail - side;
            vertices[v + 1] = head - side;
            vertices[v + 2] = head + side;
            vertices[v + 3] = tail + side;
            uvs[v] = new Vector2(r.xMin, r.yMin);
            uvs[v + 1] = new Vector2(r.xMax, r.yMin);
            uvs[v + 2] = new Vector2(r.xMax, r.yMax);
            uvs[v + 3] = new Vector2(r.xMin, r.yMax);
            Color32 c = color;
            colors[v] = c;
            colors[v + 1] = c;
            colors[v + 2] = c;
            colors[v + 3] = c;
            count++;
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect r, Color color)
        {
            if (count >= capacity)
                return;

            int v = count * 4;
            vertices[v] = a;
            vertices[v + 1] = b;
            vertices[v + 2] = c;
            vertices[v + 3] = d;
            uvs[v] = new Vector2(r.xMin, r.yMin);
            uvs[v + 1] = new Vector2(r.xMax, r.yMin);
            uvs[v + 2] = new Vector2(r.xMax, r.yMax);
            uvs[v + 3] = new Vector2(r.xMin, r.yMax);
            Color32 c32 = color;
            colors[v] = c32;
            colors[v + 1] = c32;
            colors[v + 2] = c32;
            colors[v + 3] = c32;
            count++;
        }

        internal void End()
        {
            // Collapse the unused quads so they draw nothing.
            for (int v = count * 4; v < vertices.Length; v++)
            {
                vertices[v] = Vector3.zero;
                colors[v] = new Color32(0, 0, 0, 0);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.RecalculateBounds();
            renderer.enabled = count > 0;
        }

        internal void Release()
        {
            Object.Destroy(mesh);
        }

        // One 256x256 white texture whose alpha holds three shapes: a soft glow (top left), a thin
        // ring with a faint interior haze (top right) and a streak that is brightest at its head (bottom).
        internal static Texture2D CreateAtlas()
        {
            const int size = 256;
            const int half = size / 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 0f;
                    if (y >= half)
                    {
                        int lx = x % half;
                        float dx = (lx + 0.5f - half * 0.5f) / (half * 0.5f);
                        float dy = (y - half + 0.5f - half * 0.5f) / (half * 0.5f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (x < half)
                        {
                            float falloff = Mathf.Clamp01(1f - d);
                            alpha = falloff * falloff * (0.45f + 0.55f * falloff);
                        }
                        else
                        {
                            float angle = Mathf.Atan2(dy, dx);
                            float ragged = 0.80f + 0.40f * Mathf.PerlinNoise(
                                4f + 2.6f * Mathf.Cos(angle), 4f + 2.6f * Mathf.Sin(angle));
                            float inner = (d - RingPeak) / 0.050f;
                            float outer = (d - RingPeak) / 0.022f;
                            float band = Mathf.Exp(-(d < RingPeak ? inner * inner : outer * outer));
                            float haze = d < RingPeak ? 0.10f * d * d * d : 0f;
                            alpha = Mathf.Clamp01((band * ragged + haze) * Mathf.Clamp01((0.985f - d) / 0.03f));
                        }
                    }
                    else
                    {
                        float u = (x + 0.5f) / size;
                        float v = (y + 0.5f) / half;
                        float across = (v - 0.5f) / 0.13f;
                        float thin = Mathf.Exp(-across * across);
                        float tail = Mathf.Pow(u, 1.7f);
                        float headAlong = (1f - u) / 0.06f;
                        float headAcross = (v - 0.5f) / 0.30f;
                        float head = Mathf.Exp(-headAlong * headAlong - headAcross * headAcross);
                        float ends = Mathf.Clamp01(u / 0.03f) * Mathf.Clamp01((0.985f - u) / 0.03f);
                        alpha = Mathf.Clamp01(thin * tail + head) * ends * Mathf.Clamp01((1f - Mathf.Abs(2f * v - 1f)) / 0.05f);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(alpha) + 0.5f));
                }
            }

            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, true);
            return texture;
        }
    }
}
