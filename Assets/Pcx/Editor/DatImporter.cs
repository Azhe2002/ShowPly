using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pcx
{
    /// <summary>
    /// Auto-imports .dat binary point cloud files (float32 xyz, little-endian, no color).
    /// </summary>
    [ScriptedImporter(2, "dat")]
    class DatImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            try
            {
                var mesh = ImportDat(context.assetPath);
                if (mesh == null) return;

                var go = new GameObject();
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();

                // Same shader/material setup as PlyImporter — depth grayscale (near=white, far=black).
                // Never let material creation abort the import: if the shader can't be
                // resolved yet, the prefab/mesh must still be created so the asset is usable.
                var mat = CreateMaterial(mesh);
                if (mat != null) mr.sharedMaterial = mat;

                context.AddObjectToAsset("prefab", go);
                context.AddObjectToAsset("mesh", mesh);
                if (mat != null) context.AddObjectToAsset("material", mat);
                context.SetMainObject(go);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed importing {context.assetPath}: {e.Message}");
            }
        }

        static Material CreateMaterial(Mesh mesh)
        {
            // Shader.Find can return null while shaders are still being imported (this
            // happens during the first import of the project), so fall back to loading
            // the shader asset by path and finally to the URP unlit shader.
            var shader = PlyImporter.FindPointCloudShader();
            if (shader == null)
            {
                Debug.LogWarning("Point cloud shader is not available yet; importing .dat " +
                                 "without a material. Reimport this asset once shaders finish compiling.");
                return null;
            }

            var mat = new Material(shader);
            var bounds = mesh.bounds;
            mat.SetFloat("_PointSize", 3f);
            mat.SetFloat("_DepthMin", bounds.min.z);
            mat.SetFloat("_DepthMax", bounds.max.z);
            return mat;
        }

        private Mesh ImportDat(string path)
        {
            var fileInfo = new FileInfo(path);
            int vertexCount = (int)(fileInfo.Length / 12); // 3 × float32 = 12 bytes

            if (vertexCount <= 0 || fileInfo.Length % 12 != 0)
                throw new Exception($"Invalid .dat file size: {fileInfo.Length} (not divisible by 12)");

            var vertices = new Vector3[vertexCount];
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;

            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();

                    var v = new Vector3(x, y, z);
                    vertices[i] = v;

                    if (x < min.x) min.x = x; if (x > max.x) max.x = x;
                    if (y < min.y) min.y = y; if (y > max.y) max.y = y;
                    if (z < min.z) min.z = z; if (z > max.z) max.z = z;
                }
            }

            // Recenter
            Vector3 center = (min + max) * 0.5f;
            for (int i = 0; i < vertexCount; i++)
                vertices[i] -= center;

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);

            var indices = new int[vertexCount];
            for (int i = 0; i < vertexCount; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Points, 0);

            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            Debug.Log($"Imported .dat: {vertexCount:N0} vertices, bounds={mesh.bounds}");
            return mesh;
        }
    }
}
