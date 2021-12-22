
using System;
using System.ComponentModel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class PinballTag : ISerializationCallbackReceiver
	{
		[SerializeField]
		private string _fullTag = string.Empty;
		public string FullTag {
			get { return _fullTag; }
			set { _fullTag = value; Build(); }
		}
		[field: NonSerialized]
		public string Tag { get; private set; } = string.Empty;
		[field: NonSerialized]
		public string Category { get; private set; } = string.Empty;

		public PinballTag(string fullTag)
		{
			_fullTag = fullTag;
			Build();
		}

		public void OnBeforeSerialize()		{}

		public void OnAfterDeserialize()	{ Build(); }

		private void Build()
		{
			if (string.IsNullOrEmpty(_fullTag)) {
				Tag = string.Empty;
				Category = string.Empty;
				return;
			}
			var split = _fullTag.Split('.');
			if (split.Length > 1) {
				if (split.Length > 2) {
					Category = string.Join('_', split, 0, split.Length - 1);
				} else {
					Category = split[0];
				}
				Tag = split[split.Length - 1];
			}
		}
	}

	//[CustomPropertyDrawer(typeof(PinballTag))]
	//public class PinballTagPropertyDrawer : PropertyDrawer
	//{
	//	private string _tag = string.Empty;
	//	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	//	{
	//		var prop = property.FindPropertyRelative("FullTag");
	//		_tag = prop.stringValue;

	//		Rect r = EditorGUI.PrefixLabel(position, label);

	//		Rect textFieldRect = r;
	//		textFieldRect.width -= 19f;

	//		GUIStyle textFieldStyle = new GUIStyle("TextField") {
	//			imagePosition = ImagePosition.TextOnly
	//		};

	//		_tag = GUI.TextField(textFieldRect, _tag);
	//	}
	//}
}
