using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Scene-style navigation in Game View and standalone players.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class PointCloudCamera : MonoBehaviour
{
    [Header("Point cloud (optional; otherwise finds point meshes)")]
    [SerializeField] private MeshRenderer _target;
    [SerializeField] private Transform _targetRoot;
    [SerializeField, Min(0f)] private float _margin = 0.1f;
    [SerializeField] private bool _fitOnStart = true;
    [Header("Navigation")]
    [SerializeField, Min(0.01f)] private float _rotationSpeed = 0.25f;
    [SerializeField, Min(0.01f)] private float _panSpeed = 1f;
    [SerializeField, Min(0.01f)] private float _zoomSpeed = 0.15f;
    [SerializeField, Min(0.01f)] private float _moveSpeed = 0.5f;
    [SerializeField, Min(1f)] private float _fastMultiplier = 4f;
    [SerializeField, Min(0.0001f)] private float _minDistance = 0.01f;
    [SerializeField, Min(1f)] private float _maxDistance = 1000000f;

    public bool InputEnabled { get; set; } = true;
    public Vector3 Pivot => _pivot;
    private Camera _camera;
    private Vector3 _pivot;
    private float _distance;
    private Bounds _bounds;
    private bool _hasBounds;
    private Vector3 _lastMouse;
    private int _dragButton = -1;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _distance = Mathf.Clamp(10f, _minDistance, _maxDistance);
        _pivot = transform.position + transform.forward * _distance;
    }

    private void Start()
    {
        // A loader may already have supplied bounds before Start.
        if (_fitOnStart && !_hasBounds && TryGetTargetBounds(out var bounds)) FitTopDown(bounds);
    }

    private void Update()
    {
        if (!InputEnabled || !Application.isFocused || Input.GetKeyDown(KeyCode.Escape))
        {
            _dragButton = -1;
            return;
        }
        Vector3 mouse = Input.mousePosition;
        bool overView = _camera.pixelRect.Contains(new Vector2(mouse.x, mouse.y));
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool editingUI = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;
        if (_dragButton >= 0 && !Input.GetMouseButton(_dragButton)) _dragButton = -1;
        if (overView && !overUI)
        {
            if (Input.GetMouseButtonDown(1)) BeginDrag(1, mouse);
            else if (Input.GetMouseButtonDown(2)) BeginDrag(2, mouse);
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) Zoom(scroll);
            if (!editingUI)
            {
                if (Input.GetKeyDown(KeyCode.F)) Focus();
                if (Input.GetKeyDown(KeyCode.Home)) ResetView();
            }
        }
        if (_dragButton >= 0)
        {
            Vector3 delta = mouse - _lastMouse;
            if (_dragButton == 1) Orbit(delta);
            else Pan(delta);
            if (_dragButton == 1 && !editingUI) Fly();
        }
        _lastMouse = mouse;
    }

    private void BeginDrag(int button, Vector3 mouse)
    {
        _dragButton = button;
        _lastMouse = mouse;
    }

    private void Orbit(Vector3 delta)
    {
        transform.rotation = Quaternion.AngleAxis(delta.x * _rotationSpeed, Vector3.up) *
                             transform.rotation *
                             Quaternion.AngleAxis(-delta.y * _rotationSpeed, Vector3.right);
        ApplyPosition();
    }

    private void Pan(Vector3 delta)
    {
        float halfHeight = _camera.orthographic ? _camera.orthographicSize :
            _distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float unitsPerPixel = 2f * halfHeight / Mathf.Max(1, _camera.pixelHeight);
        _pivot -= (transform.right * delta.x + transform.up * delta.y) * unitsPerPixel * _panSpeed;
        ApplyPosition();
    }

    private void Zoom(float scroll)
    {
        float factor = Mathf.Exp(-scroll * _zoomSpeed);
        if (_camera.orthographic)
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * factor, _minDistance, _maxDistance);
        else _distance = Mathf.Clamp(_distance * factor, _minDistance, _maxDistance);
        ApplyPosition();
    }

    private void Fly()
    {
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) input.z += 1f;
        if (Input.GetKey(KeyCode.S)) input.z -= 1f;
        if (Input.GetKey(KeyCode.D)) input.x += 1f;
        if (Input.GetKey(KeyCode.A)) input.x -= 1f;
        if (Input.GetKey(KeyCode.E)) input.y += 1f;
        if (Input.GetKey(KeyCode.Q)) input.y -= 1f;
        bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Move(input, Time.unscaledDeltaTime, fast);
    }

    /// <summary>
    /// Move with X = camera right, Y = world up, Z = forward.
    /// Orthographic forward/backward changes view scale, since depth motion is invisible.
    /// </summary>
    public void Move(Vector3 input, float deltaTime, bool fast = false)
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (deltaTime <= 0f || input == Vector3.zero) return;
        float multiplier = fast ? _fastMultiplier : 1f;
        float scale = _camera.orthographic ? _camera.orthographicSize * 2f : _distance;
        float speed = Mathf.Max(scale, _minDistance) * _moveSpeed * multiplier;
        Vector3 direction = transform.right * input.x + Vector3.up * input.y;
        if (_camera.orthographic)
        {
            // Keep the inspection center/depth stable while approaching or retreating.
            // Exponential scaling gives consistent speed at every zoom level/frame rate.
            float factor = Mathf.Exp(-input.z * _moveSpeed * 2f * multiplier * deltaTime);
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * factor,
                _minDistance, _maxDistance);
        }
        else direction += transform.forward * input.z;
        _pivot += direction.normalized * (speed * deltaTime);
        ApplyPosition();
    }

    public void SetTarget(Transform target, bool fit = true)
    {
        _targetRoot = target;
        _target = null;
        _hasBounds = false;
        if (fit && TryGetTargetBounds(out var bounds)) FitTopDown(bounds);
    }

    public void Focus()
    {
        if (TryGetTargetBounds(out var bounds)) FitBounds(bounds);
        else if (_hasBounds) FitBounds(_bounds);
    }

    public void ResetView()
    {
        if (TryGetTargetBounds(out var bounds)) FitTopDown(bounds);
        else if (_hasBounds) FitTopDown(_bounds);
    }

    /// <summary>Fit using the current viewing direction and projection.</summary>
    public void FitBounds(Bounds bounds)
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        _bounds = bounds;
        _hasBounds = true;
        _pivot = bounds.center;
        _dragButton = -1;
        Vector3 extent = Vector3.zero;
        Quaternion inverse = Quaternion.Inverse(transform.rotation);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = Vector3.Scale(bounds.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 view = inverse * corner;
            extent = Vector3.Max(extent, new Vector3(Mathf.Abs(view.x), Mathf.Abs(view.y), Mathf.Abs(view.z)));
        }
        float halfHeight = Mathf.Max(extent.y, extent.x / Mathf.Max(0.01f, _camera.aspect));
        halfHeight = Mathf.Max(_minDistance, halfHeight * (1f + _margin));
        if (_camera.orthographic)
        {
            _camera.orthographicSize = halfHeight;
            _distance = Mathf.Max(bounds.extents.magnitude * 2f, _minDistance * 10f);
        }
        else _distance = extent.z + halfHeight / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _maxDistance = Mathf.Max(_maxDistance, _distance * 10f, halfHeight * 10f);
        _distance = Mathf.Max(_distance, _minDistance);
        ApplyPosition();
    }

    /// <summary>XY inspection view, looking along -Z; also used after loading.</summary>
    public void FitTopDown(Bounds bounds)
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        _camera.orthographic = true;
        transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        FitBounds(bounds);
    }

    private void ApplyPosition()
    {
        transform.position = _pivot - transform.forward * _distance;
        if (!_hasBounds) return;
        float radius = Mathf.Max(_bounds.extents.magnitude, _minDistance);
        float depth = Vector3.Dot(_bounds.center - transform.position, transform.forward);
        _camera.nearClipPlane = Mathf.Max(0.0001f, (depth - radius) * 0.5f);
        _camera.farClipPlane = Mathf.Max(_camera.nearClipPlane + 1f, depth + radius * 2f, _distance * 2f);
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        bounds = default;
        if (_target != null) { bounds = _target.bounds; return true; }
        MeshFilter[] meshes = _targetRoot != null ? _targetRoot.GetComponentsInChildren<MeshFilter>() :
            FindObjectsOfType<MeshFilter>();
        bool found = false;
        foreach (var filter in meshes)
        {
            Mesh mesh = filter.sharedMesh;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || mesh.subMeshCount == 0 || renderer == null || !renderer.enabled) continue;
            if (_targetRoot == null && mesh.GetTopology(0) != MeshTopology.Points) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        return found;
    }

    private void OnDisable() => _dragButton = -1;
    private void OnApplicationFocus(bool focused) { if (!focused) _dragButton = -1; }
    [ContextMenu("Fit To Point Cloud")]
    private void FitToPointCloud() => ResetView();
}
