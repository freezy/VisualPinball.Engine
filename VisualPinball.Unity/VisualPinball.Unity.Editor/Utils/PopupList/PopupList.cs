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

namespace VisualPinball.Unity.Editor
{
	public class PopupListElement
	{
		public GUIContent m_Content;
		private float m_FilterScore = -1;
		private bool m_Selected = false;
		private bool m_WasSelected = false;
		private bool m_PartiallySelected = false;
		private bool m_Enabled = true;

		public PopupListElement()
		{
		}

		public PopupListElement(string text, bool selected, float score)
			: base()
		{
			m_Content = new GUIContent(text);
			if (!string.IsNullOrEmpty(m_Content.text)) {
				char[] a = m_Content.text.ToCharArray();
				a[0] = char.ToUpper(a[0]);
				m_Content.text = new string(a);
			}
			m_Selected = selected;
			filterScore = score;
			m_PartiallySelected = false;
			m_Enabled = true;
		}

		public PopupListElement(string text, bool selected)
		{
			m_Content = new GUIContent(text);
			m_Selected = selected;
			filterScore = 0;
			m_PartiallySelected = false;
			m_Enabled = true;
		}

		public PopupListElement(string text) : this(text, false)
		{
		}

		public float filterScore
		{
			get {
				return m_WasSelected ? float.MaxValue : m_FilterScore;
			}
			set {
				m_FilterScore = value;
				ResetScore();
			}
		}

		public bool selectable => true;

		public bool selected
		{
			get {
				return selectable && m_Selected;
			}
			set {
				m_Selected = value;
				if (m_Selected)
					m_WasSelected = true;
			}
		}

		public bool enabled
		{
			get {
				return m_Enabled;
			}
			set {
				m_Enabled = value;
			}
		}

		public bool partiallySelected
		{
			get {
				return selectable && m_PartiallySelected;
			}
			set {
				m_PartiallySelected = value;
				if (m_PartiallySelected)
					m_WasSelected = true;
			}
		}

		public string text
		{
			get {
				return m_Content.text;
			}
			set {
				m_Content.text = value;
			}
		}

		public GUIStyle style
		{
			get {
				return partiallySelected ? "MenuItemMixed" : "MenuItem";
			}
		}

		public float width
		{
			get {
				float min, max;
				style.CalcMinMaxWidth(m_Content, out min, out max);
				return max;
			}
		}
		public void ResetScore()
		{
			m_WasSelected = m_Selected || m_PartiallySelected;
		}
	}

	public class PopupList<T> : PopupWindowContent where T : PopupListElement, new()
	{
		public delegate void OnSelectCallback(T element);
		string TextFieldText = string.Empty;
		public enum Gravity
		{
			Top,
			Bottom
		}

		public class InputData
		{
			public List<T> m_ListElements;
			public bool m_CloseOnSelection;
			public bool m_AllowCustom;
			public bool m_EnableAutoCompletion = true;
			public bool m_SortAlphabetically;
			public OnSelectCallback m_OnSelectCallback;
			public int m_MaxCount;

			public InputData()
			{
				m_ListElements = new List<T>();
			}

			public void DeselectAll()
			{
				foreach (T element in m_ListElements) {
					element.selected = false;
					element.partiallySelected = false;
				}
			}

			public void ResetScores()
			{
				foreach (var element in m_ListElements)
					element.ResetScore();
			}

			public virtual IEnumerable<T> BuildQuery(string prefix)
			{
				if (prefix == "")
					return m_ListElements;
				else
					return m_ListElements.Where(
						element => element.m_Content.text.Contains(prefix, System.StringComparison.OrdinalIgnoreCase)
					);
			}

			public IEnumerable<T> GetFilteredList(string prefix)
			{
				IEnumerable<T> res = BuildQuery(prefix);
				if (m_MaxCount > 0)
					res = res.OrderByDescending(element => element.filterScore).Take(m_MaxCount);
				if (m_SortAlphabetically)
					return res.OrderBy(element => element.text.ToLower());
				else
					return res;
			}

			public int GetFilteredCount(string prefix)
			{
				IEnumerable<T> res = BuildQuery(prefix);
				if (m_MaxCount > 0)
					res = res.Take(m_MaxCount);
				return res.Count();
			}

			public T NewOrMatchingElement(string label)
			{
				foreach (var element in m_ListElements) {
					if (element.text.Equals(label, StringComparison.OrdinalIgnoreCase))
						return element;
				}

				var res = new T() { text = label, selected = false };
				m_ListElements.Add(res);
				return res;
			}
		}

