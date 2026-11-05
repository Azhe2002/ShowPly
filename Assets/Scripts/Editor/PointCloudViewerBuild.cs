using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reproducible standalone viewer scene and Windows build.</summary>
public static class PointCloudViewerBuild
{
    public const string ScenePath = "Assets/Scenes/PointCloudViewer.unity";

    [MenuItem("Tools/Point Cloud Viewer/Create Runtime Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener),
            typeof(PointCloudCamera), typeof(PointCloudViewer));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
        camera.transform.position = new Vector3(0, 0, -10);
        var viewer = new SerializedObject(cameraObject.GetComponent<PointCloudViewer>());
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PointCloudUnlit.shader");
        if (shader == null) throw new InvalidOperationException("Point cloud shader asset is missing.");
        viewer.FindProperty("_pointShader").objectReferenceValue = shader;
        viewer.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Runtime point cloud viewer scene created: " + ScenePath);
    }

    [MenuItem("Tools/Point Cloud Viewer/Build Windows")]
    public static void BuildWindows()
    {
        if (!File.Exists(ScenePath)) CreateScene();
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/ShowPly.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed: " + report.summary.result);
        Debug.Log("Windows viewer built: Builds/Windows/ShowPly.exe");
    }

    public static void ValidateAndBuild()
    {
        PointCloudViewerValidation.Run();
        CreateScene();
        BuildWindows();
    }

    public static void ValidateNavigationAndBuild()
    {
        PointCloudViewerValidation.ValidateNavigation();
        BuildWindows();
    }
}
