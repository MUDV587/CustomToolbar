using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[Serializable]
internal class ToolbarReserializeSelected : BaseToolbarElement {
	private static GUIContent reserializeSelectedBtn;

	public override string NameInList => "[Button] Reserialize selected";
	public override int SortingGroup => 5;

	public override void Init() {
		reserializeSelectedBtn = EditorGUIUtility.IconContent("d_winbtn_win_max_h@2x");
		reserializeSelectedBtn.tooltip = "Reserialize Selected Assets";
	}

	protected override void OnDrawInList(Rect position) {

	}

	protected override void OnDrawInToolbar() {
		if (GUILayout.Button(reserializeSelectedBtn, UnityToolbarExtender.ToolbarStyles.commandButtonStyle)) {
			UnityToolbarExtender.ForceReserializeAssetsUtils.ForceReserializeSelectedAssets();
		}
	}
}
