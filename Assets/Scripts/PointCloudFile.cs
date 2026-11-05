using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Runtime-only DAT/PLY reader. No UnityEditor APIs or Unity object creation.</summary>
public static class PointCloudFile
{
    public sealed class Data
    {
        public Vector3[] Positions;
        public Vector3 OriginalCenter;
        public int SkippedPoints;
    }

    private struct Property
    {
        public string Name;
        public string Type;
    }

    public static Data Read(string path, int maxPoints = 5000000)
    {
        using (var stream = File.OpenRead(path))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".dat") return ReadDat(stream, maxPoints);
            if (extension == ".ply") return ReadPly(stream, maxPoints);
            throw new InvalidDataException("Please select a .dat or .ply file.");
        }
    }

    private static Data ReadDat(Stream stream, int limit)
    {
        if (stream.Length == 0 || stream.Length % 12 != 0)
            throw new InvalidDataException("DAT must contain little-endian float32 XYZ records (12 bytes per point).");
        long count = stream.Length / 12;
        CheckCount(count, limit);
        var points = new List<Vector3>((int)count);
        using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            for (int i = 0; i < count; i++)
                AddFinite(points, reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        return Center(points, (int)count);
    }

    private static Data ReadPly(Stream stream, int limit)
    {
        if (ReadHeaderLine(stream) != "ply") throw new InvalidDataException("Missing PLY signature.");
        string format = null;
        int count = -1;
        bool vertex = false;
        bool seenVertex = false;
        bool precedingData = false;
        var properties = new List<Property>();
        for (int lines = 0; ; lines++)
        {
            if (lines > 4096) throw new InvalidDataException("PLY header is too large.");
            string line = ReadHeaderLine(stream);
            if (line == null) throw new EndOfStreamException("PLY header has no end_header.");
            string[] words = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words[0] == "comment" || words[0] == "obj_info") continue;
            if (words[0] == "end_header") break;
            if (words[0] == "format" && words.Length == 3 && words[2] == "1.0") format = words[1];
            else if (words[0] == "element" && words.Length == 3)
            {
                vertex = words[1] == "vertex";
                int elementCount = int.Parse(words[2], CultureInfo.InvariantCulture);
                if (elementCount < 0) throw new InvalidDataException("Negative PLY element count.");
                if (vertex)
                {
                    if (seenVertex || precedingData)
                        throw new InvalidDataException("PLY must contain one vertex element before other nonempty elements.");
                    seenVertex = true;
                    count = elementCount;
                    CheckCount(count, limit);
                }
                else if (!seenVertex && elementCount > 0) precedingData = true;
            }
            else if (words[0] == "property" && vertex)
            {
                if (words.Length != 3 || words[1] == "list")
                    throw new InvalidDataException("List properties inside PLY vertices are not supported.");
                ScalarSize(words[1]);
                properties.Add(new Property { Name = words[2], Type = words[1] });
            }
        }
        if (format != "ascii" && format != "binary_little_endian" && format != "binary_big_endian")
            throw new InvalidDataException("Unsupported PLY format. Use ASCII or binary PLY 1.0.");
        CheckCount(count, limit);
        int xIndex = properties.FindIndex(p => p.Name == "x");
        int yIndex = properties.FindIndex(p => p.Name == "y");
        int zIndex = properties.FindIndex(p => p.Name == "z");
        if (xIndex < 0 || yIndex < 0 || zIndex < 0)
            throw new InvalidDataException("PLY vertices need x, y and z properties.");

        var points = new List<Vector3>(count);
        if (format == "ascii")
        {
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 65536, true))
            {
                for (int i = 0; i < count; i++)
                {
                    string line;
                    do { line = reader.ReadLine(); } while (line != null && string.IsNullOrWhiteSpace(line));
                    if (line == null) throw new EndOfStreamException("PLY vertex data is truncated.");
                    string[] values = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length != properties.Count)
                        throw new InvalidDataException("PLY vertex property count does not match its header.");
                    AddFinite(points, Parse(values[xIndex]), Parse(values[yIndex]), Parse(values[zIndex]));
                }
            }
        }
        else
        {
            long stride = 0;
            foreach (var property in properties) stride += ScalarSize(property.Type);
            if (stream.Length - stream.Position < stride * count)
                throw new EndOfStreamException("PLY binary vertex data is truncated.");
            bool reverse = (format == "binary_big_endian") == BitConverter.IsLittleEndian;
            var buffer = new byte[8];
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                for (int i = 0; i < count; i++)
                {
                    double x = 0, y = 0, z = 0;
                    for (int j = 0; j < properties.Count; j++)
                    {
                        double value = ReadScalar(reader, properties[j].Type, reverse, buffer);
                        if (j == xIndex) x = value;
                        else if (j == yIndex) y = value;
                        else if (j == zIndex) z = value;
                    }
                    AddFinite(points, x, y, z);
                }
        }
        return Center(points, count);
    }

    // Read bytes rather than StreamReader so binary payload offsets and CRLF are exact.
    private static string ReadHeaderLine(Stream stream)
    {
        var line = new StringBuilder();
        while (true)
        {
            int b = stream.ReadByte();
            if (b < 0) return line.Length == 0 ? null : line.ToString().TrimEnd('\r');
            if (b == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= 65536) throw new InvalidDataException("PLY header line is too long.");
            line.Append((char)b);
        }
    }

    private static int ScalarSize(string type)
    {
        switch (type)
        {
            case "char": case "uchar": case "int8": case "uint8": return 1;
            case "short": case "ushort": case "int16": case "uint16": return 2;
            case "int": case "uint": case "int32": case "uint32": case "float": case "float32": return 4;
            case "double": case "float64": return 8;
            default: throw new InvalidDataException("Unsupported PLY scalar type: " + type);
        }
    }

    private static double ReadScalar(BinaryReader reader, string type, bool reverse, byte[] buffer)
    {
        int size = ScalarSize(type);
        for (int i = 0; i < size; i++) buffer[i] = reader.ReadByte();
        if (reverse && size > 1) Array.Reverse(buffer, 0, size);
        switch (type)
        {
            case "char": case "int8": return (sbyte)buffer[0];
            case "uchar": case "uint8": return buffer[0];
            case "short": case "int16": return BitConverter.ToInt16(buffer, 0);
            case "ushort": case "uint16": return BitConverter.ToUInt16(buffer, 0);
            case "int": case "int32": return BitConverter.ToInt32(buffer, 0);
            case "uint": case "uint32": return BitConverter.ToUInt32(buffer, 0);
            case "float": case "float32": return BitConverter.ToSingle(buffer, 0);
            default: return BitConverter.ToDouble(buffer, 0);
        }
    }

    private static double Parse(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static void CheckCount(long count, int limit)
    {
        if (count <= 0) throw new InvalidDataException("The point cloud contains no vertices.");
        if (count > limit) throw new InvalidDataException($"Point count {count:N0} exceeds the configured limit ({limit:N0}).");
    }

    private static void AddFinite(List<Vector3> points, double x, double y, double z)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || Math.Abs(x) > float.MaxValue ||
            double.IsNaN(y) || double.IsInfinity(y) || Math.Abs(y) > float.MaxValue ||
            double.IsNaN(z) || double.IsInfinity(z) || Math.Abs(z) > float.MaxValue) return;
        points.Add(new Vector3((float)x, (float)y, (float)z));
    }

    private static Data Center(List<Vector3> points, int originalCount)
    {
        if (points.Count == 0) throw new InvalidDataException("No finite XYZ coordinates were found.");
        Vector3 min = points[0], max = points[0];
        foreach (var point in points) { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
        Vector3 center = min * 0.5f + max * 0.5f;
        Vector3 size = max - min;
        if (float.IsInfinity(size.sqrMagnitude))
            throw new InvalidDataException("Coordinate range is too large for a Unity mesh.");
        Vector3[] positions = points.ToArray();
        for (int i = 0; i < positions.Length; i++) positions[i] -= center;
        return new Data { Positions = positions, OriginalCenter = center, SkippedPoints = originalCount - positions.Length };
    }
}
