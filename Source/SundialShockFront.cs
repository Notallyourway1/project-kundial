using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSundial
{
    internal sealed class SundialShockFront
    {
        private const int Segments = 192;
        private const float Duration = 28f;

        private static readonly float[] Across = { 0f, 0.12f, 0.28f, 0.46f, 0.63f, 0.74f, 0.83f, 0.89f, 0.95f, 1f };
        private static readonly float[] Opacity = { 0f, 0.13f, 0.24f, 0.35f, 0.41f, 0.49f, 0.73f, 0.96f, 0.46f, 0f };
        private static readonly float[] Lift = { 0.09f, 0.13f, 0.18f, 0.25f, 0.38f, 0.55f, 0.87f, 1f, 0.58f, 0.13f };

        private readonly Vector2[] directions = new Vector2[Segments];
        private readonly Vector3[] vertices = new Vector3[Segments * Across.Length];
        private readonly Color32[] colors = new Color32[Segments * Across.Length];
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly double surfaceRadius;
        private readonly float range;
        private bool observedCamera;
        private bool passedCamera;
        private float previousOffset;

        internal SundialShockFront(Transform parent, Material material, double surfaceRadius, float range)
        {
            this.surfaceRadius = Math.Max(1d, surfaceRadius);
            this.range = range;
            var node = new GameObject("Sundial curved shock front");
            node.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Sundial condensation crest and dust wake", vertices = vertices };
            mesh.MarkDynamic();
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -1;
            renderer.enabled = false;

            var uv = new Vector2[vertices.Length];
            var indices = new int[(Across.Length - 1) * Segments * 6];
            int index = 0;
            for (int segment = 0; segment < Segments; segment++)
            {
                float angle = segment * (2f * Mathf.PI / Segments);
                directions[segment] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int next = (segment + 1) % Segments;
                for (int row = 0; row < Across.Length; row++)
                {
                    int a = row * Segments + segment;
                    uv[a] = new Vector2(Across[row], (float)segment / Segments);
                    if (row == Across.Length - 1)
                        continue;

                    int b = row * Segments + next;
                    indices[index++] = a;
                    indices[index++] = b;
                    indices[index++] = a + Segments;
                    indices[index++] = b;
                    indices[index++] = b + Segments;
                    indices[index++] = a + Segments;
                }
            }
            mesh.uv = uv;
            mesh.triangles = indices;
        }

        internal void Draw(float elapsed, Vector3 eye, bool observeCamera)
        {
            float progress = Mathf.Clamp01(elapsed / Duration);
            float front = range * Mathf.Pow(progress, 0.74f);
            if (elapsed < Duration)
                CheckCameraCrossing(front, eye, observeCamera);

            renderer.enabled = elapsed > 0f && elapsed < Duration;
            if (!renderer.enabled)
                return;

            float spread = Mathf.Sqrt(progress);
            float width = Mathf.Min(front * 0.78f, range * Mathf.Lerp(0.020f, 0.26f, spread));
            float opacity = Mathf.Clamp01(elapsed * 2f) * (1f - Mathf.SmoothStep(0.05f, 1f, progress)) * 0.70f;
            float rise = range * Mathf.Lerp(0.0007f, 0.023f, spread);
            Color dust = new Color(0.69f, 0.59f, 0.46f);
            Color condensation = new Color(0.92f, 0.92f, 0.88f);

            for (int segment = 0; segment < Segments; segment++)
            {
                Vector2 direction = directions[segment];
                float angle = segment * (2f * Mathf.PI / Segments);
                float swell = Mathf.Sin(angle * 7f + elapsed * 0.24f);
                float chop = Mathf.Sin(angle * 19f - elapsed * 0.31f);
                float leadingEdge = front + width * (0.060f * swell + 0.034f * chop);
                float wakeWidth = width * (0.92f + 0.12f * swell);
                for (int row = 0; row < Across.Length; row++)
                {
                    float fraction = Across[row];
                    double arc = (leadingEdge - wakeWidth * (1f - fraction)) / surfaceRadius;
                    float horizontal = (float)(Math.Sin(arc) * surfaceRadius);
                    float height = (float)((Math.Cos(arc) - 1d) * surfaceRadius)
                        + rise * Lift[row] * (0.90f + 0.18f * swell + 0.10f * chop);
                    int index = row * Segments + segment;
                    vertices[index] = new Vector3(direction.x * horizontal, height, direction.y * horizontal);

                    float crest = Mathf.SmoothStep(0f, 1f, (fraction - 0.61f) / 0.27f);
                    float wake = 0.66f + 0.25f * Mathf.Sin(angle * 13f + fraction * 12f - elapsed * 0.35f)
                        + 0.09f * chop;
                    Color tone = Color.Lerp(dust, condensation, crest);
                    tone.a = opacity * Opacity[row] * Mathf.Lerp(wake, 0.83f + 0.17f * swell, crest);
                    colors[index] = tone;
                }
            }
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.RecalculateBounds();
        }

        private void CheckCameraCrossing(float front, Vector3 eye, bool observeCamera)
        {
            if (passedCamera)
                return;
            if (!observeCamera)
            {
                observedCamera = false;
                return;
            }

            double horizontal = Math.Sqrt((double)eye.x * eye.x + (double)eye.z * eye.z);
            double radiusAtEye = surfaceRadius + eye.y;
            float cameraArc = (float)(surfaceRadius * Math.Atan2(horizontal, radiusAtEye));
            float offset = cameraArc - front;
            // previousOffset can be exactly 0 on the first frame when the camera sits directly
            // above ground zero, so treat "not yet reached" as >= 0 rather than > 0.
            if (offset <= 0f && observedCamera && previousOffset >= 0f)
            {
                passedCamera = true;
                float height = (float)(Math.Sqrt(horizontal * horizontal + radiusAtEye * radiusAtEye) - surfaceRadius);
                float heightFactor = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(Mathf.Abs(height) / (range * 0.16f)));
                float distanceFactor = Mathf.Lerp(0.92f, 0.28f, cameraArc / range);
                SundialShockShake.Play(heightFactor * distanceFactor);
            }

            observedCamera = true;
            previousOffset = offset;
        }

        internal void Release()
        {
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
