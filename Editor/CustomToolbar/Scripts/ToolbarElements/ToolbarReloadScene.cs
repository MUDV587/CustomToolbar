using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityToolbarExtender;

[Serializable]
internal class ToolbarReloadScene : BaseToolbarElement {
	private static GUIContent reloadSceneBtn;

	public override string NameInList => "[Button] Reload scene";
	public override int SortingGroup => 3;

	public override void Init() {
		reloadSceneBtn = EditorGUIUtility.IconContent("RotateTool On");
		reloadSceneBtn.tooltip = "Reload scene";
	}

	protected override void OnDrawInList(Rect position) {

	}

	protected override void OnDrawInToolbar() {
		if (GUILayout.Button(reloadSceneBtn, ToolbarStyles.commandButtonStyle)) {
			if (EditorApplication.isPlaying) {
				SceneManager.LoadScene(SceneManager.GetActiveScene().name);
			}
		}
	}
}
