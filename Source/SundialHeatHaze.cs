using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSundial
{
    // A soft, wobbling shell of hot air around the fireball. It is a see-through dome whose edge
    // glows and whose middle stays clear, so the fireball shows through it. It is not a true
    // refraction effect (that needs a custom shader), just a shimmering heat glow.
    internal sealed class SundialHeatHaze
    {
        private const int Rings = 16;
        private const int Sectors = 24;

        private readonly Vector3[] directions;
        private readonly Vector3[] vertices;
        private readonly Color32[] colors;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;

        internal SundialHeatHaze(Transform parent, Material material)
        {
            int count = (Rings + 1) * (Sectors + 1);
            directions = new Vector3[count];
            vertices = new Vector3[count];
            colors = new Color32[count];

            var uvs = new Vector2[count];
            var triangles = new int[Rings * Sectors * 6];
            int index = 0;
            for (int ring = 0; ring <= Rings; ring++)
            {
                float latitude = Mathf.PI * ring / Rings;
                float height = Mathf.Cos(latitude);
                float radius = Mathf.Sin(latitude);
                for (int sector = 0; sector <= Sectors; sector++)
                {
                    float angle = 2f * Mathf.PI * sector / Sectors;
                    int i = ring * (Sectors + 1) + sector;
                    directions[i] = new Vector3(radius * Mathf.Cos(angle), height, radius * Mathf.Sin(angle));
                    uvs[i] = new Vector2((float)sector / Sectors, 1f - (float)ring / Rings);
                    if (ring < Rings && sector < Sectors)
                    {
                        int next = i + Sectors + 1;
                        triangles[index++] = i;
                        triangles[index++] = i + 1;
                        triangles[index++] = next;
                        triangles[index++] = i + 1;
                        triangles[index++] = next + 1;
                        triangles[index++] = next;
                    }
                }
            }

            var node = new GameObject("Sundial heat haze");
            node.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Sundial heat haze" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 2;
            renderer.enabled = false;
        }

        internal void Draw(Vector3 center, float radius, float alpha, Vector3 eye, float time)
        {
            renderer.enabled = alpha > 0.005f && radius > 1f;
            if (!renderer.enabled)
                return;

            Vector3 view = (eye - center).normalized;
            Color heat = Color.Lerp(new Color(1f, 0.80f, 0.52f), new Color(1f, 0.62f, 0.34f), Mathf.Clamp01(time / 8f));
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 d = directions[i];
                float ripple = 1f + 0.035f * Mathf.Sin(d.x * 9f + d.y * 6f - time * 2.1f)
                    + 0.025f * Mathf.Sin(d.z * 13f - d.y * 8f + time * 2.9f);
                vertices[i] = center + d * (radius * ripple);

                // Bright at the limb, nearly clear where we look straight through the shell.
                float limb = 1f - Mathf.Clamp01(Vector3.Dot(d, view));
                float shimmer = 0.8f + 0.2f * Mathf.Sin(d.x * 17f - d.z * 11f + time * 4.3f);
                Color c = heat;
                c.a = alpha * limb * limb * shimmer;
                colors[i] = c;
            }
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.bounds = new Bounds(center, Vector3.one * (radius * 3f));
        }

        internal void Release()
        {
            Object.Destroy(mesh);
        }
    }
}
