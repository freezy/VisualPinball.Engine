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
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SoundEffectAsset)), CanEditMultipleObjects]
	public class SoundEffectAssetInspector : SoundAssetInspector
	{
		[SerializeField]
		private VisualTreeAsset _soundEffectAssetInspectorAsset;

		private CancellationTokenSource _allowFadeCts;
		private CancellationTokenSource _instantCts;
		private Button _playButton;

		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();
			var baseUi = base.CreateInspectorGUI();
			root.Add(baseUi);
			var subUi = _soundEffectAssetInspectorAsset.Instantiate();
			root.Add(subUi);
			return root;
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
			try
			{
				var soundAsset = target as SoundEffectAsset;
				await soundAsset.PlayInEditorPreviewScene(_allowFadeCts.Token, _instantCts.Token);
			}
			catch (OperationCanceledException) { }
			finally
			{
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
