// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Stores a user's explicit permission to execute or render active content extracted from a
	/// package. Acknowledgements are keyed by the full content hash, so changed content is always
	/// presented to the user again.
	/// </summary>
	public static class PackagedContentConsent
	{
		private const string KeyPrefix = "vpe.content.consent.v1.";

		/// <summary>Test/application hook for supplying a branded consent UI.</summary>
		public static Func<PackagedContentRef, string, CancellationToken, Task<bool>> PromptOverride;

		public static bool IsAcknowledged(PackagedContentRef contentRef)
		{
			Validate(contentRef);
			return PlayerPrefs.GetInt(GetKey(contentRef), 0) == 1;
		}

		public static async Task RequireAsync(PackagedContentRef contentRef, string description, CancellationToken ct)
		{
			Validate(contentRef);
			if (IsAcknowledged(contentRef)) {
				return;
			}

			ct.ThrowIfCancellationRequested();
			var prompt = PromptOverride;
			var accepted = prompt != null
				? await prompt(contentRef, description, ct)
				: await PromptDefaultAsync(contentRef, description, ct);
			ct.ThrowIfCancellationRequested();
			if (!accepted) {
				throw new UnauthorizedAccessException(
					$"Permission to use packaged {description} was denied. Content hash: {contentRef.ContentHash}."
				);
			}

			PlayerPrefs.SetInt(GetKey(contentRef), 1);
			PlayerPrefs.Save();
		}

		public static void Forget(PackagedContentRef contentRef)
		{
			Validate(contentRef);
			PlayerPrefs.DeleteKey(GetKey(contentRef));
		}

		private static string GetKey(PackagedContentRef contentRef) => KeyPrefix + contentRef.ContentHash;

		private static void Validate(PackagedContentRef contentRef)
		{
			if (string.IsNullOrWhiteSpace(contentRef.ContentHash)) {
				throw new ArgumentException("Packaged content consent requires a content hash.", nameof(contentRef));
			}
		}

		private static Task<bool> PromptDefaultAsync(PackagedContentRef contentRef, string description, CancellationToken ct)
		{
#if UNITY_EDITOR
			ct.ThrowIfCancellationRequested();
			var accepted = UnityEditor.EditorUtility.DisplayDialog(
				"Allow packaged table content?",
				$"This table contains {description} that will be used outside the package cache.\n\n" +
				$"Content hash: {contentRef.ContentHash}\n\nOnly allow tables from sources you trust.",
				"Allow",
				"Deny"
			);
			return Task.FromResult(accepted);
#else
			return PackagedContentConsentPrompt.ShowAsync(contentRef, description, ct);
#endif
		}
	}

#if !UNITY_EDITOR
	internal sealed class PackagedContentConsentPrompt : MonoBehaviour
	{
		private TaskCompletionSource<bool> _completion;
		private CancellationTokenRegistration _registration;
		private string _message;
		private Rect _window = new Rect(0, 0, 560, 260);

		internal static Task<bool> ShowAsync(PackagedContentRef contentRef, string description, CancellationToken ct)
		{
			var gameObject = new GameObject("Packaged Content Consent") { hideFlags = HideFlags.HideAndDontSave };
			DontDestroyOnLoad(gameObject);
			var prompt = gameObject.AddComponent<PackagedContentConsentPrompt>();
			prompt._completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			prompt._message = $"This table contains {description}. Only allow tables from sources you trust.\n\n" +
				$"Content hash: {contentRef.ContentHash}";
			prompt._registration = ct.Register(() => prompt.CompleteCanceled(ct));
			return prompt._completion.Task;
		}

		private void OnGUI()
		{
			_window.x = (Screen.width - _window.width) / 2f;
			_window.y = (Screen.height - _window.height) / 2f;
			_window = GUI.ModalWindow(GetInstanceID(), _window, DrawWindow, "Allow packaged table content?");
		}

		private void DrawWindow(int id)
		{
			GUILayout.Space(12);
			GUILayout.Label(_message, new GUIStyle(GUI.skin.label) { wordWrap = true });
			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Deny", GUILayout.Height(38))) {
				Complete(false);
			}
			if (GUILayout.Button("Allow", GUILayout.Height(38))) {
				Complete(true);
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(8);
		}

		private void Complete(bool accepted)
		{
			_registration.Dispose();
			_completion.TrySetResult(accepted);
			Destroy(gameObject);
		}

		private void CompleteCanceled(CancellationToken ct)
		{
			_completion.TrySetCanceled(ct);
			Destroy(gameObject);
		}
	}
#endif
}