		protected class Styles
		{
			public GUIStyle background = "grey_border";
			public GUIStyle customTextField;
			public GUIStyle customTextFieldCancelButton;
			public GUIStyle customTextFieldCancelButtonEmpty;
			public Styles()
			{
				customTextField = new GUIStyle(EditorStyles.toolbarSearchField);
				customTextFieldCancelButton = new GUIStyle("ToolbarSeachCancelButton");
				customTextFieldCancelButtonEmpty = new GUIStyle("ToolbarSeachCancelButtonEmpty");
			}
		}

		// Static
		protected static Styles s_Styles;

		// State
		protected InputData m_Data;

		// Layout
		protected const float k_LineHeight = 16;
		protected const float k_TextFieldHeight = 16;
		protected const float k_Margin = 10;
		protected Gravity m_Gravity;

		protected string m_EnteredTextCompletion = "";
		protected string m_EnteredText = "";
		protected int m_SelectedCompletionIndex = 0;

		public PopupList(InputData inputData) : this(inputData, null) { }

		public PopupList(InputData inputData, string inititalFilter)
		{
			m_Data = inputData;
			m_Data.ResetScores();
			SelectNoCompletion();
			m_Gravity = Gravity.Top;
			if (inititalFilter != null) {
				m_EnteredTextCompletion = inititalFilter;
				UpdateCompletion();
			}
		}

		public override void OnClose()
		{
			if (m_Data != null)
				m_Data.ResetScores();
		}

		public virtual float GetWindowHeight()
		{
			int count = (m_Data.m_MaxCount == 0) ? m_Data.GetFilteredCount(m_EnteredText) : m_Data.m_MaxCount;
			return count * k_LineHeight + 2 * k_Margin + (m_Data.m_AllowCustom ? k_TextFieldHeight : 0);
		}

		public virtual float GetWindowWidth()
		{
			return m_Data.GetFilteredList("").Max(E=>E.width);
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(GetWindowWidth(), GetWindowHeight());
		}

		public override void OnGUI(Rect windowRect)
		{
			Event evt = Event.current;
			// We do not use the layout event
			if (evt.type == EventType.Layout)
				return;

			if (s_Styles == null)
				s_Styles = new Styles();

			if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape) {
				editorWindow.Close();
				GUIUtility.ExitGUI();
			}

			if (m_Gravity == Gravity.Bottom) {
				DrawList(editorWindow, windowRect);
				DrawCustomTextField(editorWindow, windowRect);
			} else {
				DrawCustomTextField(editorWindow, windowRect);
				DrawList(editorWindow, windowRect);
			}

			// Background with 1 pixel border (rendered above content)
			if (evt.type == EventType.Repaint)
				s_Styles.background.Draw(new Rect(windowRect.x, windowRect.y, windowRect.width, windowRect.height), false, false, false, false);
		}

		protected virtual void DrawCustomTextField(EditorWindow editorWindow, Rect windowRect)
		{
			if (!m_Data.m_AllowCustom)
				return;

			Event evt = Event.current;
			bool enableAutoCompletion = m_Data.m_EnableAutoCompletion;
			bool closeWindow = false;
			bool clearText = false;

			string textBeforeEdit = CurrentDisplayedText();

			// Handle "special" keyboard input
			if (evt.type == EventType.KeyDown) {
				switch (evt.keyCode) {
					case KeyCode.Comma:
					case KeyCode.Space:
					case KeyCode.Tab:
					case KeyCode.Return:
						if (textBeforeEdit != "") {
							// Toggle state
							if (m_Data.m_OnSelectCallback != null)
								m_Data.m_OnSelectCallback(m_Data.NewOrMatchingElement(textBeforeEdit));

							if (evt.keyCode == KeyCode.Tab || evt.keyCode == KeyCode.Comma)
								clearText = true;  // to ease multiple entries (it is unlikely that the same filter is used more than once)

							// Auto close
							if (m_Data.m_CloseOnSelection || evt.keyCode == KeyCode.Return)
								closeWindow = true;
						}
						evt.Use();
						break;
					case KeyCode.Delete:
					case KeyCode.Backspace:
						enableAutoCompletion = false;
						// Don't use the event yet, so the textfield below can get it and delete the selection
						break;

					case KeyCode.DownArrow:
						ChangeSelectedCompletion(1);
						evt.Use();
						break;

					case KeyCode.UpArrow:
						ChangeSelectedCompletion(-1);
						evt.Use();
						break;
					case KeyCode.None:
						if (evt.character == ' ' || evt.character == ',') {
							evt.Use();
						}
						break;
				}
			}

			// Draw textfield
			{
				Rect pos = new Rect(windowRect.x + k_Margin / 2, windowRect.y + (m_Gravity == Gravity.Top ? (k_Margin / 2) : (windowRect.height - k_TextFieldHeight - k_Margin / 2)), windowRect.width - k_Margin - 14, k_TextFieldHeight);

				TextFieldText = EditorGUI.TextField(pos, textBeforeEdit, s_Styles.customTextField);

				Rect buttonRect = pos;
				buttonRect.x += pos.width;
				buttonRect.width = 14;
				// Draw "clear textfield" button (X)
				if ((GUI.Button(buttonRect, GUIContent.none, TextFieldText != "" ? s_Styles.customTextFieldCancelButton : s_Styles.customTextFieldCancelButtonEmpty) && TextFieldText != "")
					|| clearText) {
					TextFieldText = string.Empty;
					GUIUtility.keyboardControl = 0;
					enableAutoCompletion = false;
				}
			}

			// Handle autocompletion
			if (textBeforeEdit != TextFieldText) {
				OnFilterTextChange(m_EnteredText, TextFieldText);
				m_EnteredText = TextFieldText;

				if (enableAutoCompletion)
					UpdateCompletion();
				else
					SelectNoCompletion();
			}

			if (closeWindow)
				editorWindow.Close();
		}

