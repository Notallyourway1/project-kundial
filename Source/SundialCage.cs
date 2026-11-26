using System.Collections.Generic;
using UnityEngine;

namespace ProjectSundial
{
    // The optional structural cage the player switches on in the VAB. It is a frame of steel beams
    // (octagonal hoops, longerons and diagonal braces) that closes around the casing and tapers to a
    // hub at each end, so the bottom and top attach nodes still meet the same flange.
    //
    // The frame is built once as a single mesh and only shown or hidden afterwards. A cylinder collider
    // of the same size is switched together with it, so parts attached to the side sit on the cage
    // rather than inside it. Everything is parented to the part's model and goes with it when it blows.
    internal sealed class SundialCage : MonoBehaviour
    {
        private const int Sides = 8;
        private const float Radius = 2.2f;
        private const float Beam = 0.16f;
        private const float Brace = 0.10f;
        private const float BaseY = -0.23f;
        private const float TopY = 10.77f;
        private const float LowRingY = 0.5f;
        private const float HighRingY = 9.9f;
        private const float BaseHubRadius = 1.3f;
        private const float TopHubRadius = 0.65f;
        private const int Bays = 5;

        // A bare yellow patch of the casing texture (the same colour as the base collar).
        private static readonly Vector2 SteelUv = new Vector2(0.05f, 0.02f);

        private ModuleSundialPayload module;
        private GameObject frame;

        internal static void Attach(ModuleSundialPayload owner)
        {
            if (owner == null || owner.part == null)
                return;

            Transform parent = owner.part.transform.Find("model");
            if (parent == null)
                parent = owner.part.transform;

            var existing = parent.GetComponentInChildren<SundialCage>(true);
            if (existing != null)
            {
                existing.module = owner;
                return;
            }

            // Borrow the casing's own material so the cage is lit, shaded and textured like the rest.
            Material material = null;
            var renderer = parent.GetComponentInChildren<MeshRenderer>(true);
            if (renderer != null)
                material = renderer.sharedMaterial;
            if (material == null)
            {
                Shader shader = Shader.Find("KSP/Diffuse");
                if (shader == null)
                    return;
                material = new Material(shader) { color = new Color(0.60f, 0.54f, 0.32f) };
            }

            // Use the layer of the casing's own collider, so the editor's placement ray hits the cage
            // and surface-attached parts land on its outside instead of on the casing inside.
            int layer = parent.gameObject.layer;
            var casingCollider = parent.GetComponentInChildren<Collider>(true);
            if (casingCollider != null)
                layer = casingCollider.gameObject.layer;

            var root = new GameObject("Sundial structural cage");
            root.layer = layer;
            root.transform.SetParent(parent, false);
            var cage = root.AddComponent<SundialCage>();
            cage.module = owner;
            cage.Build(root, material);
        }

