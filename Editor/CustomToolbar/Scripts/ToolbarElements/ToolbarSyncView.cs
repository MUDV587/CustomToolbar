using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityToolbarExtender;

[Serializable]
internal class ToolbarSyncView : BaseToolbarElement 
{

    private static GUIContent syncSceneToggle;
    private static GUIContent syncGameToggle;
    
    private static GUIContent syncSceneToGameButton;
    private static GUIContent syncGameToSceneButton;

    private static bool syncSceneToGame;
    private static bool syncGameToScene;

    private const string CameraSelectionKey = "SyncViewGameCameraPath";
    private static GUIContent s_cameraPopupContent;

    // 选择解析缓存：path key -> camera（fake-null 由 Unity == 判断）
    private static string s_resolvedSelectionKey;
    private static Camera s_resolvedCamera;

    // 游戏相机状态快照（用于 GameView -> SceneView 的"值变化才同步"）
    private static bool s_lastGameCamStateInit = false;
    private static Vector3 s_lastGameCamPos;
    private static Quaternion s_lastGameCamRot;
    private static float s_lastGameCamFov = float.NaN;
    private static float s_lastGameCamNear = float.NaN;
    private static float s_lastGameCamFar = float.NaN;

    public override string NameInList => "[Sync View] Sync GameView & SceneView";
    public override int SortingGroup => 3;

    private static SceneView s_SceneView = null;
    private static SceneView sceneView
    {
        get
        {
            if (s_SceneView == null)
            {
                s_SceneView = SceneView.lastActiveSceneView;
            }

            return s_SceneView;
        }
    }

    private static Camera s_gameViewCamera = null;
    // 选择解析结果缓存见 GetSelectedCamera()：按存储的 path key 缓存，失效才重新枚举场景
    private static Camera gameViewCamera
    {
        get
        {
            s_gameViewCamera = GetSelectedCamera();
            return s_gameViewCamera;
        }
    }

    public override void Init()
    {
        syncSceneToggle = EditorGUIUtility.IconContent("UnityEditor.SceneView@2x");
        syncSceneToggle.tooltip = "Continuously sync SceneView camera to GameView camera";

        syncGameToggle = EditorGUIUtility.IconContent("UnityEditor.GameView@2x");
        syncGameToggle.tooltip = "Continuously sync GameView camera to SceneView";

        syncSceneToGameButton = EditorGUIUtility.IconContent("d_tab_next@2x");
        syncSceneToGameButton.tooltip = "Sync SceneView to GameView camera now";

        syncGameToSceneButton = EditorGUIUtility.IconContent("d_tab_prev@2x");
        syncGameToSceneButton.tooltip = "Sync GameView camera to SceneView now";

        SceneView.duringSceneGui -= Sync;
        SceneView.duringSceneGui += Sync;
        AssemblyReloadEvents.beforeAssemblyReload += UnsubscribeDuringSceneGui;
    }

    // 域重载前解订阅，避免在 UnityEditor 的静态事件上泄漏僵尸 handler
    private void UnsubscribeDuringSceneGui()
    {
        SceneView.duringSceneGui -= Sync;
        AssemblyReloadEvents.beforeAssemblyReload -= UnsubscribeDuringSceneGui;
    }

	protected override void OnDrawInList(Rect position) 
    {

	}

	protected override void OnDrawInToolbar()
    {
        bool guiEnable = GUI.enabled;

        GUI.enabled = true;
        syncSceneToGame = GUILayout.Toggle(syncSceneToGame, syncSceneToggle, ToolbarStyles.commandButtonStyle);
        if (syncSceneToGame)
        {
            syncGameToScene = false;
        }

        GUI.enabled = !(syncSceneToGame || syncGameToScene);
        if (GUILayout.Button(syncGameToSceneButton, ToolbarStyles.commandButtonStyle))
        {
            SyncGameToScene();
        }
        
        if (GUILayout.Button(syncSceneToGameButton, ToolbarStyles.commandButtonStyle))
        {
            SyncSceneToGame();
        }

        GUI.enabled = true;
        syncGameToScene = GUILayout.Toggle(syncGameToScene, syncGameToggle, ToolbarStyles.commandButtonStyle);
        if (syncGameToScene)
        {
            syncSceneToGame = false;
        }

        GUI.enabled = true;
        DrawCameraDropdown();

        GUI.enabled = guiEnable;
    }

