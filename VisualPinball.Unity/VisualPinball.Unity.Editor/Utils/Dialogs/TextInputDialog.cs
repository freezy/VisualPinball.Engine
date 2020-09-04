// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor.Utils.Dialogs
{

	/// <summary>
	/// 
	/// </summary>
	class TextInputDialog : EditorWindow
	{
		/// <summary>
		/// A delegate function to pass to the dialog for text validation when validationButton is pressed
		/// </summary>
		/// <param name="text">The text the dialog will ask for validation</param>
		/// <returns>True if the text is validated</returns>
		public delegate bool ValidateText(string text);

		/// <summary>
		/// The text edited by this dialog
		/// </summary>
		/// <remarks>
		/// Could be initialized at a default value 
		/// </remarks>
		public string Text = string.Empty;
		/// <summary>
		/// It will say if the entered text has been validated after pressing the validationButton
		/// </summary>
		/// <remarks>
		/// if you press the cancel button it will be false
		/// </remarks>
		public bool TextValidated { get; private set; } = false;

		/// <summary>
		/// A message shown above the text input field
		/// </summary>
		public string Message = string.Empty;
		/// <summary>
		/// A label displayed next to the text input field
		/// </summary>
		public string InputLabel = string.Empty;
		/// <summary>
		/// The GUIContent to display the validation button
		/// </summary>
		public GUIContent ValidationButton = new GUIContent("button") { text = "OK" };
		/// <summary>
		/// TheGUIContent to display the cancel button
		/// </summary>
		public GUIContent CancelButton = new GUIContent("button") { text = "Cancel" };
		/// <summary>
		/// A delegate function which will be called when pressin the validation button to validate the text
		/// </summary>
		/// <remarks>
		/// If no delegate is set, validation is skipped
		/// </remarks>
		public ValidateText ValidationDelegate;

		/// <summary>
		/// TextInputDialog Creation helper
		/// </summary>
		/// <returns>the dialog created and configured</returns>
		public static TextInputDialog Create(GUIContent titleContent,
											Rect position,
											string text = "", 
											string message = "", 
											string inputLabel = "", 
											GUIContent validButton = null,
											GUIContent cancelButton = null,
											ValidateText validationDelegate = null)
		{
			var dialog = CreateInstance<TextInputDialog>();
			dialog.titleContent = titleContent;
			dialog.position = position;
			if (position.width > 0 && position.height > 0) {
				dialog.minSize = new Vector2(position.width, position.height);
				dialog.maxSize = dialog.minSize;
			}
			dialog.Text = text;
			dialog.Message = message;
			dialog.InputLabel = inputLabel;
			if (validButton != null) {
				dialog.ValidationButton = validButton;
			}
			if (cancelButton != null) {
				dialog.CancelButton = cancelButton;
			}
			dialog.ValidationDelegate = validationDelegate;
			return dialog;
		}

		private void OnGUI()
		{
			GUILayout.BeginVertical();

			if (!string.IsNullOrEmpty(Message)) {
				GUILayout.Space(10);
				GUILayout.Label(Message);
				GUILayout.Space(10);
			}

			GUILayout.BeginHorizontal();
			if (!string.IsNullOrEmpty(InputLabel)) {
				GUILayout.Label(InputLabel, GUILayout.ExpandWidth(false));
				GUILayout.Space(10);
			}

			GUI.SetNextControlName("TextInput");
			Text = GUILayout.TextField(Text);
			GUI.FocusControl("TextInput");

			GUILayout.EndHorizontal();

			GUILayout.Space(10);

			GUILayout.BeginHorizontal();
			
			if (GUILayout.Button(ValidationButton) || Event.current.keyCode == KeyCode.Return) {
				TextValidated = true;
				if (ValidationDelegate != null) {
					TextValidated = ValidationDelegate.Invoke(Text);
				}
				if (TextValidated) {
					Close();
				}
			}
			if (GUILayout.Button(CancelButton)) {
				TextValidated = false;
				Close();
			}
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();
		}

	}
}
