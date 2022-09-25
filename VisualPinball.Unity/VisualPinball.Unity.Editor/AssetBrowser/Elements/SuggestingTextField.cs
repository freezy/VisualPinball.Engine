// MIT License
//
// Copyright (c) 2019 James
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// https://github.com/PassivePicasso/VisualTemplates/tree/master/Editor

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A text field that supports as-you-type suggestions.
	///
	/// Set <see cref="SuggestOptions"/> or available autocomplete strings. If <see cref="IsMultiValue"/>
	/// is set, the input field expects each value separated by a comma, and will match and complete only
	/// the current value.
	/// </summary>
	public class SuggestingTextField : VisualElement
	{
		#region UXML Definitions and Pass-Throughs

		public new class UxmlFactory : UxmlFactory<SuggestingTextField, UxmlTraits> { }
		public new class UxmlTraits : VisualElement.UxmlTraits
		{
			private readonly UxmlStringAttributeDescription _label = new() { name = "label" };
			private readonly UxmlStringAttributeDescription _value = new() { name = "value" };
			private readonly UxmlStringAttributeDescription _bindingPath = new() { name = "binding-path" };
			private readonly UxmlStringAttributeDescription _tooltip = new() { name = "tootip" };
			private readonly UxmlBoolAttributeDescription _isMultiValue = new() { name = "multivalue", defaultValue = false };

			public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
			{
				get { yield break; }
			}

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
			{
				base.Init(ve, bag, cc);
				var ate = ve as SuggestingTextField;

				ate!.Label = _label.GetValueFromBag(bag, cc);
				ate!.Value = _value.GetValueFromBag(bag, cc);
				ate!.IsMultiValue = _isMultiValue.GetValueFromBag(bag, cc);
				ate!.BindingPath = _bindingPath.GetValueFromBag(bag, cc);
				ate!.Tooltip = _tooltip.GetValueFromBag(bag, cc);
			}
		}

		public string Label { get => _textField.label; set => _textField.label = value; }
		public string BindingPath { get => _textField.bindingPath; set => _textField.bindingPath = value; }
		public string Value { get => _textField.value; set => _textField.value = value; }
		public bool IsMultiValue;
		public string Tooltip { get => _textField.tooltip; set => _textField.tooltip = value; }

		#endregion

		public new void Focus() => _textField.Focus();
		public void SelectAll() => _textField.SelectAll();
		public void RegisterKeyDownCallback(EventCallback<KeyDownEvent> evt) => _textField.RegisterCallback(evt);

		public List<string> MatchedSuggestOption { get; set; }
		private readonly Func<string, bool> _matchingSuggestOptions;
		public string[] SuggestOptions { get; set; } = Array.Empty<string>();

		private EditorWindow _popupWindow;
		private ListView _optionList;
		private readonly TextField _textField;

		private PropertyInfo _ownerObjectProperty;
		private PropertyInfo _screenPositionProperty;
		private MethodInfo _showPopupNonFocus;
		private object[] _showValueArray;
		private object _ownerObject;
		private object _showValue;

		private bool _hasFocus;
		public bool PopupVisible;
		private Rect _popupPosition;

		public SuggestingTextField()
		{
			AddToClassList("search-suggest");

			_textField = new TextField { name = "search-suggest-input" };
			_textField.AddToClassList("unity-base-field");
			_textField.AddToClassList("unity-base-text-field");
			_textField.AddToClassList("unity-text-field");
			_textField.AddToClassList("unity-base-field__aligned");
			_textField.AddToClassList("unity-base-field__inspector-field");

			MatchedSuggestOption = new List<string>();

			ConfigureOptionList();

			_matchingSuggestOptions = suggestOption => suggestOption.ToLower().Contains(CurrentValue.ToLower());

			RegisterCallback<AttachToPanelEvent>(OnAttached);
			RegisterCallback<DetachFromPanelEvent>(OnDetached);

			Add(_textField);
		}

		private string CurrentValue {
			get {
				if (!IsMultiValue) {
					return _textField.value;
				}
				var startIndex = 0;
				var endIndex = _textField.value.Length;
				for (var i = 0; i < _textField.value.Length; i++) {
					if (_textField.value[i] == ',' && i < _textField.cursorIndex) {
						startIndex = i + 1;
					}
					if (_textField.value[i] == ',' && i >= _textField.cursorIndex) {
						endIndex = i;
						break;
					}
				}
				return _textField.value.Substring(startIndex, endIndex - startIndex).Trim();
			}
			set {
				if (!IsMultiValue) {
					_textField.value = value;
					return;
				}
				var before = string.Empty;
				var after = string.Empty;
				for (var i = 0; i < _textField.value.Length; i++) {
					if (_textField.value[i] == ',' && i < _textField.cursorIndex) {
						before = _textField.value[..(i + 1)];
					}
					if (_textField.value[i] == ',' && i >= _textField.cursorIndex) {
						after = _textField.value[i..];
						break;
					}
				}
				_textField.value = before + value + after;
			}
		}

		private void CreateNewWindow()
		{
			if (_popupWindow == null) {
				_popupWindow = ScriptableObject.CreateInstance<EditorWindow>();
				_popupWindow.rootVisualElement.hierarchy.Add(_optionList);
			}
		}

		private void ConfigureOptionList()
		{
			if (_optionList == null) {
				_optionList = new ListView {
					name = "search-suggest-list",
					fixedItemHeight = 20,
					makeItem = () => {
						var label = new Label();
						label.AddToClassList("suggestion");
						label.RegisterCallback<MouseDownEvent>(OnLabelMouseDown);

						return label;
					}
				};

				_optionList.bindItem = (v, i) => {
					var label = v as Label;
					var suggestOption = (string)_optionList.itemsSource[i];
					label!.text = suggestOption;
				};
				_optionList.selectionType = SelectionType.Single;
				StyleOptionList(_optionList);
			}
		}

		private static void StyleOptionList(VisualElement element)
		{
			element.style.left = 0;
			element.style.right = 0;
			element.style.height = 100;

			element.style.borderTopWidth =
				element.style.borderLeftWidth =
					element.style.borderRightWidth =
						element.style.borderBottomWidth = 1;

			element.style.borderTopColor =
				element.style.borderLeftColor =
					element.style.borderRightColor =
						element.style.borderBottomColor
							= Color.Lerp(Color.gray, Color.black, 0.3f);
		}

		private void OnDetached(DetachFromPanelEvent evt)
		{
			_textField.UnregisterValueChangedCallback(OnTextChanged);
			_textField.UnregisterCallback<FocusOutEvent>(OnLostFocus);
			_textField.UnregisterCallback<FocusInEvent>(OnGainedFocus);
			_textField.UnregisterCallback<KeyDownEvent>(OnKeyDown);
			Cleanup();
		}

		private void OnAttached(AttachToPanelEvent evt)
		{
			_ownerObjectProperty = evt.destinationPanel.GetType().GetProperty("ownerObject");
			_ownerObject = _ownerObjectProperty!.GetValue(evt.destinationPanel);

			_screenPositionProperty = _ownerObject.GetType().GetProperty("screenPosition");

			var showMode = typeof(EditorWindow).Assembly.GetType("UnityEditor.ShowMode");

			_showPopupNonFocus = typeof(EditorWindow).GetMethod("ShowPopupWithMode", BindingFlags.Instance | BindingFlags.NonPublic);

			_showValue = Enum.GetValues(showMode).GetValue((int)ShowMode.Tooltip);
			_showValueArray = new[] { _showValue, false };

			_textField.RegisterValueChangedCallback(OnTextChanged);
			_textField.RegisterCallback<FocusOutEvent>(OnLostFocus);
			_textField.RegisterCallback<FocusInEvent>(OnGainedFocus);
			_textField.RegisterCallback<GeometryChangedEvent>(OnGeometryChange);
			_textField.RegisterCallback<KeyDownEvent>(OnKeyDown);
		}

		private void OnKeyDown(KeyDownEvent evt)
		{
			switch (evt.keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					if (_optionList.selectedIndex != -1) {
						var suggestOption = MatchedSuggestOption[_optionList.selectedIndex];
						CurrentValue = suggestOption;
						_hasFocus = false;
						UpdateVisibility();
						_textField.Focus();
					}
					return;

				case KeyCode.UpArrow:
					evt.PreventDefault();
					if (_optionList.selectedIndex > 0)
						_optionList.selectedIndex--;
					break;
				case KeyCode.DownArrow:
					evt.PreventDefault();
					if (_optionList.selectedIndex < MatchedSuggestOption.Count - 1)
						_optionList.selectedIndex++;
					break;
				default:
					_optionList.selectedIndex = -1;
					break;
			}

			_optionList.ScrollToItem(_optionList.selectedIndex);
		}

		private void OnGeometryChange(GeometryChangedEvent evt)
		{
			UpdatePosition();
		}

		private void OnGainedFocus(FocusInEvent evt)
		{
			_hasFocus = true;
			UpdateOptionList();
			UpdateVisibility();
			UpdatePosition();
		}

		private void OnLostFocus(FocusOutEvent evt)
		{
			if (evt.relatedTarget == null) return;
			_hasFocus = false;
			UpdateVisibility();
		}

		private void OnTextChanged(ChangeEvent<string> evt)
		{
			UpdateOptionList();
			UpdateVisibility();
			UpdatePosition();
		}

		private void UpdatePosition()
		{
			if (_popupWindow == null) return;
			var worldSpaceTextLayout = _textField.LocalToWorld(_textField.layout);

			var windowPosition = (Rect)_screenPositionProperty.GetValue(_ownerObject);

			var topLeft = windowPosition.position + worldSpaceTextLayout.position;
			topLeft = new Vector2(topLeft.x - 3, topLeft.y + worldSpaceTextLayout.height);
			_popupPosition = new Rect(topLeft, new Vector2(worldSpaceTextLayout.width, 100));


			_popupWindow.rootVisualElement.style.height = 100;
			_popupWindow.rootVisualElement.style.width = worldSpaceTextLayout.width;

			_popupWindow.position = _popupPosition;
		}

		private void UpdateOptionList()
		{
			MatchedSuggestOption.Clear();
			_optionList.itemsSource = MatchedSuggestOption;
			_optionList.selectedIndex = -1;

			if (string.IsNullOrEmpty(CurrentValue)) {
				_optionList.Rebuild();
				return;
			}

			MatchedSuggestOption.AddRange(SuggestOptions.Where(_matchingSuggestOptions));

			_optionList.Rebuild();
		}

		private void UpdateVisibility()
		{
			if (_hasFocus && _optionList.itemsSource.Count > 0 && !(_optionList.itemsSource.Count == 1 && (string)_optionList.itemsSource[0] == CurrentValue)) {
				if (PopupVisible) {
					return;
				}
				CreateNewWindow();
				_showPopupNonFocus.Invoke(_popupWindow, _showValueArray);
				PopupVisible = true;
			} else {
				Cleanup();
			}
		}

		private void Cleanup()
		{
			_optionList.RemoveFromHierarchy();
			if (_popupWindow != null) {
				_popupWindow.Close();
				Object.DestroyImmediate(_popupWindow);
			}
			PopupVisible = false;
		}

		private void OnLabelMouseDown(MouseDownEvent evt)
		{
			var pickedLabel = evt.target as Label;
			var suggestOption = pickedLabel!.text;
			CurrentValue = suggestOption;
			_hasFocus = false;
			UpdateVisibility();
			_textField.Focus();
		}

		private enum ShowMode
		{
			// Show as a normal window with max, min & close buttons.
			NormalWindow = 0,
			// Used for a popup menu. On mac this means light shadow and no titlebar.
			PopupMenu = 1,
			// Utility window - floats above the app. Disappears when app loses focus.
			Utility = 2,
			// Window has no shadow or decorations. Used internally for dragging stuff around.
			NoShadow = 3,
			// The Unity main window. On mac, this is the same as NormalWindow, except window doesn't have a close button.
			MainWindow = 4,
			// Aux windows. The ones that close the moment you move the mouse out of them.
			AuxWindow = 5,
			// Like PopupMenu, but without keyboard focus
			Tooltip = 6,
			// Modal Utility window
			ModalUtility = 7
		}
	}
}
