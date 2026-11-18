using System;
using UnityEngine;
using UnityEditor;
using System.Reflection;

#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif

namespace UnityToolbarExtender
{
	public static class ToolbarCallback
	{
		public static Type m_toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
		public static Type m_guiViewType = typeof(Editor).Assembly.GetType("UnityEditor.GUIView");
#if UNITY_2020_1_OR_NEWER
		public static Type m_iWindowBackendType = typeof(Editor).Assembly.GetType("UnityEditor.IWindowBackend");
		public static PropertyInfo m_windowBackend = m_guiViewType?.GetProperty("windowBackend",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		public static PropertyInfo m_viewVisualTree = m_iWindowBackendType?.GetProperty("visualTree",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#else
		public static PropertyInfo m_viewVisualTree = m_guiViewType?.GetProperty("visualTree",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#endif
		public static FieldInfo m_imguiContainerOnGui = typeof(IMGUIContainer).GetField("m_OnGUIHandler",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		public static ScriptableObject m_currentToolbar;

		/// <summary>
		/// Callback for toolbar OnGUI method.
		/// </summary>
		public static Action OnToolbarGUI;
		public static Action OnToolbarGUILeft;
		public static Action OnToolbarGUIRight;

		static ToolbarCallback()
		{
			EditorApplication.update -= OnUpdate;
			EditorApplication.update += OnUpdate;
		}

		private static void OnUpdate()
		{
			// Unity 6 起 toolbar 类型已被重写；拿不到类型时静默降级而不是每帧 NRE
			if (m_toolbarType == null)
			{
				return;
			}

			// Relying on the fact that toolbar is ScriptableObject and gets deleted when layout changes
			if (m_currentToolbar == null)
			{
				// Find toolbar
				var toolbars = Resources.FindObjectsOfTypeAll(m_toolbarType);
				m_currentToolbar = toolbars.Length > 0 ? (ScriptableObject) toolbars[0] : null;

				if (m_currentToolbar != null)
				{
#if UNITY_2021_1_OR_NEWER
					var root = m_currentToolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
					if (root == null)
					{
						Debug.LogWarning("CustomToolbar: Unable to resolve toolbar root via reflection. Toolbar won't render.");
						return;
					}
					var rawRoot = root.GetValue(m_currentToolbar);
					var mRoot = rawRoot as VisualElement;
					if (mRoot == null)
					{
						Debug.LogWarning("CustomToolbar: Toolbar root is not a VisualElement. Toolbar won't render.");
						return;
					}

					void RegisterCallback(string root, Action cb)
					{
						var toolbarZone = mRoot.Q(root);
						if (toolbarZone == null)
						{
							Debug.LogWarning($"CustomToolbar: Toolbar zone '{root}' was not found. Toolbar won't render.");
							return;
						}

						var parent = new VisualElement()
						{
							style = {
								flexGrow = 1,
								flexDirection = FlexDirection.Row,
							}
						};
						var container = new IMGUIContainer();
						container.onGUIHandler += () => { 
							cb?.Invoke();
						}; 
						parent.Add(container);
						toolbarZone.Add(parent);
					}

					RegisterCallback("ToolbarZoneLeftAlign", OnToolbarGUILeft);
					RegisterCallback("ToolbarZoneRightAlign", OnToolbarGUIRight);
#else
#if UNITY_2020_1_OR_NEWER
					var windowBackend = m_windowBackend?.GetValue(m_currentToolbar);
					if (windowBackend == null || m_viewVisualTree == null)
					{
						Debug.LogWarning("CustomToolbar: Unable to resolve toolbar internals via reflection. Toolbar won't render.");
						return;
					}

					// Get it's visual tree
					var visualTree = (VisualElement) m_viewVisualTree.GetValue(windowBackend, null);
#else
					if (m_viewVisualTree == null)
					{
						Debug.LogWarning("CustomToolbar: Unable to resolve toolbar internals via reflection. Toolbar won't render.");
						return;
					}
					// Get it's visual tree
					var visualTree = (VisualElement) m_viewVisualTree.GetValue(m_currentToolbar, null);
#endif
					if (visualTree == null || visualTree.childCount == 0 || m_imguiContainerOnGui == null)
					{
						Debug.LogWarning("CustomToolbar: Unable to resolve toolbar IMGUIContainer via reflection. Toolbar won't render.");
						return;
					}
					// Get first child which 'happens' to be toolbar IMGUIContainer
					var container = (IMGUIContainer) visualTree[0];

					// (Re)attach handler
					var handler = (Action) m_imguiContainerOnGui.GetValue(container);
					handler -= OnGUI;
					handler += OnGUI;
					m_imguiContainerOnGui.SetValue(container, handler);
#endif
				}
			}
		}

		static void OnGUI()
		{
			var handler = OnToolbarGUI;
			if (handler != null) handler();
		}
	}
}
