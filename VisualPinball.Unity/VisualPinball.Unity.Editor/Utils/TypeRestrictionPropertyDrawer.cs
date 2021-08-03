// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(TypeRestrictionAttribute))]
	public class TypeRestrictionPropertyDrawer : PropertyDrawer
	{
		private MonoBehaviour _component;
		private AdvancedDropdownState _itemPickDropdownState;

		public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
		{

			#region Sanity Checks

			if (property.propertyType != SerializedPropertyType.ObjectReference) {
				Debug.LogError("[TypeRestriction] attribute must be on an object reference.");
				return;
			}

			if (!(attribute is TypeRestrictionAttribute attrib)) {
				return;
			}

			var comp = property.serializedObject.targetObject as Component;
			if (comp == null) {
				Debug.LogError($"Cannot find component of {property.serializedObject.targetObject.name}.");
				return;
			}


			#endregion

			// find components
			var go = comp.gameObject;
			var ta = go.GetComponentInParent<TableAuthoring>();

			var field = property.objectReferenceValue;
			pos = EditorGUI.PrefixLabel(pos, label);

			// retrieve selected reference
			MonoBehaviour obj = null;
			if (_component == null) {
				if (field != null) {
					obj = ta.GetComponentsInChildren(attrib.Type, true)
						.FirstOrDefault(s => s == field) as MonoBehaviour;
					_component = obj;
				}
			} else {
				obj = _component;
			}

			// render content
			var content = obj == null
				? new GUIContent(attrib.NoneLabel)
				: new GUIContent(obj.name, Icons.ByComponent(obj, IconSize.Small, IconColor.Orange));

			// render drawer
			var id = GUIUtility.GetControlID(FocusType.Keyboard, pos);
			var objectFieldButton = GUI.skin.GetStyle("ObjectFieldButton");
			var suffixButtonPos = new Rect(pos.xMax - 19f, pos.y + 1, 19f, pos.height - 2);
			EditorGUIUtility.SetIconSize(new Vector2(12f, 12f));

			// handle click
			if (Event.current.type == EventType.MouseDown && pos.Contains(Event.current.mousePosition)) {

				// click on ping
				if (obj != null && !suffixButtonPos.Contains(Event.current.mousePosition)) {
					EditorGUIUtility.PingObject(obj.gameObject);

				// click on picker
				} else {
					_itemPickDropdownState ??= new AdvancedDropdownState();

					var dropdown = new ItemSearchableDropdownUntyped(
						attrib.Type,
						_itemPickDropdownState,
						ta,
						attrib.PickerLabel,
						item => {
							switch (item) {
								case null:
									_component = null;
									property.objectReferenceValue = null;
									break;
								case MonoBehaviour mb:
									_component = mb;
									property.objectReferenceValue = mb;
									break;
							}
							property.serializedObject.ApplyModifiedProperties();
							if (comp is IItemMainRenderableAuthoring renderable) {
								if (attrib.RebuildMeshes) {
									renderable.RebuildMeshes();
								}
								if (attrib.UpdateTransforms) {
									renderable.UpdateTransforms();
								}
							}
						}
					);
					dropdown.Show(pos);
				}
			}
			if (Event.current.type == EventType.Repaint) {
				EditorStyles.objectField.Draw(pos, content, id, DragAndDrop.activeControlID == id, pos.Contains(Event.current.mousePosition));
				objectFieldButton.Draw(suffixButtonPos, GUIContent.none, id, DragAndDrop.activeControlID == id, suffixButtonPos.Contains(Event.current.mousePosition));
			}
		}

		private class ItemSearchableDropdownUntyped : AdvancedDropdown
		{
			private readonly string _title;

			private readonly TableAuthoring _tableAuthoring;

			private readonly Action<UnityEngine.Object> _onElementSelected;

			private readonly Type _type;

			public ItemSearchableDropdownUntyped(Type type, AdvancedDropdownState state, TableAuthoring tableAuthoring, string title, Action<UnityEngine.Object> onElementSelected) : base(state)
			{
				_type = type;
				_onElementSelected = onElementSelected;
				_tableAuthoring = tableAuthoring;
				minimumSize = new Vector2(200, 300);
				_title = title;
			}

			protected override AdvancedDropdownItem BuildRoot()
			{
				var node = new AdvancedDropdownItem(_title);
				var elements = _tableAuthoring.GetComponentsInChildren(_type);
				node.AddChild(new ElementDropdownItem(null));
				foreach (var element in elements) {
					node.AddChild(new ElementDropdownItem(element));
				}
				return node;
			}

			protected override void ItemSelected(AdvancedDropdownItem item)
			{
				var elementItem = (ElementDropdownItem) item;
				_onElementSelected?.Invoke(elementItem?.Item);
			}

			private class ElementDropdownItem : AdvancedDropdownItem
			{
				public readonly UnityEngine.Object Item;

				public ElementDropdownItem(UnityEngine.Object element) : base(element == null ? "None" : element.name)
				{
					if (element != null) {
						Item = element;
						icon = Icons.ByComponent(element, color: IconColor.Gray, size: IconSize.Small);
					}
				}
			}
		}
	}
}