        private void Build(GameObject root, Material material)
        {
            // The script stays on the always-active root; only this child is switched on and off,
            // because an inactive object never runs its own Update.
            frame = new GameObject("Sundial cage frame");
            frame.layer = root.layer;
            frame.transform.SetParent(root.transform, false);

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Hoops at each level, joined by straight longerons and diagonal braces.
            for (int level = 0; level <= Bays; level++)
            {
                float y = Mathf.Lerp(LowRingY, HighRingY, (float)level / Bays);
                for (int i = 0; i < Sides; i++)
                    AddBeam(vertices, uvs, triangles, Corner(i, y, Radius), Corner(i + 1, y, Radius), Beam);
            }

            for (int i = 0; i < Sides; i++)
                AddBeam(vertices, uvs, triangles, Corner(i, LowRingY, Radius), Corner(i, HighRingY, Radius), Beam);

            for (int bay = 0; bay < Bays; bay++)
            {
                float lower = Mathf.Lerp(LowRingY, HighRingY, (float)bay / Bays);
                float upper = Mathf.Lerp(LowRingY, HighRingY, (float)(bay + 1) / Bays);
                for (int i = 0; i < Sides; i++)
                {
                    // Alternate the lean so neighbouring faces zig-zag.
                    bool forward = (i + bay) % 2 == 0;
                    Vector3 from = Corner(forward ? i : i + 1, lower, Radius);
                    Vector3 to = Corner(forward ? i + 1 : i, upper, Radius);
                    AddBeam(vertices, uvs, triangles, from, to, Brace);
                }
            }

            // Struts that close the frame down to a hub at the base and up to a hub at the nose.
            for (int i = 0; i < Sides; i++)
            {
                AddBeam(vertices, uvs, triangles, Corner(i, LowRingY, Radius), Corner(i, BaseY, BaseHubRadius), Beam);
                AddBeam(vertices, uvs, triangles, Corner(i, HighRingY, Radius), Corner(i, TopY, TopHubRadius), Beam);
                AddBeam(vertices, uvs, triangles, Corner(i, BaseY, BaseHubRadius), Corner(i + 1, BaseY, BaseHubRadius), Beam);
                AddBeam(vertices, uvs, triangles, Corner(i, TopY, TopHubRadius), Corner(i + 1, TopY, TopHubRadius), Beam);
            }

            var mesh = new Mesh { name = "Sundial structural cage" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            frame.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = frame.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;

            var collider = frame.AddComponent<MeshCollider>();
            collider.sharedMesh = BuildCollisionPrism();
            collider.convex = true;

            frame.SetActive(module != null && module.cage);
        }

        private void Update()
        {
            if (module == null || frame == null)
                return;

            bool wanted = module.cage;
            if (frame.activeSelf != wanted)
                frame.SetActive(wanted);
        }

        private void OnDestroy()
        {
            if (frame != null)
            {
                var filter = frame.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    Destroy(filter.sharedMesh);
            }
        }

        private static Vector3 Corner(int index, float y, float radius)
        {
            float angle = Mathf.PI * 2f * (index % Sides) / Sides;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        // A square-section bar from a to b. The vertices of each face are not shared, so every face
        // is flat shaded, and the winding is fixed up so each face looks outward.
        private static void AddBeam(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, float width)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-6f)
                return;

            axis.Normalize();
            Vector3 reference = Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, reference).normalized * (width * 0.5f);
            Vector3 v = Vector3.Cross(axis, u).normalized * (width * 0.5f);

            Vector3[] corner =
            {
                a - u - v, a + u - v, a + u + v, a - u + v,
                b - u - v, b + u - v, b + u + v, b - u + v,
            };
            Vector3 center = (a + b) * 0.5f;
            int[][] faces =
            {
                new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 },
                new[] { 3, 0, 4, 7 }, new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 },
            };

            foreach (int[] face in faces)
            {
                Vector3 p0 = corner[face[0]];
                Vector3 p1 = corner[face[1]];
                Vector3 p2 = corner[face[2]];
                Vector3 p3 = corner[face[3]];
                Vector3 faceCenter = (p0 + p1 + p2 + p3) * 0.25f;
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), faceCenter - center) < 0f)
                {
                    Vector3 swap = p1;
                    p1 = p3;
                    p3 = swap;
                }

                int start = vertices.Count;
                vertices.Add(p0);
                vertices.Add(p1);
                vertices.Add(p2);
                vertices.Add(p3);
                for (int k = 0; k < 4; k++)
                    uvs.Add(SteelUv);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }

        // An octagonal prism the size of the frame, for the convex collider.
        private static Mesh BuildCollisionPrism()
        {
            var vertices = new Vector3[Sides * 2];
            for (int i = 0; i < Sides; i++)
            {
                vertices[i] = Corner(i, LowRingY, Radius);
                vertices[Sides + i] = Corner(i, HighRingY, Radius);
            }

            var triangles = new List<int>();
            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                triangles.AddRange(new[] { i, Sides + i, next, next, Sides + i, Sides + next });
                if (i > 0 && i < Sides - 1)
                {
                    triangles.AddRange(new[] { 0, i + 1, i });
                    triangles.AddRange(new[] { Sides, Sides + i, Sides + i + 1 });
                }
            }

            var mesh = new Mesh { name = "Sundial cage collider" };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            return mesh;
        }
    }
}
