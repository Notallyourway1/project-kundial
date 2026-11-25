using UnityEngine;

namespace ProjectSundial
{
    public sealed class SundialDetonationEffect : MonoBehaviour
    {
        private const int FirePuffs = 14;
        private const int ColumnPuffs = 120;
        private const int CapPuffs = 200;
        private const int CrownPuffs = 110;
        private const int UndersidePuffs = 110;
        private const int DustPuffs = 30;
        private const float AtmosphereDuration = 80f;
        private const float CloudFadeStart = 62f;
        private const float CloudGrowthTime = 40f;
        private const float CapRadiusRatio = 1.25f;

        // The ground shock front travels this many times the blast radius before it fades.
        private const float ShockRangeScale = 1.7f;

        // The vacuum burst was tuned when visualRadius was 8500 m; keep that size now that it is larger.
        private const float VacuumSizeScale = 8500f / 12000f;

        // Cloud height above the burst in stock Kerbin conditions.
        private const float KerbinCloudTop = 11000f;
        private const float EarlyCloudTop = 2800f;
        private const float EarlyCapRadius = 1350f;
        private const float FireballPeakTime = 5f;
        private const float CloudHandoffStart = 6f;
        private const float CloudHandoffEnd = 16f;

        // Detonation above the atmosphere: no fireball dome or mushroom cloud. A white flash, two
        // expanding shock rings, streaks of hot debris and a few thin wisps instead.
        private const float VacuumDuration = 30f;
        private const int VacuumStreaks = 56;
        private const int VacuumWisps = 14;
        private const int VacuumParticles = VacuumStreaks + VacuumWisps;
        private const int BurstQuadCapacity = VacuumParticles + 6;
        private const float VacuumRingTime = 12f;
        private const float VacuumSecondRingTime = 14f;

        private static readonly string[] AdditiveShaderNames =
        {
            "Legacy Shaders/Particles/Additive",
            "KSP/Particles/Additive",
            "Mobile/Particles/Additive"
        };

        private static readonly string[] ShaderNames =
        {
            "Sprites/Default",
            "Legacy Shaders/Particles/Alpha Blended",
            "KSP/Particles/Alpha Blended"
        };

        private CelestialBody body;
        private double latitude;
        private double longitude;
        private double altitude;
        private bool inAtmosphere;
        private float age;
        private float visualRadius;
        private float fireballRadius;
        private float cloudTopRise;
        private float veilAlpha;

        private Light flash;
        private Transform fireballCore;
        private Material fireballMaterial;
        private Texture2D fireballTexture;
        private Mesh fireballMesh;
        private Vector3[] fireballSphere;
        private Vector3[] fireballVertices;
        private float[] fireballMottle;
        private Color32[] fireballPixels;
        private readonly Color32[] fireballGradient = new Color32[256];
        private int fireballColorStep = -1;
        private SundialCloudMesh cloud;
        private SundialFireballGlow glow;
        private Material glowMaterial;
        private Texture2D glowTexture;
        private Vector4[] distribution;
        private SundialShockFront shockFront;
        private Material cloudMaterial;
        private SundialBurstQuads burst;
        private Material burstMaterial;
        private Texture2D burstTexture;
        private float groundDrop;
        private bool lightsAtmosphere;
        private bool shaken;

        internal static bool IsVacuum(CelestialBody body, double altitude)
        {
            return body == null || !body.atmosphere || altitude >= body.atmosphereDepth;
        }

        internal static SundialDetonationEffect Spawn(Vector3 center, CelestialBody body, float visualRadius, float fireballRadius)
        {
            if (body == null)
                return null;

            var root = new GameObject("Project Sundial detonation");
            var effect = root.AddComponent<SundialDetonationEffect>();
            effect.body = body;
            effect.latitude = body.GetLatitude(center);
            effect.longitude = body.GetLongitude(center);
            effect.altitude = body.GetAltitude(center);
            effect.inAtmosphere = !IsVacuum(body, effect.altitude);
            effect.visualRadius = Mathf.Max(100f, visualRadius);
            effect.fireballRadius = Mathf.Max(1f, fireballRadius);
            if (effect.inAtmosphere)
                effect.cloudTopRise = (float)System.Math.Min(KerbinCloudTop, 0.9d * (body.atmosphereDepth - effect.altitude));
            else if (body.atmosphere && effect.altitude < 4d * body.atmosphereDepth)
            {
                // The flash lights the air and ground below the burst: remember how far down that is.
                double ground = System.Math.Max(0d, body.TerrainAltitude(effect.latitude, effect.longitude, false));
                effect.groundDrop = (float)System.Math.Max(0d, effect.altitude - ground);
                effect.lightsAtmosphere = effect.groundDrop > 0f;
            }
            effect.UpdateAnchor();
            effect.InitializeVisuals();
            effect.RenderFrame();
            return effect;
        }

