using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSundial
{
    internal sealed class SundialShockFront
    {
        private const int Segments = 192;
        private const float Duration = 95f;

        // Cross-section of the front, from the tail of the dust wake (0) to the leading edge (1):
        // a main condensation crest near 0.87 and a weaker secondary crest trailing near 0.43.
        private const float SecondaryCrest = 0.43f;
        private static readonly float[] Across = { 0f, 0.08f, 0.18f, 0.28f, 0.36f, 0.43f, 0.50f, 0.60f, 0.72f, 0.80f, 0.87f, 0.93f, 1f };
        private static readonly float[] Opacity = { 0f, 0.10f, 0.20f, 0.26f, 0.42f, 0.52f, 0.32f, 0.30f, 0.46f, 0.72f, 0.98f, 0.46f, 0f };
        private static readonly float[] Lift = { 0.08f, 0.12f, 0.17f, 0.22f, 0.40f, 0.52f, 0.30f, 0.30f, 0.52f, 0.82f, 1f, 0.58f, 0.13f };

        // Per-row blend factors depend only on the cross-section, so they are worked out once.
        private static readonly float[] ToneBlend = BuildToneBlend();
        private static readonly float[] CrestAmount = BuildCrestAmount();

        private static float[] BuildCrestAmount()
        {
            var result = new float[Across.Length];
            for (int row = 0; row < result.Length; row++)
                result[row] = Mathf.SmoothStep(0f, 1f, (Across[row] - 0.61f) / 0.27f);
            return result;
        }

        private static float[] BuildToneBlend()
        {
            var result = new float[Across.Length];
            for (int row = 0; row < result.Length; row++)
            {
                float crest = Mathf.SmoothStep(0f, 1f, (Across[row] - 0.61f) / 0.27f);
                float secondary = Mathf.Clamp01(1f - Mathf.Abs(Across[row] - SecondaryCrest) / 0.10f);
                result[row] = Mathf.Max(crest, 0.55f * secondary);
            }
            return result;
        }

        private readonly Vector2[] directions = new Vector2[Segments];
        private readonly float[] angles = new float[Segments];
        private readonly Vector3[] vertices = new Vector3[Segments * Across.Length];
        private readonly Color32[] colors = new Color32[Segments * Across.Length];
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly double surfaceRadius;
        private readonly float range;
        private bool observedCamera;
        private bool passedCamera;
        private float previousOffset;
        private float previousFront;
        private float lastElapsed;
        private float lastProgress;
        private float lastFront;

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
                angles[segment] = angle;
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
            float front = range * Mathf.Pow(progress, 0.62f);
            lastElapsed = elapsed;
            lastProgress = progress;
            lastFront = front;
            if (front > previousFront)
            {
                CheckCameraCrossing(front, eye, observeCamera);
                previousFront = front;
            }

            renderer.enabled = elapsed > 0f && elapsed < Duration;
            if (!renderer.enabled)
                return;

            float spread = Mathf.Sqrt(progress);
            float width = Mathf.Min(front * 0.78f, range * Mathf.Lerp(0.020f, 0.20f, spread));
            // Stays strong for most of its life and only fades toward the end.
            float opacity = Mathf.Clamp01(elapsed * 2f) * (1f - Mathf.SmoothStep(0.30f, 1f, progress)) * 0.82f;
            float rise = range * Mathf.Lerp(0.0007f, 0.018f, spread);

            // Just after the blast the dust wall is lit orange by the fireball, then cools to tan.
            float scorch = 0.85f * (1f - Mathf.SmoothStep(0f, 14f, elapsed));
            Color dust = Color.Lerp(new Color(0.69f, 0.59f, 0.46f), new Color(0.98f, 0.66f, 0.38f), scorch);
            Color condensation = Color.Lerp(new Color(0.92f, 0.92f, 0.88f), new Color(1f, 0.90f, 0.74f), scorch * 0.7f);

            for (int segment = 0; segment < Segments; segment++)
            {
                Vector2 direction = directions[segment];
                float angle = angles[segment];
                float swell = Mathf.Sin(angle * 7f + elapsed * 0.24f);
                float chop = Mathf.Sin(angle * 19f - elapsed * 0.31f);
                float leadingEdge = front + width * (0.060f * swell + 0.034f * chop);
                float wakeWidth = width * (0.92f + 0.12f * swell);
                float crestWake = 0.83f + 0.17f * swell;
                for (int row = 0; row < Across.Length; row++)
                {
                    float fraction = Across[row];
                    double arc = (leadingEdge - wakeWidth * (1f - fraction)) / surfaceRadius;
                    float horizontal = (float)(Math.Sin(arc) * surfaceRadius);
                    float height = (float)((Math.Cos(arc) - 1d) * surfaceRadius)
                        + rise * Lift[row] * (0.90f + 0.18f * swell + 0.10f * chop);
                    int index = row * Segments + segment;
                    vertices[index] = new Vector3(direction.x * horizontal, height, direction.y * horizontal);

                    float crest = CrestAmount[row];
                    float wake = 0.66f + 0.25f * Mathf.Sin(angle * 13f + fraction * 12f - elapsed * 0.35f)
                        + 0.09f * chop;
                    Color tone = Color.Lerp(dust, condensation, ToneBlend[row]);
                    tone.a = opacity * Opacity[row] * Mathf.Lerp(wake, crestWake, crest);
                    colors[index] = tone;
                }
            }
            mesh.SetVertices(vertices, 0, vertices.Length);
            mesh.colors32 = colors;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(range * 2.6f, range * 2.6f, range * 2.6f));
        }

        // Where the crest is, on the bearing toward the viewer, so a light can ride along with it.
        // strength fades from full to nothing over the front's life.
        internal bool TryGetCrest(Vector3 eye, out Vector3 position, out float width, out float strength)
        {
            position = Vector3.zero;
            width = 0f;
            strength = 0f;
            if (lastElapsed <= 0f || lastElapsed >= Duration)
                return false;

            float spread = Mathf.Sqrt(lastProgress);
            width = Mathf.Min(lastFront * 0.78f, range * Mathf.Lerp(0.020f, 0.20f, spread));
            strength = Mathf.Clamp01(lastElapsed * 2f) * (1f - Mathf.SmoothStep(0.25f, 1f, lastProgress));

            Vector2 bearing = new Vector2(eye.x, eye.z);
            bearing = bearing.sqrMagnitude < 1f ? Vector2.right : bearing.normalized;
            double arc = (lastFront - width * 0.13f) / surfaceRadius;
            float horizontal = (float)(Math.Sin(arc) * surfaceRadius);
            float height = (float)((Math.Cos(arc) - 1d) * surfaceRadius) + Mathf.Max(200f, width * 0.15f);
            position = new Vector3(bearing.x * horizontal, height, bearing.y * horizontal);
            return strength > 0.001f;
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
            // With no earlier camera sample, compare the current position to the last
            // radius so a crossing can still register on the first rendered frame.
            float offsetBefore = observedCamera ? previousOffset : cameraArc - previousFront;
            if (offset <= 0f && offsetBefore >= 0f)
            {
                passedCamera = true;
                float height = (float)(Math.Sqrt(horizontal * horizontal + radiusAtEye * radiusAtEye) - surfaceRadius);
                float heightFactor = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(Mathf.Abs(height) / (range * 0.40f)));
                float distanceFactor = Mathf.Lerp(0.92f, 0.38f, cameraArc / range);
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
