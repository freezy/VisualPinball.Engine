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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Editor.Utils;

namespace VisualPinball.Unity.Editor
{
	internal class SoundManager : ManagerWindow<SoundListData>
	{
		protected override string DataTypeName => "Sound";
		protected override float DetailsMaxWidth => 500.0f;

		/// <summary>
		/// Sound positions display
		/// </summary>
		private bool _displaySoundPosition = true;
		private bool _displayAllSounds;

		/// <summary>
		/// Auto framing, going to Top view and frame on whole table when focused to ease sound position visualization
		/// </summary>
		private bool _autoFrame = true;
		private bool _needFraming;

		/// <summary>
		/// Table & selected sound position & size used for display
		/// </summary>
		private Vector3 _tableCenter = Vector3.zero;
		private Vector3 _tableSize = Vector3.zero;
		private Vector3 _selectedSoundPos = Vector3.zero;

		private readonly Color _selectedColor = Color.yellow;
		private readonly Color _unselectedColor = new Color(0.25f, 0.25f, 0.25f, 0.75f);
		private GUIContent _iconContent;
		private GUIStyle _iconStyle;

		/// <summary>
		/// Audio Data Playback & Visualization
		/// </summary>
		private float[] _audioSamples;
		private GameObject _audioSource;
		private AudioSource _audioSourceComp;

		private static readonly string[] SoundOutTypeStrings = {
			"Table",
			"Backglass",
		};
		private static readonly byte[] SoundOutTypeValues = {
			SoundOutTypes.Table,
			SoundOutTypes.Backglass,
		};

		[MenuItem("Visual Pinball/Sound Manager", false, 404)]
		public static void ShowWindow()
		{
			GetWindow<SoundManager>();
		}

		protected override void OnButtonBarGUI()
		{
			EditorGUI.BeginChangeCheck();
			_autoFrame = GUILayout.Toggle(_autoFrame, "Auto Framing", GUILayout.ExpandWidth(false));
			if (EditorGUI.EndChangeCheck() && _autoFrame) {
				_needFraming = true;
			}

			EditorGUI.BeginChangeCheck();
			_displaySoundPosition = GUILayout.Toggle(_displaySoundPosition, "Display Sound Position", GUILayout.ExpandWidth(false));
			if (_displaySoundPosition) {
				_displayAllSounds = GUILayout.Toggle(_displayAllSounds, "Display All Sounds", GUILayout.ExpandWidth(false));
			}
			if (EditorGUI.EndChangeCheck()) {
				SceneView.RepaintAll();
			}

		}

		private void InitAudioSource()
		{
			if (_audioSource == null) {
				_audioSource = new GameObject("SoundManager AudioSource") {
					hideFlags = HideFlags.HideAndDontSave
				};
				_audioSource.AddComponent<AudioSource>();
				_audioSourceComp = _audioSource.GetComponent<AudioSource>();
			}
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Sound Manager", EditorGUIUtility.IconContent("SceneViewAudio").image);
			_iconContent = new GUIContent() {
				image = EditorGUIUtility.IconContent("AudioSource Gizmo").image
			};
			_iconStyle = new GUIStyle() {
				alignment = TextAnchor.MiddleCenter,
				imagePosition = ImagePosition.ImageOnly,
				fixedHeight = 25.0f,
				fixedWidth = 25.0f
			};

			base.OnEnable();
			SceneView.duringSceneGui += OnSceneGUI;
		}

		public override void OnDisable()
		{
			DestroyImmediate(_audioSource);
			_audioSource = null;
			_audioSourceComp = null;
			SceneView.duringSceneGui -= OnSceneGUI;
			base.OnDisable();
		}

		protected override void OnDataDetailGUI()
		{
			InitAudioSource();

			DropDownField("Output Target", ref _selectedItem.LegacySound.OutputTarget, SoundOutTypeStrings, SoundOutTypeValues);
			SliderField("Volume", ref _selectedItem.LegacySound.Volume, -100, 100);
			SliderField("Balance", ref _selectedItem.LegacySound.Balance, -100, 100);
			SliderField("Fade (Rear->Front)", ref _selectedItem.LegacySound.Fade, -100, 100);

			EditorGUILayout.Space();
			var wfx = _selectedItem.LegacySound.Wfx;
			if (_selectedItem.LegacySound.AudioClip != null) {
				GUILayout.Label($"Length : {_selectedItem.LegacySound.AudioClip.length} s,  Channels : {wfx.Channels}, BPS : {wfx.BitsPerSample}, Freq : {wfx.SamplesPerSec}");

				if (GUILayout.Button(new GUIContent() { image = EditorGUIUtility.IconContent("PlayButton").image })) {
					_audioSource.transform.position = _selectedSoundPos;
					_audioSourceComp.volume = (_selectedItem.LegacySound.Volume + 100) / 200.0f;
					_audioSourceComp.panStereo = _selectedItem.LegacySound.Balance / 100.0f;
					_audioSourceComp.clip = _selectedItem.LegacySound.AudioClip;
					_audioSourceComp.Play();
				}

				EditorGUILayout.Space();
				Rect curveRect = GUILayoutUtility.GetLastRect();
				curveRect.x += 10.0f;
				curveRect.y += curveRect.height;
				curveRect.width -= 20.0f;
				curveRect.height = 100.0f;
				Rect r = AudioCurveRendering.BeginCurveFrame(curveRect);
				AudioCurveRendering.DrawCurve(r, x => _audioSamples[(int)((_audioSamples.Length - 1) * x + 0.5f)], Color.green);
				AudioCurveRendering.EndCurveFrame();

			} else {
				_selectedItem.LegacySound.AudioClip = (AudioClip)EditorGUILayout.ObjectField(_selectedItem.LegacySound.AudioClip, typeof(AudioClip), false);
			}

		}