        private void UpdateAnchor()
        {
            Vector3d center = body.GetWorldSurfacePosition(latitude, longitude, altitude);
            Vector3 up = (Vector3)(center - body.position).normalized;
            transform.SetPositionAndRotation((Vector3)center, Quaternion.FromToRotation(Vector3.up, up));
        }

        private void InitializeVisuals()
        {
            flash = gameObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.94f, 0.80f);
            flash.range = Mathf.Min(12000f, visualRadius);
            flash.shadows = LightShadows.None;

            if (inAtmosphere)
                InitializeFireballCore();

            Shader shader = FindShader();
            if (shader == null)
            {
                Debug.LogWarning("[ProjectSundial] No usable transparency shader found; smoke and shock fronts are unavailable.");
                return;
            }

            if (!inAtmosphere)
            {
                // Additive blending makes the flash, rings and streaks glow; fall back to plain alpha.
                Shader additive = FindShader(AdditiveShaderNames) ?? shader;
                burstTexture = SundialBurstQuads.CreateAtlas();
                burstMaterial = new Material(additive) { mainTexture = burstTexture };
                burst = new SundialBurstQuads(transform, burstMaterial, BurstQuadCapacity);
                InitializeDistribution(VacuumParticles);
                return;
            }

            cloudMaterial = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            glowTexture = CreateGlowTexture();
            glowMaterial = new Material(shader) { mainTexture = glowTexture };
            glow = new SundialFireballGlow(transform, glowMaterial, FirePuffs);
            InitializeCloud();
            shockFront = new SundialShockFront(transform, cloudMaterial, body.Radius + altitude, visualRadius * ShockRangeScale);
        }

