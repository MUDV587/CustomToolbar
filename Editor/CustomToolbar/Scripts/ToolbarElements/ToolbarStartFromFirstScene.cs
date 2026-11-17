using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityToolbarExtender;

[Serializable]
internal class ToolbarStartFromFirstScene : BaseToolbarElement {
	private static GUIContent s_startFromFirstSceneBtn;
	private static GUIContent startFromFirstSceneBtn
	{
		get
		{
			if (s_startFromFirstSceneBtn == null)
			{
				s_startFromFirstSceneBtn = new GUIContent((Texture)null, "Start from 1 scene");
			}

			if (s_startFromFirstSceneBtn.image == null)
			{
				s_startFromFirstSceneBtn.image = AssetDatabase.LoadAssetAtPath<Texture>($"{GetPackageRootPath}/Editor/CustomToolbar/Icons/LookDevSingle1@2x.png");
			}

			return s_startFromFirstSceneBtn;
		}
	}

	public override string NameInList => "[Button] Start from first scene";
	public override int SortingGroup => 3;

	public override void Init() {
		EditorApplication.playModeStateChanged += LogPlayModeState;
	}

	protected override void OnDrawInList(Rect position) {

	}

	protected override void OnDrawInToolbar() {
		if (GUILayout.Button(startFromFirstSceneBtn, ToolbarStyles.commandButtonStyle)) {
			if (!EditorApplication.isPlaying) {
				if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				{
					EditorApplication.isPlaying = false;
					return;
				}
				var scenePath = SceneUtility.GetScenePathByBuildIndex(0);
				if (string.IsNullOrEmpty(scenePath))
				{
					Debug.LogError("Can not find Scene of index 0 in Build Settings");
					return;
				}
				SessionState.SetString(LastActiveSceneToolbar, EditorSceneManager.GetActiveScene().path);
				EditorSceneManager.OpenScene(scenePath);
			}

			EditorApplication.isPlaying = !EditorApplication.isPlaying;
		}
	}
	
	private const string LastActiveSceneToolbar = "LastActiveSceneToolbar";
	private static void LogPlayModeState(PlayModeStateChange state)
	{
		if (state == PlayModeStateChange.EnteredEditMode)
		{
			if(string.IsNullOrWhiteSpace(SessionState.GetString(LastActiveSceneToolbar,string.Empty))) return;
			EditorSceneManager.OpenScene(SessionState.GetString(LastActiveSceneToolbar,string.Empty));
			SessionState.EraseString(LastActiveSceneToolbar);
		}
	}
}