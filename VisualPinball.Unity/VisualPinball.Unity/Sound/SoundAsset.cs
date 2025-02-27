// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace VisualPinball.Unity
{
	public enum SoundPriority
	{
		Lowest,
		Low,
		Medium,
		High,
		Highest,
	}

	/// <summary>
	/// Common base class for sound effect assets, callout assets, and music assets. Multiple audio
	/// clips can be assigned for variation. Instances of this class are Unity assets and can
	/// therefore be stored in the project files or in an asset library for reuse across tables.
	/// </summary>
	[PackAs("SoundAsset")]
	[CreateAssetMenu(fileName = "Sound", menuName = "Pinball/Sound", order = 102)]
	public abstract class SoundAsset : ScriptableObject
	{
		public enum SelectionMethod
		{
			RoundRobin,
			Random,
		}
		
		[SerializeField]
		private string _description;

		[JsonIgnore]
		public AudioClip[] Clips;

		[SerializeField]
		private SelectionMethod _clipSelectionMethod;

		[SerializeField]
		private AudioMixerGroup _audioMixerGroup;

		[NonSerialized]
		private int _roundRobinIndex = 0;

		public abstract bool Loop { get; }

		public virtual void ConfigureAudioSource(AudioSource audioSource)
		{
			audioSource.clip = GetClip();
			audioSource.outputAudioMixerGroup = _audioMixerGroup;
			audioSource.playOnAwake = false;
		}

		public bool IsValid()
		{
			if (Clips == null)
				return false;

			foreach (var clip in Clips) {
				if (clip != null)
					return true;
			}

			return false;
		}

		private AudioClip GetClip()
		{
			if (Clips.Length == 0) {
				throw new InvalidOperationException($"The sound asset '{name}' has no audio clips to play.");
			}

			switch (_clipSelectionMethod) {

				case SelectionMethod.RoundRobin:
					_roundRobinIndex %= Clips.Length;
					var clip = Clips[_roundRobinIndex];
					_roundRobinIndex++;
					return clip;
				case SelectionMethod.Random:
					return Clips[Random.Range(0, Clips.Length)];

				default:
					throw new NotImplementedException("Selection method not implemented.");
			}
		}

		/// <summary>
		/// <c>AudioSource.isPlaying</c> returns <c>false</c> if the window loses focus, so that
		/// needs to be checked as well to make sure an audio source that was playing actually has
		/// stopped.
		/// </summary>
		public static bool HasStopped(AudioSource audioSource)
		{
			if (audioSource == null)
				return true;
			if (audioSource.isPlaying)
				return false;
#if UNITY_EDITOR
			return EditorApplication.isFocused;
#else
			return Application.isFocused;
#endif
		}

		public static async Task WaitUntilAudioStops(AudioSource audioSource, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			while (!HasStopped(audioSource))
			{
				await Task.Yield();
				ct.ThrowIfCancellationRequested();
			}
		}
	}
}
