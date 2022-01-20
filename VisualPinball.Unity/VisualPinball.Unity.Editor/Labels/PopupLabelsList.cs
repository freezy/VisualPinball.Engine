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
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	public class PopupLabelList : PopupList<PopupListElement>
	{
		private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private PopupListElement _selectedElement = null;
		private string _selectedCategory = string.Empty;
		private Dictionary<string, List<PopupListElement>> _categoryDictionary = new Dictionary<string, List<PopupListElement>>();
		private GUIStyle _categoryStyle;

		private void CreateStyles()
		{
			_categoryStyle = new GUIStyle("MenuItem");
		}

		public PopupLabelList(InputData inputData) : base(inputData)
		{
			CreateStyles();
			BuildCategoryDictionary("");
		}

		public PopupLabelList(InputData inputData, string initialSelectionLabel) : base(inputData, initialSelectionLabel)
		{
			CreateStyles();
			BuildCategoryDictionary("");
		}

		public override float GetWindowHeight()
		{
			int count = _categoryDictionary.Keys.Count(K => !string.IsNullOrEmpty(K));
			count += _categoryDictionary.Values.Sum(L=>L.Count);
			return count * k_LineHeight + 2 * k_Margin + (m_Data.m_AllowCustom ? k_TextFieldHeight : 0);
		}

		private void BuildCategoryDictionary(string filterText)
		{
			_categoryDictionary.Clear();
			foreach (var element in m_Data.GetFilteredList(filterText)) {
				var tuple = PinballLabel.Split(element.text);
				if (!_categoryDictionary.Keys.Contains(tuple.Item1)) {
					_categoryDictionary.Add(tuple.Item1, new List<PopupListElement>());
				}
				_categoryDictionary[tuple.Item1].Add(element);
			}
			Logger.Debug($"Category Dictionary Rebuilt :\nKeys [{string.Join(",", _categoryDictionary.Keys.ToArray())}]");
		}

		protected override void OnFilterTextChange(string oldText, string newText) 
		{
			BuildCategoryDictionary(newText);
		}

		protected override void DrawList(EditorWindow editorWindow, Rect windowRect)
		{
			Event evt = Event.current;

			var categoryDictionary = new Dictionary<string, List<PopupListElement>>();
			foreach (var element in m_Data.GetFilteredList(m_EnteredText)) {
				var tuple = PinballLabel.Split(element.text);
				if (!categoryDictionary.Keys.Contains(tuple.Item1)) {
					categoryDictionary.Add(tuple.Item1, new List<PopupListElement>());
				}
				categoryDictionary[tuple.Item1].Add(element);
			}

			int i = -1;
			Rect rect = new Rect();

			foreach (var category in categoryDictionary.Keys) {
				if (!string.IsNullOrEmpty(category)) {
					i++;
					rect.Set(windowRect.x, windowRect.y + k_Margin + i * k_LineHeight + (m_Gravity == Gravity.Top && m_Data.m_AllowCustom ? k_TextFieldHeight : 0), windowRect.width, k_LineHeight);
					HandleCategory(evt, category, rect);
				}

				foreach (var element in categoryDictionary[category]) {
					i++;
					rect.Set(windowRect.x, windowRect.y + k_Margin + i * k_LineHeight + (m_Gravity == Gravity.Top && m_Data.m_AllowCustom ? k_TextFieldHeight : 0), windowRect.width, k_LineHeight);
					HandleLabel(evt, category, element, rect);
				}
			}
		}

		private void HandleLabel(Event evt, string category, PopupListElement element, Rect rect)
		{
			switch (evt.type) {
				case EventType.Repaint: {
					GUIStyle style = element.style;
					bool selected = element.selected || element.partiallySelected;
					bool isHover = element == _selectedElement;
					bool isActive = selected;

					using (new EditorGUI.DisabledScope(!element.enabled)) {
						var label = PinballLabel.Split(element.text);
						GUIContent content = new GUIContent($"{(string.IsNullOrEmpty(category) ? string.Empty : "    ")}{label.Item3}");
						style.Draw(rect, content, isHover, isActive, selected, false);
					}
				}
				break;
				case EventType.MouseDown: {
					if (Event.current.button == 0 && rect.Contains(Event.current.mousePosition) && element.enabled) {
						// Toggle state
						if (m_Data.m_OnSelectCallback != null)
							m_Data.m_OnSelectCallback(element);

						evt.Use();

						// Auto close
						if (m_Data.m_CloseOnSelection)
							editorWindow.Close();
					}
				}
				break;
				case EventType.MouseMove: {
					if (rect.Contains(Event.current.mousePosition)) {
						_selectedElement = element;
						_selectedCategory = string.Empty;
						//SelectCompletionWithIndex(i);
						evt.Use();
					}
				}
				break;
			}
		}

		private void HandleCategory(Event evt, string category, Rect rect)
		{
			switch (evt.type) {
				case EventType.Repaint: {
					bool isHover = category == _selectedCategory;

					using (new EditorGUI.DisabledScope()) {
						GUIContent content = new GUIContent(category);
						_categoryStyle.Draw(rect, content, isHover, true, false, false);
					}
				}
				break;
				case EventType.MouseDown: { 
					if (Event.current.button == 0 && rect.Contains(Event.current.mousePosition)) {
						//TODO Set the filterText to categoryname + '.'
						evt.Use();
					}
				}
				break;
				case EventType.MouseMove: {
					if (rect.Contains(Event.current.mousePosition)) {
						_selectedCategory = category;
						_selectedElement = null;
						//SelectCompletionWithIndex(i);
						evt.Use();
					}
				}
				break;
			}
		}
	}
}