    public void Sync(SceneView sceneView)
    {
        if (syncSceneToGame)
        {
            SyncSceneToGame();
        }

        if (syncGameToScene)
        {
            SyncGameToScene();
        }
    }
    private void SyncSceneToGame()
    {
        if (sceneView == null)
        {
            return;
        }

        Camera cam = gameViewCamera;
        if (cam == null)
        {
            return;
        }

        var cameraSettings = sceneView.cameraSettings;
        Transform t = cam.transform;

        Quaternion desiredRot = sceneView.rotation;
        Vector3 desiredPos = sceneView.pivot - (desiredRot * Vector3.forward) * sceneView.cameraDistance;

        bool clipChanged = !Mathf.Approximately(cam.nearClipPlane, cameraSettings.nearClip)
                        || !Mathf.Approximately(cam.farClipPlane, cameraSettings.farClip);
        bool fovChanged = !Mathf.Approximately(cam.fieldOfView, cameraSettings.fieldOfView);
        bool transformChanged = t.rotation != desiredRot
                             || (t.position - desiredPos).sqrMagnitude > 1e-10f;

        // 仅当实际变化时写入（避免每事件把游戏相机标 dirty，且可 Undo）
        if (!clipChanged && !fovChanged && !transformChanged)
        {
            return;
        }

        Undo.RecordObject(cam, "Sync View");
        Undo.RecordObject(t, "Sync View");

        if (clipChanged)
        {
            cam.nearClipPlane = cameraSettings.nearClip;
            cam.farClipPlane = cameraSettings.farClip;
        }

        if (fovChanged)
        {
            cam.fieldOfView = cameraSettings.fieldOfView;
        }

        if (transformChanged)
        {
            t.rotation = desiredRot;
            t.position = desiredPos;
        }

        EditorUtility.SetDirty(cam);
    }
    private void SyncGameToScene()
    {
        if (sceneView == null)
        {
            return;
        }

        Camera cam = gameViewCamera;
        if (cam == null)
        {
            return;
        }

        // 仅当游戏相机真的变化了才同步 SceneView（开关开启时，用户可以自由导航 SceneView）
        if (!TryDetectGameCameraChange(cam))
        {
            return;
        }

        var cameraSettings = sceneView.cameraSettings;

        // 近远裁剪面只在 SceneView 未开启 dynamicClip 时生效，直接跳过无效写入
        if (!cameraSettings.dynamicClip)
        {
            cameraSettings.nearClip = cam.nearClipPlane;
            cameraSettings.farClip = cam.farClipPlane;
        }

        cameraSettings.fieldOfView = cam.fieldOfView;

        float distance = sceneView.cameraDistance;
        Transform t = cam.transform;
        var desiredPivot = t.position + t.forward * distance;

        if (sceneView.pivot != desiredPivot)
        {
            sceneView.pivot = desiredPivot;
        }

        if (sceneView.rotation != t.rotation)
        {
            sceneView.rotation = t.rotation;
        }
    }

    // 记录上次应用的游戏相机状态，仅当游戏相机的任何值变化时函数才返回 true 并更新快照
    private static bool TryDetectGameCameraChange(Camera cam)
    {
        Transform t = cam.transform;

        bool posChanged = t.position != s_lastGameCamPos;
        bool rotChanged = t.rotation != s_lastGameCamRot;
        bool fovChanged = !Mathf.Approximately(cam.fieldOfView, s_lastGameCamFov);
        bool clipChanged = !Mathf.Approximately(cam.nearClipPlane, s_lastGameCamNear)
                        || !Mathf.Approximately(cam.farClipPlane, s_lastGameCamFar);

        bool anyChanged = posChanged || rotChanged || fovChanged || clipChanged || !s_lastGameCamStateInit;
        if (!anyChanged)
        {
            return false;
        }

        s_lastGameCamStateInit = true;
        s_lastGameCamPos = t.position;
        s_lastGameCamRot = t.rotation;
        s_lastGameCamFov = cam.fieldOfView;
        s_lastGameCamNear = cam.nearClipPlane;
        s_lastGameCamFar = cam.farClipPlane;
        return true;
    }

    #region 相机选择（SessionState 持久化，默认 MainCamera）