		protected virtual void OnFilterTextChange(string oldText, string newText)	{}

		protected string CurrentDisplayedText()
		{
			return m_EnteredTextCompletion != "" ? m_EnteredTextCompletion : m_EnteredText;
		}

		protected void UpdateCompletion()
		{
			if (!m_Data.m_EnableAutoCompletion)
				return;
			IEnumerable<string> query = m_Data.GetFilteredList(m_EnteredText).Select(element => element.text);

			if (m_EnteredTextCompletion != "" && m_EnteredTextCompletion.StartsWith(m_EnteredText, System.StringComparison.OrdinalIgnoreCase)) {
				m_SelectedCompletionIndex = query.TakeWhile(element => element != m_EnteredTextCompletion).Count();
				// m_EnteredTextCompletion is already correct
			} else {
				// Clamp m_SelectedCompletionIndex to 0..query.Count () - 1
				if (m_SelectedCompletionIndex < 0)
					m_SelectedCompletionIndex = 0;
				else if (m_SelectedCompletionIndex >= query.Count())
					m_SelectedCompletionIndex = query.Count() - 1;

				m_EnteredTextCompletion = query.Skip(m_SelectedCompletionIndex).DefaultIfEmpty("").FirstOrDefault();
			}
			AdjustRecycledEditorSelectionToCompletion();
		}

		protected void ChangeSelectedCompletion(int change)
		{
			int count = m_Data.GetFilteredCount(m_EnteredText);
			if (m_SelectedCompletionIndex == -1 && change < 0)  // specal case for initial selection
				m_SelectedCompletionIndex = count;

			int index = count > 0 ? (m_SelectedCompletionIndex + change + count) % count : 0;
			SelectCompletionWithIndex(index);
		}

		protected void SelectCompletionWithIndex(int index)
		{
			m_SelectedCompletionIndex = index;
			m_EnteredTextCompletion = "";
			UpdateCompletion();
		}

		protected void SelectNoCompletion()
		{
			m_SelectedCompletionIndex = -1;
			m_EnteredTextCompletion = "";
			AdjustRecycledEditorSelectionToCompletion();
		}

		protected void AdjustRecycledEditorSelectionToCompletion()
		{
			if (m_EnteredTextCompletion != "") {
				//PopupTextEditor.text = m_EnteredTextCompletion;
				//PopupTextEditor.cursorIndex = m_EnteredText.Length;
				//PopupTextEditor.selectIndex = m_EnteredTextCompletion.Length; //the selection goes from s_RecycledEditor.cursorIndex (already set by DoTextField) to s_RecycledEditor.selectIndex
			}
		}

		protected virtual void DrawList(EditorWindow editorWindow, Rect windowRect)
		{
			Event evt = Event.current;

			int i = -1;
			foreach (var element in m_Data.GetFilteredList(m_EnteredText)) {
				i++;
				Rect rect = new Rect(windowRect.x, windowRect.y + k_Margin + i * k_LineHeight + (m_Gravity == Gravity.Top && m_Data.m_AllowCustom ? k_TextFieldHeight : 0), windowRect.width, k_LineHeight);

				switch (evt.type) {
					case EventType.Repaint: {
						GUIStyle style = element.style;
						bool selected = element.selected || element.partiallySelected;
						bool focused = false;
						bool isHover = i == m_SelectedCompletionIndex;
						bool isActive = selected;

						using (new EditorGUI.DisabledScope(!element.enabled)) {
							GUIContent content = element.m_Content;
							style.Draw(rect, content, isHover, isActive, selected, focused);
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
							SelectCompletionWithIndex(i);
							evt.Use();
						}
					}
					break;
				}
			}
		}
	}

	public class GenericPopupList : PopupList<PopupListElement>	
	{
		public GenericPopupList(InputData inputData) : base(inputData) { }
		public GenericPopupList(InputData inputData, string inititalFilter) : base(inputData, inititalFilter) { }
	}
}
