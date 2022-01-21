using System.Collections.Generic;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(OnScreenInputSystemButton))]
	public class OnScreenInputSystemButtonInspector : UnityEditor.Editor
	{ 
		private InputManager _inputManager;

		private SerializedProperty _inputActionMapNameProperty;
		private SerializedProperty _inputActionNameProperty;

		private struct InputSystemEntry
		{
			public string ActionMapName;
			public string ActionName;
		}

		private void OnEnable()
		{
			_inputManager = new InputManager(InputManager.ASSETS_RESOURCES_PATH);

			_inputActionMapNameProperty = serializedObject.FindProperty(nameof(OnScreenInputSystemButton.InputActionMapName));
			_inputActionNameProperty = serializedObject.FindProperty(nameof(OnScreenInputSystemButton.InputActionName));
		}

		public override void OnInspectorGUI()
		{
			var inputSystemList = new List<InputSystemEntry>();
			var tmpIndex = 0;
			var selectedIndex = -1;
			var options = new List<string>();

			foreach (var actionMapName in _inputManager.GetActionMapNames())
			{
				if (options.Count > 0)
				{
					options.Add("");
					inputSystemList.Add(new InputSystemEntry());
					tmpIndex++;
				}

				foreach (var actionName in _inputManager.GetActionNames(actionMapName))
				{
					inputSystemList.Add(new InputSystemEntry
					{
						ActionMapName = actionMapName,
						ActionName = actionName
					});

					options.Add(actionName.Replace('/', '\u2215'));

					if (actionMapName == _inputActionMapNameProperty.stringValue && actionName == _inputActionNameProperty.stringValue)
					{
						selectedIndex = tmpIndex;
					}

					tmpIndex++;
				}
			}

			EditorGUI.BeginChangeCheck();

			var index = EditorGUILayout.Popup("Input System", selectedIndex, options.ToArray());
			if (EditorGUI.EndChangeCheck())
			{
				_inputActionMapNameProperty.stringValue = inputSystemList[index].ActionMapName;
				_inputActionMapNameProperty.serializedObject.ApplyModifiedProperties();

				_inputActionNameProperty.stringValue = inputSystemList[index].ActionName;
				_inputActionNameProperty.serializedObject.ApplyModifiedProperties();
			}
		}
	}
}
