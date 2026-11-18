using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityToolbarExtender;

[Serializable]
internal class ToolbarClearPrefs : BaseToolbarElement {
	private static GUIContent clearPlayerPrefsBtn;

	public override string NameInList => "[Button] Clear prefs";
	public override int SortingGroup => 4;

	public override void Init() {
		clearPlayerPrefsBtn = EditorGUIUtility.IconContent("SaveFromPlay");
		clearPlayerPrefsBtn.tooltip = "Clear player prefs";
	}

	protected override void OnDrawInList(Rect position) {

	}

	protected override void OnDrawInToolbar() {
		if (GUILayout.Button(clearPlayerPrefsBtn, ToolbarStyles.commandButtonStyle)) {
			if (EditorUtility.DisplayDialog("Attention", "This will delete ALL saved PlayerPrefs of this project. Continue?", "Delete", "Cancel")) {
				PlayerPrefs.DeleteAll();
				Debug.Log("Clear Player Prefs");
			}
		}
	}
}
