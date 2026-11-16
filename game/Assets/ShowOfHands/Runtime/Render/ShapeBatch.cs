using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ShowOfHands
{
    // Meshes made in code, so the app ships no model files.
    public static class Shapes
    {
        private static Mesh sphere, tube, quad;
        // A unit sphere: radius 0.5, so a scale of (d, d, d) gives a ball of diameter d.
        public static Mesh Sphere
        {
            get
            {
                if (sphere != null) return sphere;
                const int rings = 10, segments = 16;
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int r = 0; r <= rings; r++)
                {
                    float v = Mathf.PI * r / rings;
                    for (int s = 0; s <= segments; s++)
                    {
                        float u = 2f * Mathf.PI * s / segments;
                        vertices.Add(new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)) * 0.5f);
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < segments; s++)
                    {
                        int a = r * (segments + 1) + s, b = a + segments + 1;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                        triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                    }
                sphere = Build("SOH Sphere", vertices, triangles, v => v.normalized);
                return sphere;
            }
        }
        // An open tube along y from -0.5 to 0.5 with radius 0.5. Balls at the joints close the ends.
        public static Mesh Tube
        {
            get
            {
                if (tube != null) return tube;
                const int segments = 14;
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int s = 0; s <= segments; s++)
                {
                    float u = 2f * Mathf.PI * s / segments;
                    float x = Mathf.Cos(u) * 0.5f, z = Mathf.Sin(u) * 0.5f;
                    vertices.Add(new Vector3(x, -0.5f, z));
                    vertices.Add(new Vector3(x, 0.5f, z));
                }
                for (int s = 0; s < segments; s++)
                {
                    int a = s * 2;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                    triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 2);
                }
                tube = Build("SOH Tube", vertices, triangles, v => new Vector3(v.x, 0f, v.z).normalized);
                return tube;
            }
        }
        // A unit quad in the xy plane facing -z, with uv from 0 to 1.
        public static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "SOH Quad", hideFlags = HideFlags.HideAndDontSave };
                quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) });
                quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
                quad.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
                quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                quad.RecalculateBounds();
                return quad;
            }
        }
        private static Mesh Build(string name, List<Vector3> vertices, List<int> triangles, System.Func<Vector3, Vector3> normal)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            var normals = new List<Vector3>(vertices.Count);
            foreach (var v in vertices) normals.Add(normal(v));
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
    // Collects balls and tubes for a frame and draws them with GPU instancing: a few draw calls for every
    // hand on screen (T1). Fill it during Update, call Draw once in LateUpdate.
    public sealed class ShapeBatch
    {
        private const int Chunk = 1023;
        private sealed class Bucket
        {
            public readonly List<Matrix4x4[]> Matrices = new List<Matrix4x4[]>();
            public readonly List<Vector4[]> Colors = new List<Vector4[]>();
            public readonly List<Vector4[]> Effects = new List<Vector4[]>();
            public int Count;
            public void Add(in Matrix4x4 matrix, in Vector4 color, in Vector4 effect)
            {
                int chunk = Count / Chunk, slot = Count % Chunk;
                if (chunk == Matrices.Count)
                {
                    Matrices.Add(new Matrix4x4[Chunk]);
                    Colors.Add(new Vector4[Chunk]);
                    Effects.Add(new Vector4[Chunk]);
                }
                Matrices[chunk][slot] = matrix;
                Colors[chunk][slot] = color;
                Effects[chunk][slot] = effect;
                Count++;
            }
        }
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EffectId = Shader.PropertyToID("_Fx");
        private readonly Bucket spheres = new Bucket(), tubes = new Bucket();
        readonly Dictionary<Mesh,Bucket> extras=new Dictionary<Mesh,Bucket>();int extraCount;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Material material;
        public int Instances => spheres.Count + tubes.Count+extraCount;
        public ShapeBatch(Material material) { this.material = material; }
        public void Clear() { spheres.Count = 0; tubes.Count = 0;extraCount=0;foreach(var bucket in extras.Values)bucket.Count=0; }
        // color.a is how solid the shape is (1 solid, less is dithered away). effect.x adds glow, effect.y
        // turns on the mitt's quilt pattern.
        public void Sphere(Vector3 centre, float radius, Vector4 color, Vector4 effect)
        {
            float d = radius * 2f;
            var m = Matrix4x4.TRS(centre, Quaternion.identity, new Vector3(d, d, d));
            spheres.Add(m, color, effect);
        }
        public void Ellipsoid(Vector3 centre, Quaternion rotation, Vector3 radii, Vector4 color, Vector4 effect)
        {
            var m = Matrix4x4.TRS(centre, rotation, radii * 2f);
            spheres.Add(m, color, effect);
        }
        public void Tube(Vector3 from, Vector3 to, float radius, Vector4 color, Vector4 effect)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            if (length < 1e-5f) return;
            var rotation = Quaternion.FromToRotation(Vector3.up, along / length);
            var m = Matrix4x4.TRS((from + to) * 0.5f, rotation, new Vector3(radius * 2f, length, radius * 2f));
            tubes.Add(m, color, effect);
        }
        public void AddMesh(Mesh mesh,in Matrix4x4 matrix,in Vector4 color,in Vector4 fx){if(!extras.TryGetValue(mesh,out var bucket)){bucket=new Bucket();extras.Add(mesh,bucket);}bucket.Add(matrix,color,fx);extraCount++;}
        public void Draw(int layer = 0)
        {
            if (material == null) return;
            DrawBucket(spheres, Shapes.Sphere, layer);
            DrawBucket(tubes, Shapes.Tube, layer);foreach(var pair in extras)DrawBucket(pair.Value,pair.Key,layer);
        }
        private void DrawBucket(Bucket bucket, Mesh mesh, int layer)
        {
            for (int start = 0, chunk = 0; start < bucket.Count; start += Chunk, chunk++)
            {
                int count = Mathf.Min(Chunk, bucket.Count - start);
                block.SetVectorArray(ColorId, bucket.Colors[chunk]);
                block.SetVectorArray(EffectId, bucket.Effects[chunk]);
                Graphics.DrawMeshInstanced(mesh, 0, material, bucket.Matrices[chunk], count, block, ShadowCastingMode.Off, false, layer);
            }
        }
    }
}
