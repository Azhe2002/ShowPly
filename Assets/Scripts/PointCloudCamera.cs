using UnityEngine;

/// <summary>
/// Auto-positions the camera for a top-down Z-axis view of the point cloud.
/// Attach to the Main Camera, or call SetupFromBounds() manually.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PointCloudCamera : MonoBehaviour
{
    [SerializeField] private MeshRenderer _target;
    [SerializeField] private float _margin = 0.1f; // 10% margin

    private void Start()
    {
        if (_target != null)
            FitTopDown(_target.bounds);
    }

    /// <summary>Fit camera top-down on Z axis to cover the given bounds.</summary>
    public void FitTopDown(Bounds bounds)
    {
        var cam = GetComponent<Camera>();

        // Orthographic — best for top-down point cloud inspection
        cam.orthographic = true;

        // Camera above center, looking down along -Z
        Vector3 center = bounds.center;
        float halfW = bounds.extents.x * (1f + _margin);
        float halfH = bounds.extents.y * (1f + _margin);

        cam.transform.position = new Vector3(center.x, center.y, bounds.max.z + bounds.extents.z + 10f);
        cam.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        // Match ortho size to cover XY extent
        cam.orthographicSize = Mathf.Max(halfH, halfW / cam.aspect);
    }

#if UNITY_EDITOR
    [ContextMenu("Fit To Point Cloud")]
    private void EditorFit()
    {
        if (_target == null)
        {
            _target = FindObjectOfType<MeshRenderer>();
            if (_target == null)
            {
                Debug.LogWarning("No MeshRenderer found in scene. Drag the point cloud to _target.");
                return;
            }
        }
        FitTopDown(_target.bounds);
        UnityEditor.SceneView.RepaintAll();
    }
#endif
}