    private static IEnumerable<Scene> LoadedScenes
    {
        get
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) yield return scene;
            }
        }
    }

    // 按 Hierarchy 的深度优先顺序枚举场景内相机（与 Hierarchy 面板中从上到下的顺序一致）
    private static IEnumerable<Camera> EnumerateSceneCameras(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects().OrderBy(go => go.transform.GetSiblingIndex()))
        {
            foreach (Camera cam in EnumerateHierarchyOrder(root.transform))
            {
                yield return cam;
            }
        }
    }

    // 按 Hierarchy 的深度优先顺序枚举（与 Hierarchy 面板中从上到下的顺序一致）
    private static IEnumerable<Camera> EnumerateHierarchyOrder(Transform t)
    {
        Camera cam = t.GetComponent<Camera>();
        if (cam != null)
        {
            yield return cam;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            foreach (Camera childCam in EnumerateHierarchyOrder(t.GetChild(i)))
            {
                yield return childCam;
            }
        }
    }

    // 显示用：未保存场景的 name 是空串，Unity 层级上显示为 "Untitled"
    private static string SceneDisplayName(Scene scene)
    {
        return string.IsNullOrEmpty(scene.name) ? "Untitled (unsaved)" : scene.name;
    }

    // 存储 key 用：未保存场景按 handle 区分，避免多个未保存场景路径冲突
    private static string SceneStorageKey(Scene scene)
    {
        return string.IsNullOrEmpty(scene.name) ? "Untitled[" + scene.handle + "]" : scene.name;
    }

    private static string GetCameraPath(Camera camera)
    {
        string path = camera.gameObject.name;
        Transform t = camera.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }

        return SceneStorageKey(camera.gameObject.scene) + "/" + path;
    }

    private static Camera FindCameraByPath(string storedPath)
    {
        if (string.IsNullOrEmpty(storedPath))
        {
            return null;
        }

        foreach (Scene scene in LoadedScenes)
        {
            Camera found = EnumerateSceneCameras(scene)
                .FirstOrDefault(cam => GetCameraPath(cam) == storedPath);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // 按存储的 path key 缓存解析结果；仅当选择变化或相机已被销毁时重新枚举场景
    private static Camera GetSelectedCamera()
    {
        string stored = SessionState.GetString(CameraSelectionKey, string.Empty);

        if (s_resolvedSelectionKey == stored)
        {
            // 未找到（null 由 Unity fake-null 判断）时不复用缓存，重新解析
            if (s_resolvedCamera != null)
            {
                return s_resolvedCamera;
            }
        }

        s_resolvedSelectionKey = stored;
        if (string.IsNullOrEmpty(stored))
        {
            s_resolvedCamera = Camera.main;
        }
        else
        {
            s_resolvedCamera = FindCameraByPath(stored) ?? Camera.main;
        }

        return s_resolvedCamera;
    }

    private static void SelectCamera(Camera camera)
    {
        SessionState.SetString(CameraSelectionKey, GetCameraPath(camera));
    }

    private static void DrawCameraDropdown()
    {
        Camera current = GetSelectedCamera();

        if (s_cameraPopupContent == null)
        {
            s_cameraPopupContent = new GUIContent();
        }

        s_cameraPopupContent.text = current != null ? current.gameObject.name : "<No camera>";
        s_cameraPopupContent.tooltip = "GameView camera used for camera sync (default: Main Camera)";

        if (GUILayout.Button(s_cameraPopupContent, EditorStyles.toolbarPopup, GUILayout.Width(140f)))
        {
            ShowCameraMenu(current);
        }
    }

    private static void ShowCameraMenu(Camera current)
    {
        List<Scene> scenes = LoadedScenes.ToList();
        // 多个场景（additive）才按场景分组；只有一个场景时平铺
        bool groupByScene = scenes.Count > 1;
        bool any = false;

        GenericMenu menu = new GenericMenu();
        foreach (Scene scene in scenes)
        {
            List<Camera> sceneCameras = EnumerateSceneCameras(scene).ToList();
            if (sceneCameras.Count == 0)
            {
                continue;
            }

            foreach (Camera cam in sceneCameras)
            {
                any = true;
                string path = GetCameraPath(cam);
                // 多场景：两级菜单（第一级场景名，第二级该场景的相机列表）；单场景：平铺
                string label = groupByScene
                    ? SceneDisplayName(scene) + "/" + cam.gameObject.name
                    : cam.gameObject.name;
                bool selectable = cam.enabled && cam.gameObject.activeInHierarchy;
                if (!selectable)
                {
                    menu.AddDisabledItem(new GUIContent(label + "  (disabled)", path));
                    continue;
                }

                menu.AddItem(new GUIContent(label, path), cam == current, () => SelectCamera(cam));
            }
        }

        if (!any)
        {
            menu.AddDisabledItem(new GUIContent("<No camera>"));
        }

        menu.ShowAsContext();
    }

    #endregion
}
