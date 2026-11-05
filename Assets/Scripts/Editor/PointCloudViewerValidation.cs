using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Checks binary offsets, scalar types, malformed files, and camera framing in Unity.</summary>
public static class PointCloudViewerValidation
{
    [MenuItem("Tools/Point Cloud Viewer/Validate")]
    public static void Run()
    {
        string folder = "Temp/PointCloudValidation";
        Directory.CreateDirectory(folder);
        string dat = Path.Combine(folder, "sample.dat");
        using (var writer = new BinaryWriter(File.Create(dat)))
        {
            WritePoint(writer, 10, 20, 30);
            WritePoint(writer, 14, 26, 38);
            WritePoint(writer, float.NaN, 0, 0);
        }
        var data = PointCloudFile.Read(dat);
        Require(data.Positions.Length == 2 && data.SkippedPoints == 1, "DAT finite filtering");
        Require(data.OriginalCenter == new Vector3(12, 23, 34), "DAT recentering");
        Require(data.Positions[0] == new Vector3(-2, -3, -4), "DAT XYZ reading");
        ExpectFailure(() => PointCloudFile.Read(dat, 1), "point limit");
        File.WriteAllBytes(dat, new byte[13]);
        ExpectFailure(() => PointCloudFile.Read(dat), "malformed DAT");

        string ply = Path.Combine(folder, "sample.ply");
        File.WriteAllText(ply, "ply\r\nformat ascii 1.0\r\ncomment validation\r\nelement vertex 2\r\n" +
            "property uchar red\r\nproperty double z\r\nproperty float x\r\nproperty float y\r\n" +
            "element face 0\r\nproperty list uchar int vertex_indices\r\nend_header\r\n" +
            "255 30 10 20\r\n0 38 14 26\r\n");
        CheckPly(ply);
        foreach (bool bigEndian in new[] { false, true })
        {
            using (var stream = File.Create(ply))
            using (var writer = new BinaryWriter(stream))
            {
                string header = "ply\r\nformat binary_" + (bigEndian ? "big" : "little") +
                    "_endian 1.0\r\nelement vertex 2\r\nproperty uchar red\r\nproperty double z\r\n" +
                    "property float x\r\nproperty short y\r\nend_header\r\n";
                writer.Write(Encoding.ASCII.GetBytes(header));
                for (int i = 0; i < 2; i++)
                {
                    writer.Write((byte)255);
                    WriteBytes(writer, BitConverter.GetBytes(i == 0 ? 30.0 : 38.0), bigEndian);
                    WriteBytes(writer, BitConverter.GetBytes(i == 0 ? 10f : 14f), bigEndian);
                    WriteBytes(writer, BitConverter.GetBytes((short)(i == 0 ? 20 : 26)), bigEndian);
                }
            }
            CheckPly(ply);
            byte[] bytes = File.ReadAllBytes(ply);
            Array.Resize(ref bytes, bytes.Length - 1);
            File.WriteAllBytes(ply, bytes);
            ExpectFailure(() => PointCloudFile.Read(ply), "truncated binary PLY");
        }
        File.WriteAllText(ply, "ply\nformat ascii 1.0\nelement vertex 1\nproperty float x\nend_header\n1\n");
        ExpectFailure(() => PointCloudFile.Read(ply), "missing XYZ");
        File.WriteAllText(ply, "ply\nformat ascii 1.0\nelement vertex 1\n");
        ExpectFailure(() => PointCloudFile.Read(ply), "missing end_header");

        ValidateCamera();
        ValidateNavigation();
        foreach (string directory in new[] { "Assets/PointCloudDat", "Assets/PointCloudsPly" })
        {
            if (!Directory.Exists(directory)) continue;
            foreach (string path in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(path) != ".dat" && Path.GetExtension(path) != ".ply") continue;
                data = PointCloudFile.Read(path);
                Require(data.Positions.Length > 0, "project sample " + path);
                Debug.Log($"Validated sample: {path}; points={data.Positions.Length}; skipped={data.SkippedPoints}");
            }
        }
        Debug.Log("POINT_CLOUD_VALIDATION_PASSED");
    }

    private static void ValidateCamera()
    {
        var go = new GameObject("Validation Camera", typeof(Camera), typeof(PointCloudCamera));
        try
        {
            var camera = go.GetComponent<Camera>();
            camera.aspect = 0.6f;
            var navigation = go.GetComponent<PointCloudCamera>();
            var bounds = new Bounds(new Vector3(100, -200, 300), new Vector3(2000, 400, 600));
            navigation.FitTopDown(bounds);
            CheckVisible(camera, bounds);
            camera.orthographic = false;
            go.transform.rotation = Quaternion.Euler(31, 53, 0);
            navigation.FitBounds(bounds);
            CheckVisible(camera, bounds);
            navigation.FitTopDown(new Bounds(Vector3.zero, Vector3.zero));
            Require(camera.orthographicSize > 0, "single-point fit");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/Point Cloud Viewer/Validate Navigation")]
    public static void ValidateNavigation()
    {
        var go = new GameObject("Navigation Validation", typeof(Camera), typeof(PointCloudCamera));
        try
        {
            var camera = go.GetComponent<Camera>();
            camera.aspect = 1.5f;
            var navigation = go.GetComponent<PointCloudCamera>();
            var bounds = new Bounds(Vector3.zero, new Vector3(20, 10, 4));
            navigation.FitTopDown(bounds);
            float initialWidth = ProjectedWidth(camera, bounds);
            Vector3 initialPosition = go.transform.position;
            navigation.Move(Vector3.forward, 0.25f);
            Require(ProjectedWidth(camera, bounds) > initialWidth, "W visibly approaches in orthographic view");
            Require(go.transform.position == initialPosition, "orthographic approach preserves depth");
            navigation.Move(Vector3.back, 0.25f);
            Require(Mathf.Abs(ProjectedWidth(camera, bounds) - initialWidth) < 0.0001f,
                "S reverses orthographic approach");
            navigation.Move(Vector3.back, 0.25f);
            Require(ProjectedWidth(camera, bounds) < initialWidth, "S visibly retreats in orthographic view");

            navigation.FitTopDown(bounds);
            navigation.Move(Vector3.forward, 0.25f);
            float normalSize = camera.orthographicSize;
            navigation.FitTopDown(bounds);
            navigation.Move(Vector3.forward, 0.25f, true);
            Require(camera.orthographicSize < normalSize, "Shift accelerates approach");

            navigation.FitTopDown(bounds);
            for (int i = 0; i < 25; i++) navigation.Move(Vector3.forward, 0.01f);
            Require(Mathf.Abs(camera.orthographicSize - normalSize) < 0.0001f,
                "approach is independent of frame rate");
            navigation.Move(Vector3.forward, 1000f);
            Require(camera.orthographicSize > 0f && !float.IsNaN(camera.orthographicSize), "minimum zoom limit");
            navigation.Move(Vector3.back, 1000f);
            Require(!float.IsInfinity(camera.orthographicSize) && !float.IsNaN(camera.orthographicSize),
                "maximum zoom limit");

            // Preserve the existing world-up Q/E and camera-right A/D translations.
            foreach (Vector3 input in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down })
            {
                navigation.FitTopDown(bounds);
                go.transform.rotation = Quaternion.Euler(24, 53, 0);
                navigation.FitBounds(bounds);
                Vector3 start = go.transform.position;
                Vector3 expected = go.transform.right * input.x + Vector3.up * input.y;
                float size = camera.orthographicSize;
                navigation.Move(input, 0.25f);
                Require(Vector3.Dot((go.transform.position - start).normalized, expected.normalized) > 0.999f,
                    "A/D/Q/E movement direction");
                Require(camera.orthographicSize == size, "A/D/Q/E preserve zoom");
            }

            camera.orthographic = false;
            go.transform.rotation = Quaternion.identity;
            navigation.FitBounds(bounds);
            Vector3 perspectivePosition = go.transform.position;
            initialWidth = ProjectedWidth(camera, bounds);
            navigation.Move(Vector3.forward, 0.1f);
            Require(Vector3.Dot(go.transform.position - perspectivePosition, go.transform.forward) > 0f &&
                ProjectedWidth(camera, bounds) > initialWidth, "perspective W moves forward");
            navigation.Move(Vector3.back, 0.1f);
            Require(Vector3.Distance(go.transform.position, perspectivePosition) < 0.0001f,
                "perspective S moves backward");
            Debug.Log("POINT_CLOUD_NAVIGATION_VALIDATION_PASSED");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static float ProjectedWidth(Camera camera, Bounds bounds)
    {
        Vector3 left = camera.WorldToViewportPoint(bounds.center - camera.transform.right * bounds.extents.x);
        Vector3 right = camera.WorldToViewportPoint(bounds.center + camera.transform.right * bounds.extents.x);
        return Mathf.Abs(right.x - left.x);
    }

    private static void CheckVisible(Camera camera, Bounds bounds)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 view = camera.WorldToViewportPoint(point);
            Require(view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1 &&
                view.z >= camera.nearClipPlane && view.z <= camera.farClipPlane, "camera bounds fit");
        }
    }

    private static void CheckPly(string path)
    {
        var data = PointCloudFile.Read(path);
        Require(data.Positions.Length == 2 && data.Positions[0] == new Vector3(-2, -3, -4) &&
            data.Positions[1] == new Vector3(2, 3, 4), "PLY scalar order / endian / CRLF");
    }
    private static void WritePoint(BinaryWriter writer, float x, float y, float z)
    { writer.Write(x); writer.Write(y); writer.Write(z); }
    private static void WriteBytes(BinaryWriter writer, byte[] bytes, bool bigEndian)
    { if (bigEndian == BitConverter.IsLittleEndian) Array.Reverse(bytes); writer.Write(bytes); }
    private static void Require(bool condition, string label)
    { if (!condition) throw new Exception("Validation failed: " + label); }
    private static void ExpectFailure(Action action, string label)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (EndOfStreamException) { return; }
        throw new Exception("Expected rejection: " + label);
    }
}