		private void OnFocus()
		{
			if (_tableAuthoring == null || _tableAuthoring.gameObject == null) return;

			Selection.activeObject = _tableAuthoring.gameObject;

			if (_autoFrame) {
				_needFraming = true;
			}

			SceneView.RepaintAll();
		}

		private void OnLostFocus()
		{
			SceneView.RepaintAll();
		}


		protected override void OnDataSelected()
		{
			base.OnDataSelected();
			SceneView.RepaintAll();

			if (_selectedItem != null) {
				if (_audioSource != null && _audioSourceComp.isPlaying) {
					_audioSourceComp.Stop();
				}
				if (_selectedItem.LegacySound.AudioClip) {
					_audioSamples = new float[_selectedItem.LegacySound.AudioClip.samples * _selectedItem.LegacySound.AudioClip.channels];
					_selectedItem.LegacySound.AudioClip.GetData(_audioSamples, 0);
				}
			}
		}

		private bool ShouldDisplaySoundPosition => _tableAuthoring != null && Event.current.type == EventType.Repaint
		     && (focusedWindow == this || focusedWindow == SceneView.lastActiveSceneView && Selection.activeObject == _tableAuthoring.gameObject)
		     && _displaySoundPosition && _selectedItem != null && _selectedItem.LegacySound.OutputTarget == SoundOutTypes.Table;


		//Draw the sound position based on Balance/Fade data
		private void RenderSound(LegacySound data, bool selected)
		{
			var sndPos = _tableCenter;
			sndPos.x += _tableSize.x * 0.5f * data.Balance.PercentageToRatio();
			sndPos.y -= _tableSize.y * 0.25f;
			sndPos.z += _tableSize.z * 0.5f * data.Fade.PercentageToRatio();
			Color col = selected ? _selectedColor : _unselectedColor;

			//Disc size based on sound volume
			var minSphereSize = 0.25f;
			var maxSphereSize = _tableSize.magnitude * 0.5f;

			//Volume goes from -100 to 100 -> ratio
			var sphereSizeRatio = (data.Volume + 100) * 0.005f;
			var sphereSize = sphereSizeRatio * (maxSphereSize - minSphereSize);
			col.a = 0.05f;
			Handles.color = col;
			Handles.DrawSolidDisc(sndPos, Vector3.up, sphereSize);

			//Sound Gizmo
			col.a = selected ? 1.0f : 0.25f;
			GUI.color = col;
			Handles.Label(sndPos, _iconContent, _iconStyle);

			if (selected) {
				_selectedSoundPos = sndPos;
			}
		}

		private void OnSceneGUI(SceneView sceneView)
		{
			if (_tableAuthoring == null || _selectedItem == null) {
				return;
			}

			var bb = _playfieldAuthoring.BoundingBox;
			var sndData = _selectedItem.LegacySound;
			_tableCenter = new Vector3((bb.Right - bb.Left) * 0.5f, (bb.Bottom - bb.Top) * 0.5f, (bb.ZHigh - bb.ZLow) * 0.5f);
			_tableCenter = _tableAuthoring.gameObject.transform.TransformPoint(_tableCenter);
			_tableSize = new Vector3(bb.Width, bb.Height, bb.Depth);
			_tableSize = _tableAuthoring.gameObject.transform.TransformVector(_tableSize);

			if (ShouldDisplaySoundPosition) {
				if (_displayAllSounds) {
					foreach (var snd in _tableAuthoring.LegacyContainer.Sounds) {
						if (snd != sndData) {
							RenderSound(snd, false);
						}
					}
				}

				RenderSound(sndData, true);
			}

			//Ask for framing after _tableCenter calculation
			if (_needFraming) {
				//Frame to Top View
				SceneViewFramer.FrameObjects(Selection.objects);
				var view = SceneView.lastActiveSceneView;
				var quat = Quaternion.identity;
				quat.SetLookRotation(Vector3.down);
				view.LookAt(_tableCenter, quat, Mathf.Max(_tableSize.x, _tableSize.z) * 1.1f);

				_needFraming = false;
			}
		}

		protected override void AddNewData(string undoName, string newName)
		{
			Undo.RecordObject(_tableAuthoring, undoName);
			_tableAuthoring.LegacyContainer.Sounds.Add(new LegacySound());
		}

		protected override void RemoveData(string undoName, SoundListData data)
		{
			Undo.RecordObject(_tableAuthoring, undoName);
			_tableAuthoring.LegacyContainer.Sounds.Remove(data.LegacySound);
		}

		protected override List<SoundListData> CollectData()
		{
			var data = new List<SoundListData>();
			foreach (var s in _tableAuthoring.LegacyContainer.Sounds) {
				data.Add(new SoundListData(s));
			}
			return data;
		}
	}
}
