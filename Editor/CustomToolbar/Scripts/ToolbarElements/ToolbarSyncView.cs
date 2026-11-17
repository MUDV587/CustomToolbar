using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
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

    private static Camera s_GameViewCamera = null;
    private static Camera gameViewCamera
    {
        get
        {
            if (s_GameViewCamera == null)
            {
                s_GameViewCamera = Camera.main;
            }

            return s_GameViewCamera;
        }
    }

    public override void Init()
    {
        syncSceneToggle = EditorGUIUtility.IconContent("UnityEditor.SceneView@2x");
        syncGameToggle = EditorGUIUtility.IconContent("UnityEditor.GameView@2x");

        syncSceneToGameButton = EditorGUIUtility.IconContent("d_tab_next@2x");
        syncGameToSceneButton = EditorGUIUtility.IconContent("d_tab_prev@2x");

        SceneView.duringSceneGui -= Sync;
        SceneView.duringSceneGui += Sync;
    }

	protected override void OnDrawInList(Rect position) 
    {

	}

	protected override void OnDrawInToolbar() 
    {
        Init();

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
        if (sceneView == null || gameViewCamera == null)
        {
            return;
        }

        var cameraSettings = sceneView.cameraSettings;
        var t = gameViewCamera.transform;

        gameViewCamera.nearClipPlane = cameraSettings.nearClip;
        gameViewCamera.farClipPlane = cameraSettings.farClip;
        gameViewCamera.fieldOfView = cameraSettings.fieldOfView;

        t.rotation = sceneView.rotation;
        t.position = sceneView.pivot - t.forward * sceneView.cameraDistance;
    }
    private void SyncGameToScene()
    {
        if (sceneView == null || gameViewCamera == null)
        {
            return;
        }

        var cameraSettings = sceneView.cameraSettings;
        var t = gameViewCamera.transform;

        cameraSettings.nearClip = gameViewCamera.nearClipPlane;
        cameraSettings.farClip = gameViewCamera.farClipPlane;
        cameraSettings.fieldOfView = gameViewCamera.fieldOfView;

        sceneView.pivot = t.position + t.forward * sceneView.cameraDistance;
        sceneView.rotation = t.rotation;
    }
}