        private void InitializeFireballCore()
        {
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("KSP/Unlit");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Unlit/Color");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[ProjectSundial] No usable unlit shader found for the fireball core.");
                return;
            }

            fireballMaterial = new Material(shader);
            if (fireballMaterial.HasProperty("_MainTex"))
            {
                fireballTexture = CreateFireballTexture();
                fireballMaterial.SetTexture("_MainTex", fireballTexture);
            }
            else if (fireballMaterial.HasProperty("_Color"))
            {
                fireballMaterial.SetColor("_Color", new Color(1f, 0.93f, 0.68f));
            }

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Luminous fireball";
            core.transform.SetParent(transform, false);
            Collider collider = core.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            MeshRenderer renderer = core.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = fireballMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            fireballCore = core.transform;
            fireballMesh = core.GetComponent<MeshFilter>().mesh;
            fireballMesh.MarkDynamic();
            fireballSphere = fireballMesh.vertices;
            fireballVertices = new Vector3[fireballSphere.Length];
        }

        private Texture2D CreateFireballTexture()
        {
            const int width = 192;
            const int height = 96;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            fireballMottle = new float[width * height];
            fireballPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float phi = Mathf.PI * y / (height - 1f);
                float ring = Mathf.Sin(phi);
                float sy = Mathf.Cos(phi);
                for (int x = 0; x < width; x++)
                {
                    float theta = 2f * Mathf.PI * x / (width - 1f);
                    float sx = ring * Mathf.Cos(theta);
                    float sz = ring * Mathf.Sin(theta);
                    float coarse = Mathf.PerlinNoise(5f + 3.9f * (sx + 0.4f * sy), 5f + 3.9f * sz);
                    float detail = Mathf.PerlinNoise(13f + 16f * sz, 13f + 16f * (sy + 0.3f * sx));

                    // Bright veins along the noise zero-crossings give the cellular, convecting surface.
                    float vein = 1f - Mathf.Abs(2f * coarse - 1f);
                    vein = vein * vein * vein;
                    fireballMottle[y * width + x] = Mathf.Clamp01(0.45f * coarse + 0.20f * detail + 0.45f * vein);
                }
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private void AnimateFireballSurface(float cooling, float handoff)
        {
            for (int i = 0; i < fireballSphere.Length; i++)
            {
                Vector3 vertex = fireballSphere[i];
                float variation = FireballSurfaceScale(vertex, age, handoff);
                fireballVertices[i] = vertex * variation;
            }
            fireballMesh.vertices = fireballVertices;
            fireballMesh.RecalculateBounds();

            int step = Mathf.FloorToInt(age * 12f);
            if (step == fireballColorStep)
                return;
            fireballColorStep = step;

            Color edge = Color.Lerp(new Color(1f, 0.78f, 0.40f), new Color(0.93f, 0.50f, 0.16f), cooling);
            Color hottest = Color.Lerp(new Color(1f, 0.99f, 0.93f), new Color(1f, 0.88f, 0.55f), cooling);
            if (fireballTexture == null)
            {
                if (fireballMaterial.HasProperty("_Color"))
                    fireballMaterial.SetColor("_Color", hottest);
                return;
            }

            // The colour depends only on brightness, so look it up in a 256-entry gradient.
            for (int g = 0; g < fireballGradient.Length; g++)
                fireballGradient[g] = Color.Lerp(edge, hottest, g / 255f);
            float baseBrightness = Mathf.Lerp(0.88f, 0.38f, cooling);
            float contrast = Mathf.Lerp(0.75f, 1.44f, cooling);
            for (int i = 0; i < fireballPixels.Length; i++)
            {
                float brightness = Mathf.Clamp01(baseBrightness + (fireballMottle[i] - 0.48f) * contrast);
                fireballPixels[i] = fireballGradient[(int)(brightness * 255f + 0.5f)];
            }
            fireballTexture.SetPixels32(fireballPixels);
            fireballTexture.Apply(false, false);
        }

        private static float FireballSurfaceScale(Vector3 vertex, float time, float handoff)
        {
            float phase = time * 1.35f + vertex.y * 4f;
            float broad = Mathf.Sin(vertex.x * 13f + vertex.z * 7f - phase)
                * Mathf.Cos(vertex.y * 11f - vertex.z * 9f + phase * 0.83f);
            float small = Mathf.Sin(vertex.x * 29f - vertex.y * 24f + vertex.z * 17f + phase * 1.22f);
            float swell = Mathf.Clamp01(time / 2f) * (1f - handoff * 0.31f);
            return 1f + (0.095f * broad + 0.038f * small) * swell;
        }

        private float FireballCooling()
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 2.5f) / (CloudHandoffEnd - 2.5f)));
        }

        private float FireballCenterHeight(float radius, float capCenter, float handoff)
        {
            float lift = Mathf.SmoothStep(0.06f, 0.30f, Mathf.Clamp01(age / FireballPeakTime));
            return Mathf.Lerp(radius * lift, capCenter, handoff);
        }

        private void RenderFireballCore()
        {
            if (fireballCore == null || !fireballCore.gameObject.activeSelf)
                return;
            if (age >= CloudHandoffEnd)
            {
                fireballCore.gameObject.SetActive(false);
                return;
            }

            float radius = FireballVisualRadius();
            float handoff = FireballToCloudBlend();
            CloudCap cap = GetCloudCap();
            float rise = FireballCenterHeight(radius, cap.CenterHeight, handoff);
            float collapse = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((age - 12f) / (CloudHandoffEnd - 12f)));
            float width = Mathf.Lerp(1.80f * radius, 1.40f * cap.Radius, handoff) * collapse;
            float height = Mathf.Lerp(1.80f * radius, 0.54f * cap.Radius, handoff) * collapse;
            fireballCore.localPosition = Vector3.up * rise;
            fireballCore.localScale = new Vector3(width, height, width);
            AnimateFireballSurface(FireballCooling(), handoff);
        }

        private float FireballToCloudBlend()
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                (age - CloudHandoffStart) / (CloudHandoffEnd - CloudHandoffStart)));
        }

        private struct CloudCap
        {
            public float Top;
            public float Radius;
            public float Depth;
            public float CenterHeight => Top - Depth * 0.5f;
        }

        private CloudCap GetCloudCap()
        {
            float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 7f) / CloudGrowthTime));
            float top = Mathf.Lerp(Mathf.Min(EarlyCloudTop, cloudTopRise), cloudTopRise, growth);

            // A broad, flat mushroom: the cap ends up wider than the cloud is tall.
            float radius = Mathf.Min(Mathf.Lerp(EarlyCapRadius, cloudTopRise * CapRadiusRatio, growth), top * CapRadiusRatio);
            float depth = Mathf.Min(radius * 0.55f, top * 0.5f);
            return new CloudCap { Top = top, Radius = radius, Depth = depth };
        }

        private float FireballVisualRadius()
        {
            float peakRadius = inAtmosphere ? fireballRadius : fireballRadius * 0.11f;
            float timeToPeak = inAtmosphere ? FireballPeakTime : 2.5f;
            float expansion = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / timeToPeak), 2.4f);
            return Mathf.Lerp(peakRadius * 0.015f, peakRadius, expansion);
        }

        private static Shader FindShader()
        {
            return FindShader(ShaderNames);
        }

        private static Shader FindShader(string[] names)
        {
            foreach (string name in names)
            {
                Shader shader = Shader.Find(name);
                if (shader != null && shader.isSupported)
                    return shader;
            }

            return null;
        }

        private void InitializeCloud()
        {
            int capacity = FirePuffs + ColumnPuffs + CapPuffs + CrownPuffs + UndersidePuffs + DustPuffs;
            cloud = new SundialCloudMesh(transform, cloudMaterial, capacity - FirePuffs);
            InitializeDistribution(capacity);
        }

        private void InitializeDistribution(int capacity)
        {
            distribution = new Vector4[capacity];
            var random = new System.Random(1954);
            for (int i = 0; i < capacity; i++)
            {
                float angle = (float)random.NextDouble() * 2f * Mathf.PI;
                distribution[i] = new Vector4(
                    Mathf.Cos(angle), (float)random.NextDouble(),
                    Mathf.Sin(angle), (float)random.NextDouble());
            }

        }

        private static Texture2D CreateGlowTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var colors = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float distance = 2f * Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                    float noise = 0.58f * Mathf.PerlinNoise(9f * u, 9f * v)
                        + 0.42f * Mathf.PerlinNoise(21f * u + 5f, 21f * v);
                    float alpha = Mathf.Clamp01((1f - distance) * 3.2f)
                        * Mathf.Lerp(0.35f, 0.94f, 1f - distance) * (0.35f + 0.65f * noise);
                    byte shade = (byte)Mathf.Lerp(151f, 255f, noise);
                    colors[y * size + x] = new Color32(shade, shade, shade, (byte)(255f * alpha));
                }
            }

            texture.SetPixels32(colors);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, true);
            return texture;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            float duration = inAtmosphere ? AtmosphereDuration : VacuumDuration;
            if (body == null || age >= duration)
            {
                Destroy(gameObject);
                return;
            }

            // Use geographic coordinates so KSP floating-origin shifts do not move the blast site.
            UpdateAnchor();
            RenderFrame();
        }

        private void RenderFrame()
        {
            if (flash == null)
                return;

            float glare = 1f - Mathf.Clamp01(age / (inAtmosphere ? 1.4f : 0.9f));
            flash.intensity = (inAtmosphere ? 9f : 14f) * glare * glare;
            flash.enabled = glare > 0f;

            RenderFireballCore();

            var flightCamera = FlightCamera.fetch;
            Camera camera = flightCamera != null ? flightCamera.mainCamera : Camera.main;
            Vector3 eye = camera != null ? transform.InverseTransformPoint(camera.transform.position) : Vector3.back * visualRadius;
            var manager = CameraManager.Instance;
            bool flightView = camera != null && manager != null && manager.currentCameraMode == CameraManager.CameraMode.Flight;
            UpdateFlashVeil(eye, flightView);

            Vector3 right = camera != null ? transform.InverseTransformDirection(camera.transform.right) : Vector3.right;
            Vector3 up = camera != null ? transform.InverseTransformDirection(camera.transform.up) : Vector3.up;
            if (!inAtmosphere)
            {
                if (burst != null)
                    RenderVacuumBurst(eye, right, up);
                if (flightView && !shaken)
                    JoltCamera(eye);
                return;
            }

            if (glow == null)
                return;

            glow.Draw(RenderFireball(), eye, right, up);
            cloud.Draw(RenderAtmosphericCloud(0), eye, LocalSunDirection());
            shockFront.Draw(age, eye, flightView);
        }

        // There is no air to carry a shock wave, so this is only a brief jolt from the flash,
        // strongest for a camera close to the burst.
        private void JoltCamera(Vector3 eye)
        {
            shaken = true;
            float closeness = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(eye.magnitude / (visualRadius * VacuumSizeScale * 3f)));
            SundialShockShake.Play(0.55f * closeness, 0.7f, 1.2f);
        }

        private Vector3 LocalSunDirection()
        {
            Vector3 fallback = new Vector3(0.6f, 0.65f, 0.3f).normalized;
            if (FlightGlobals.Bodies == null || FlightGlobals.Bodies.Count == 0 || FlightGlobals.Bodies[0] == null)
                return fallback;

            Vector3d toSun = FlightGlobals.Bodies[0].position - (Vector3d)transform.position;
            if (toSun.sqrMagnitude < 1d)
                return fallback;

            return transform.InverseTransformDirection((Vector3)toSun.normalized).normalized;
        }

        // The opening flash washes out the screen for a viewer close to the burst.
        private void UpdateFlashVeil(Vector3 eye, bool flightView)
        {
            if (!flightView)
            {
                veilAlpha = 0f;
                return;
            }

            float reach = Mathf.Max(1000f, fireballRadius * 1.5f);
            float proximity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(eye.magnitude / reach));
            float rise = inAtmosphere ? 0.08f : 0.04f;
            float fall = inAtmosphere ? 1.6f : 0.5f;
            float pulse = Mathf.Clamp01(age / rise)
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.15f) / fall)));
            veilAlpha = (inAtmosphere ? 0.9f : 0.8f) * proximity * pulse;
        }

        private void OnGUI()
        {
            if (veilAlpha <= 0.01f || Event.current.type != EventType.Repaint)
                return;

            Color previous = GUI.color;
            GUI.depth = -1000;
            GUI.color = inAtmosphere
                ? new Color(1f, 0.97f, 0.88f, veilAlpha)
                : new Color(0.88f, 0.93f, 1f, veilAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Vector3 SphereDirection(Vector4 puff)
        {
            float y = 2f * puff.y - 1f;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(puff.x * ring, y, puff.z * ring);
        }

        private void RenderVacuumBurst(Vector3 eye, Vector3 right, Vector3 up)
        {
            // Keep the burst readable from far away: its size never drops below a fixed
            // fraction of the view distance.
            float eyeDistance = Mathf.Max(1f, eye.magnitude);
            float burstRadius = visualRadius * VacuumSizeScale;
            float shellRadius = Mathf.Max(burstRadius * 0.55f, Mathf.Min(eyeDistance * 0.075f, burstRadius * 6f));
            float t = age;

            burst.Begin();

            // Light thrown onto the air and ground under the burst.
            if (lightsAtmosphere)
            {
                float lit = Mathf.Clamp01(t / 0.25f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.5f) / 8f)));
                Color glowOnGround = Color.Lerp(new Color(0.60f, 0.76f, 1f), new Color(0.78f, 0.62f, 1f), Mathf.Clamp01(t / 5f));
                glowOnGround.a = 0.38f * lit;
                burst.Flat(Vector3.down * groundDrop, shellRadius * 3.2f, SundialBurstQuads.GlowRegion, glowOnGround);
            }

            // Wide, soft bloom around the burst.
            float bloomFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 2.8f));
            Color bloom = Color.Lerp(new Color(0.80f, 0.90f, 1f), new Color(0.72f, 0.60f, 1f), Mathf.Clamp01(t / VacuumRingTime));
            bloom.a = 0.55f * bloomFade * Mathf.Clamp01(t / 0.05f);
            burst.Billboard(Vector3.zero, shellRadius * (0.9f + 0.9f * Mathf.Clamp01(t / 2.5f)),
                SundialBurstQuads.GlowRegion, bloom, right, up);

            // Hot core: a white flash that warms and shrinks as it dies.
            float coreAge = Mathf.Clamp01(t / 1.5f);
            Color core = Color.Lerp(Color.white, new Color(1f, 0.90f, 0.72f), coreAge);
            core.a = (1f - coreAge) * (1f - coreAge) * Mathf.Clamp01(t / 0.03f);
            burst.Billboard(Vector3.zero, Mathf.Max(60f, shellRadius * 0.55f * (1f - 0.5f * coreAge)),
                SundialBurstQuads.GlowRegion, core, right, up);

            // Main shock ring: blue-white at first, fading toward violet.
            float ringProgress = Mathf.Clamp01(t / VacuumRingTime);
            float ringRadius = shellRadius * (1f - Mathf.Pow(1f - ringProgress, 2.4f));
            Color ring = Color.Lerp(new Color(0.78f, 0.88f, 1f), new Color(0.80f, 0.58f, 1f), Mathf.Clamp01(t / 14f));
            ring.a = 0.95f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 2f) / 17f))) * Mathf.Clamp01(t / 0.05f);
            if (ringRadius > 1f)
                burst.Billboard(Vector3.zero, ringRadius / SundialBurstQuads.RingPeak,
                    SundialBurstQuads.RingRegion, ring, right, up);

            // A fainter second ring trails the first.
            float lag = t - 0.4f;
            if (lag > 0f)
            {
                float lagProgress = Mathf.Clamp01(lag / VacuumSecondRingTime);
                float lagRadius = shellRadius * 0.78f * (1f - Mathf.Pow(1f - lagProgress, 2.4f));
                Color second = new Color(0.82f, 0.62f, 1f);
                second.a = 0.45f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((lag - 1f) / 14f)));
                if (lagRadius > 1f)
                    burst.Billboard(Vector3.zero, lagRadius / SundialBurstQuads.RingPeak,
                        SundialBurstQuads.RingRegion, second, right, up);
            }

            // Debris streaks: white-hot fragments flying straight out, cooling to dark red.
            float streakWidth = Mathf.Max(shellRadius * 0.012f, eyeDistance * 0.0012f);
            for (int i = 0; i < VacuumStreaks; i++)
            {
                Vector4 puff = distribution[i];
                float reach = shellRadius * (0.35f + 0.60f * puff.w);
                float life = Mathf.Clamp01(t / (10f + 4f * puff.y));
                float travel = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / (12f + 6f * puff.y)), 2.4f);
                float headDistance = reach * travel;
                if (headDistance < 1f)
                    continue;

                float length = reach * (0.04f + 0.24f * Mathf.Sqrt(1f - travel));
                Vector3 direction = SphereDirection(puff);
                Vector3 head = direction * headDistance;
                Vector3 tail = direction * Mathf.Max(0f, headDistance - length);
                Color heat = Color.Lerp(new Color(1f, 0.97f, 0.88f), new Color(1f, 0.55f, 0.18f), Mathf.Clamp01(life * 2f));
                heat = Color.Lerp(heat, new Color(0.55f, 0.14f, 0.08f), Mathf.Clamp01(life * 2f - 1f));
                heat.a = 0.9f * (1f - Mathf.SmoothStep(0.55f, 1f, life)) * Mathf.Clamp01(t / 0.08f);
                burst.Streak(tail, head, streakWidth * (0.7f + 0.6f * puff.y), eye, heat);
            }

            // A few thin, warm wisps linger where the payload was vaporised.
            float wispFade = Mathf.Clamp01((t - 0.3f) / 1.5f)
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 6f) / (VacuumDuration - 6f))));
            for (int i = 0; i < VacuumWisps; i++)
            {
                Vector4 puff = distribution[VacuumStreaks + i];
                float spread = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / VacuumDuration), 2f);
                Vector3 position = SphereDirection(puff) * (shellRadius * 0.24f * (0.3f + 0.7f * puff.w) * spread);
                Color wisp = Color.Lerp(new Color(1f, 0.62f, 0.30f), new Color(0.55f, 0.28f, 0.22f), Mathf.Clamp01(t / 9f));
                wisp.a = 0.13f * wispFade;
                burst.Billboard(position, shellRadius * (0.10f + 0.08f * puff.y) * (0.6f + 0.8f * spread),
                    SundialBurstQuads.GlowRegion, wisp, right, up);
            }

            burst.End();
        }

        private int RenderFireball()
        {
            float duration = CloudHandoffEnd;
            if (age >= duration)
                return 0;

            float timeToPeak = inAtmosphere ? FireballPeakTime : 2.5f;
            float radius = FireballVisualRadius();
            float heat = Mathf.Clamp01((age - timeToPeak * 0.55f) / (duration - timeToPeak * 0.55f));
            float fade = 1f - Mathf.Clamp01((age - timeToPeak) / (duration - timeToPeak));
            float horizontal = radius;
            float vertical = radius;
            var center = Vector3.zero;
            if (inAtmosphere)
            {
                float handoff = FireballToCloudBlend();
                CloudCap cap = GetCloudCap();
                center = Vector3.up * FireballCenterHeight(radius, cap.CenterHeight, handoff);
                heat = FireballCooling();
                horizontal = Mathf.Lerp(radius, cap.Radius * 0.93f, handoff);
                vertical = Mathf.Lerp(radius, cap.Radius * 0.36f, handoff);
                fade = 1f - handoff;
            }

            // The halo gathers into the same volume as the growing smoke cap.
            var outerGlow = Color.Lerp(new Color(1f, 0.92f, 0.69f), new Color(0.95f, 0.60f, 0.28f), heat);
            outerGlow.a = fade * 0.92f;
            glow.SetFlare(0, center, 2f * Mathf.Sqrt(horizontal * vertical), outerGlow, 0f);

            Color color = Color.Lerp(new Color(1f, 0.99f, 0.91f), new Color(0.95f, 0.66f, 0.34f), heat);
            color.a = fade;
            for (int i = 1; i < FirePuffs; i++)
            {
                Vector4 puff = distribution[i];
                float spread = horizontal * puff.w * 0.53f;
                float rotation = age * (0.37f + 0.14f * puff.w);
                float sine = Mathf.Sin(rotation);
                float cosine = Mathf.Cos(rotation);
                float drift = 1f + 0.16f * Mathf.Sin(age * 1.15f + puff.y * Mathf.PI * 2f);
                var offset = new Vector3((puff.x * cosine - puff.z * sine) * spread * drift,
                    (puff.y - 0.5f) * vertical * 0.6f,
                    (puff.x * sine + puff.z * cosine) * spread * drift);
                glow.SetFlare(i, center + offset, vertical * (0.36f + 0.35f * puff.y), color, puff.w * 360f);
            }

            return FirePuffs;
        }

        private int RenderAtmosphericCloud(int count)
        {
            float density = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 8f) / 10f))
                * (1f - Mathf.Clamp01((age - CloudFadeStart) / (AtmosphereDuration - CloudFadeStart)));
            if (density <= 0f)
                return count;

            CloudCap cap = GetCloudCap();
            float top = cap.Top;
            float capRadius = cap.Radius;
            float capDepth = cap.Depth;
            float capBase = top - capDepth;
            float flowTime = Mathf.Max(0f, age - 5f);
            float dustDensity = density * (1f - Mathf.Clamp01((age - 20f) / 20f));
            float outwardSpread = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - CloudHandoffEnd) / (CloudGrowthTime * 0.7f)));
            float emberFade = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((age - 10f) / 50f));
            var stemAlbedo = new Color(0.62f, 0.40f, 0.30f);
            var stemTop = new Color(0.80f, 0.62f, 0.50f);
            var capAlbedo = new Color(1.00f, 0.98f, 0.94f);
            var undersideAlbedo = new Color(1.00f, 0.86f, 0.68f);

            // Dark reddish stem with a flared foot and a collar where it meets the cap.
            for (int i = 0; i < ColumnPuffs; i++)
            {
                Vector4 puff = distribution[FirePuffs + i];
                float level = Mathf.Repeat((i + 0.5f) / ColumnPuffs + flowTime * (0.020f + 0.010f * puff.w), 1f);
                float angle = Mathf.Atan2(puff.z, puff.x) + level * 1.6f + flowTime * 0.10f;
                float foot = (1f - level) * (1f - level);
                foot *= foot;
                float collar = level * level * level;
                collar *= collar;
                float profile = 0.15f + 0.15f * foot + 0.07f * collar;
                float spread = capRadius * profile * Mathf.Sqrt(puff.w);
                float shear = capRadius * 0.03f * level * (Mathf.Sin(level * 8f - flowTime * 0.30f) + 1f);
                var position = new Vector3(Mathf.Cos(angle) * spread + shear,
                    (capBase + capDepth * 0.12f) * level, Mathf.Sin(angle) * spread);
                Color color = Color.Lerp(stemAlbedo, stemTop, level * 0.4f);
                color.a = density * (0.80f + 0.15f * Mathf.Sin(level * Mathf.PI));
                float size = capRadius * (0.17f + 0.11f * puff.y + 0.08f * foot);
                WritePuff(count++, position, size, color, puff.w * 360f + flowTime * 8f,
                    emberFade * (0.30f + 0.55f * puff.y), 0f);
            }

            // Rolling rim: a thick torus of cream billows with glowing hot patches between them.
            for (int i = 0; i < CapPuffs; i++)
            {
                Vector4 puff = distribution[FirePuffs + ColumnPuffs + i];
                float azimuth = Mathf.Atan2(puff.z, puff.x) + flowTime * (0.020f + 0.020f * puff.y);
                float roll = puff.y * 2f * Mathf.PI - flowTime * (0.22f + 0.08f * puff.w);
                float lobe = 0.92f + 0.08f * Mathf.Sin(azimuth * 5f + flowTime * 0.15f);
                float cosRoll = Mathf.Cos(roll);
                float sinRoll = Mathf.Sin(roll);
                float tube = 0.55f + 0.45f * puff.w;
                float ringFraction = 0.70f + 0.28f * cosRoll * tube;
                float rim = Mathf.Clamp01((ringFraction - 0.42f) / 0.56f);
                float ring = capRadius * lobe * ringFraction;
                float droop = 0.12f * outwardSpread * rim * rim;
                float size = capDepth * (0.24f + 0.30f * puff.w);
                float height = capBase + capDepth * (0.50f + 0.40f * sinRoll * (0.60f + 0.40f * puff.w) - droop);
                height = Mathf.Min(height, top - size * 0.5f);
                var position = new Vector3(Mathf.Cos(azimuth) * ring, height, Mathf.Sin(azimuth) * ring);
                Color color = capAlbedo;
                color.a = density * (0.82f + 0.15f * puff.w);
                float hot = Mathf.SmoothStep(0.55f, 0.95f, Mathf.Repeat(puff.w * 37.1f + puff.y * 11.7f, 1f));
                WritePuff(count++, position, size, color, puff.w * 360f + flowTime * 15f,
                    emberFade * (0.08f + 0.75f * hot), Mathf.Clamp01(-sinRoll) * 0.75f);
            }

            // Flat-topped crown over the rim.
            float crownDensity = density * Mathf.Clamp01((age - 10f) / 9f);
            float crownRadius = capRadius * (0.62f + 0.22f * outwardSpread);
            for (int i = 0; i < CrownPuffs && crownDensity > 0f; i++)
            {
                Vector4 puff = distribution[FirePuffs + ColumnPuffs + CapPuffs + i];
                float fraction = Mathf.Sqrt(puff.w);
                float angle = Mathf.Atan2(puff.z, puff.x) + flowTime * 0.020f;
                float spread = crownRadius * fraction;
                float size = capDepth * (0.30f + 0.32f * puff.y);
                float height = top - size * 0.5f - capDepth * (0.24f * fraction * fraction + 0.06f * (1f - puff.y));
                var position = new Vector3(Mathf.Cos(angle) * spread, height, Mathf.Sin(angle) * spread);
                Color color = capAlbedo;
                color.a = crownDensity * (0.80f + 0.15f * puff.y);
                float hot = Mathf.SmoothStep(0.60f, 0.95f, Mathf.Repeat(puff.y * 29.3f + puff.w * 7.9f, 1f));
                WritePuff(count++, position, size, color, puff.y * 360f + flowTime * 6f,
                    emberFade * 0.60f * hot, 0f);
            }

            // Bright, warm-lit underside of the cap.
            float underDensity = density * Mathf.Clamp01((age - 14f) / 10f);
            for (int i = 0; i < UndersidePuffs && underDensity > 0f; i++)
            {
                Vector4 puff = distribution[FirePuffs + ColumnPuffs + CapPuffs + CrownPuffs + i];
                float fraction = Mathf.Sqrt(puff.w);
                float angle = Mathf.Atan2(puff.z, puff.x) + flowTime * 0.030f;
                float spread = capRadius * (0.14f + 0.78f * fraction);
                float size = capDepth * (0.20f + 0.22f * puff.y);
                float height = capBase + capDepth * (0.10f + 0.10f * puff.y - 0.05f * fraction
                    + 0.04f * Mathf.Sin(angle * 7f + flowTime * 0.20f));
                var position = new Vector3(Mathf.Cos(angle) * spread, height, Mathf.Sin(angle) * spread);
                Color color = undersideAlbedo;
                color.a = underDensity * (0.80f + 0.15f * puff.w);
                WritePuff(count++, position, size, color, puff.y * 360f + flowTime * 9f,
                    emberFade * (0.35f + 0.25f * puff.w), 0.90f);
            }

            for (int i = 0; i < DustPuffs && dustDensity > 0f; i++)
            {
                Vector4 puff = distribution[FirePuffs + ColumnPuffs + CapPuffs + CrownPuffs + UndersidePuffs + i];
                float spread = capRadius * (0.12f + 0.30f * puff.w);
                float height = capRadius * (0.010f + 0.030f * puff.y);
                var position = new Vector3(puff.x * spread, height, puff.z * spread);
                var color = new Color(0.52f, 0.42f, 0.34f, dustDensity * (0.35f + 0.30f * puff.w));
                WritePuff(count++, position, capRadius * (0.07f + 0.08f * puff.y), color, puff.y * 360f, 0f, 0f);
            }

            return count;
        }

        private void WritePuff(int index, Vector3 position, float size, Color color, float rotation,
            float glow, float underlight)
        {
            cloud.SetBillow(index, position, size, color, rotation, glow, underlight);
        }

        private void OnDestroy()
        {
            if (fireballMesh != null)
                Destroy(fireballMesh);
            if (fireballTexture != null)
                Destroy(fireballTexture);
            if (fireballMaterial != null)
                Destroy(fireballMaterial);
            if (cloud != null)
                cloud.Release();
            if (glow != null)
                glow.Release();
            if (burst != null)
                burst.Release();
            if (burstMaterial != null)
                Destroy(burstMaterial);
            if (burstTexture != null)
                Destroy(burstTexture);
            if (glowMaterial != null)
                Destroy(glowMaterial);
            if (glowTexture != null)
                Destroy(glowTexture);
            if (cloudMaterial != null)
                Destroy(cloudMaterial);
            if (shockFront != null)
                shockFront.Release();
        }
    }
}
