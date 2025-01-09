// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SoundAsset)), CanEditMultipleObjects]
	public class SoundAssetInspector : UnityEditor.Editor
	{
		private CancellationTokenSource _allowFadeCts;
		private CancellationTokenSource _instantCts;

		[SerializeField]
		private VisualTreeAsset _inspectorXml;

		private Button _playButton;

		public override VisualElement CreateInspectorGUI()
		{
			var ui = _inspectorXml.Instantiate();
			_playButton = ui.Q<Button>("play-button");
			_playButton.clicked += OnPlayButtonClicked;
			var loopField = ui.Q<PropertyField>("loop");
			var fadeInTimeField = ui.Q<PropertyField>("fade-in-time");
			var fadeOutTimeField = ui.Q<PropertyField>("fade-out-time");
			loopField.RegisterValueChangeCallback(e => {
				var loop = e.changedProperty.boolValue;
				var displayStyle = loop ? DisplayStyle.Flex : DisplayStyle.None;
				fadeInTimeField.style.display = displayStyle;
				fadeOutTimeField.style.display = displayStyle;
			});
			return ui;
		}

		private void OnEnable()
		{
			_allowFadeCts = new();
			_instantCts = new();
		}

		private void OnDisable()
		{
			_instantCts.Cancel();
			_instantCts.Dispose();
			_instantCts = null;
			_allowFadeCts.Dispose();
			_allowFadeCts = null;
		}

		private async void OnPlayButtonClicked()
		{
			_playButton.clicked -= OnPlayButtonClicked;
			_playButton.clicked += OnStopButtonClicked;
			_playButton.text = "Stop";
			try {
				var soundAsset = target as SoundAsset;
				await SoundUtils.PlayInEditorPreviewScene(soundAsset, _allowFadeCts.Token, _instantCts.Token);
			} catch (OperationCanceledException) { } finally {
				_playButton.clicked -= OnStopButtonClicked;
				_playButton.clicked -= OnStopForrealButtonClicked;
				_playButton.clicked += OnPlayButtonClicked;
				_playButton.text = "Play";
			}
		}

		private void OnStopButtonClicked()
		{
			_playButton.clicked -= OnStopButtonClicked;
			_playButton.clicked += OnStopForrealButtonClicked;
			_allowFadeCts.Cancel();
			_allowFadeCts.Dispose();
			_allowFadeCts = new();
		}

		private void OnStopForrealButtonClicked()
		{
			_playButton.clicked -= OnStopForrealButtonClicked;
			_instantCts.Cancel();
			_instantCts.Dispose();
			_instantCts = new();
		}
	}
}

