using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Minimal standalone file viewer with a path field and background file reading.</summary>
[DefaultExecutionOrder(-100), RequireComponent(typeof(PointCloudCamera))]
public sealed class PointCloudViewer : MonoBehaviour
{
    [SerializeField] private Shader _pointShader;
    [SerializeField, Min(1)] private int _maxPoints = 5000000;
    private PointCloudCamera _navigation;
    private GameObject _cloud;
    private Mesh _mesh;
    private Material _material;
    private string _path = "";
    private string _status = "Enter a DAT / PLY path to open a point cloud.";
    private string _info = "";
    private bool _loading;
    private bool _importRequested;
    private bool _showHelp = true;
    private float _pointSize = 3f;
    private Rect _panel;

    private void Awake() => _navigation = GetComponent<PointCloudCamera>();

    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--file" && i + 1 < args.Length)
            {
                _path = args[i + 1];
                LoadFile(_path);
                break;
            }
        }
    }

    private void Update()
    {
        // Open the modal outside OnGUI so its layout/event processing stays balanced.
        if (_importRequested)
        {
            _importRequested = false;
            ImportPath();
        }
        // IMGUI uses top-left coordinates. Block navigation over the whole toolbar,
        // and while typing, so file names containing F/W/A/S/D don't move the view.
        Vector3 mouse = Input.mousePosition;
        _navigation.InputEnabled = !_panel.Contains(new Vector2(mouse.x, Screen.height - mouse.y)) &&
                                   GUIUtility.keyboardControl == 0 && !_loading;
        if (Input.GetKeyDown(KeyCode.F1)) _showHelp = !_showHelp;
    }

    private void OnGUI()
    {
        float width = Mathf.Min(860f, Screen.width - 16f);
        _panel = new Rect(8f, 8f, width, _showHelp ? 184f : 140f);
        GUILayout.BeginArea(_panel, GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("DAT / PLY", GUILayout.Width(76));
        GUI.SetNextControlName("FilePath");
        _path = GUILayout.TextField(_path, GUILayout.MinWidth(80));
        bool enter = Event.current.type == EventType.KeyDown &&
                     (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                     GUI.GetNameOfFocusedControl() == "FilePath";
        GUI.enabled = !_loading;
        if (GUILayout.Button("Import", GUILayout.Width(64)))
        {
            GUI.FocusControl(null);
            _importRequested = true;
        }
        if (GUILayout.Button(_loading ? "Loading..." : "Open", GUILayout.Width(84)) || enter)
        {
            GUI.FocusControl(null);
            if (enter) Event.current.Use();
            LoadFile(_path);
        }
        GUI.enabled = true;
        if (GUILayout.Button("Quit", GUILayout.Width(50))) Application.Quit();
        GUILayout.EndHorizontal();
        GUILayout.Label(_status);
        GUILayout.Label(_info);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Focus (F)", GUILayout.Width(90))) { GUI.FocusControl(null); _navigation.Focus(); }
        if (GUILayout.Button("Reset (Home)", GUILayout.Width(110))) { GUI.FocusControl(null); _navigation.ResetView(); }
        GUILayout.Label("Point size", GUILayout.Width(70));
        _pointSize = GUILayout.HorizontalSlider(_pointSize, 1f, 12f, GUILayout.Width(100));
        if (_material != null) _material.SetFloat("_PointSize", _pointSize);
        _showHelp = GUILayout.Toggle(_showHelp, "Help (F1)");
        GUILayout.EndHorizontal();
        if (_showHelp)
        {
            GUILayout.Label("RMB drag: orbit   |   MMB drag: pan   |   Wheel: zoom   |   F: fit   |   Home: top view");
            GUILayout.Label("Hold RMB + W/S: forward/back   |   A/D: left/right   |   Q/E: down/up   |   Shift: faster");
        }
        GUILayout.EndArea();
        if (Event.current.type == EventType.MouseDown && !_panel.Contains(Event.current.mousePosition))
            GUI.FocusControl(null);
    }

    private void ImportPath()
    {
        _navigation.InputEnabled = false;
        try
        {
            string selected = PointCloudFileDialog.SelectFile(_path);
            if (!string.IsNullOrEmpty(selected))
            {
                _path = selected;
                _status = "Selected: " + Path.GetFileName(selected) + ". Click Open to load.";
            }
        }
        catch (Exception exception)
        {
            _status = "Import failed: " + exception.Message;
            Debug.LogWarning(_status);
        }
    }

    public async void LoadFile(string path)
    {
        if (_loading) return;
        _loading = true;
        _status = "Reading file...";
        Mesh nextMesh = null;
        Material nextMaterial = null;
        try
        {
            path = Path.GetFullPath(path.Trim().Trim('"'));
            int limit = _maxPoints;
            var data = await Task.Run(() => PointCloudFile.Read(path, limit));
            if (this == null || !Application.isPlaying) return;
            Shader shader = _pointShader != null ? _pointShader : Shader.Find("PointCloud/UnlitVertexColor");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Point cloud shader is missing or unsupported on this device.");
            nextMesh = new Mesh { name = Path.GetFileNameWithoutExtension(path),
                indexFormat = data.Positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            nextMesh.vertices = data.Positions;
            int[] indices = new int[data.Positions.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            nextMesh.SetIndices(indices, MeshTopology.Points, 0);
            nextMesh.RecalculateBounds();
            nextMaterial = new Material(shader);
            nextMaterial.SetFloat("_PointSize", _pointSize);
            nextMaterial.SetFloat("_DepthMin", nextMesh.bounds.min.z);
            nextMaterial.SetFloat("_DepthMax", nextMesh.bounds.max.z);
            nextMesh.UploadMeshData(true);

            if (_cloud == null)
            {
                _cloud = new GameObject("Runtime Point Cloud", typeof(MeshFilter), typeof(MeshRenderer));
                _cloud.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                _cloud.GetComponent<MeshRenderer>().receiveShadows = false;
            }
            _cloud.GetComponent<MeshFilter>().sharedMesh = nextMesh;
            _cloud.GetComponent<MeshRenderer>().sharedMaterial = nextMaterial;
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            _mesh = nextMesh;
            _material = nextMaterial;
            nextMesh = null;
            nextMaterial = null;
            _navigation.SetTarget(_cloud.transform);
            _status = "Opened: " + Path.GetFileName(path);
            _info = $"{data.Positions.Length:N0} points  |  Size: {_mesh.bounds.size:F2}  |  Skipped invalid: {data.SkippedPoints:N0}";
            Debug.Log($"Point cloud loaded: {path}; {data.Positions.Length} points; center={data.OriginalCenter}");
        }
        catch (Exception exception)
        {
            if (this != null) _status = "Open failed: " + exception.Message;
            Debug.LogWarning("Point cloud open failed: " + exception.Message);
        }
        finally
        {
            if (nextMesh != null) Destroy(nextMesh);
            if (nextMaterial != null) Destroy(nextMaterial);
            _loading = false;
        }
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
        if (_cloud != null) Destroy(_cloud);
    }
}
