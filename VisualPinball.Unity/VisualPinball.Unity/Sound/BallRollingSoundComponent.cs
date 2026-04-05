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

using System.Collections.Generic;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Plays a continuous looping rolling sound for every ball on the playfield.
	/// Volume is driven by the ball's linear speed; pitch is driven by its rotational
	/// (angular) speed. Place this component on the same GameObject as the
	/// <see cref="Player"/> component (the table root).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The component subscribes to <see cref="Player.OnBallCreated"/> and
	/// <see cref="Player.OnBallDestroyed"/>. For every ball that is created, it adds
	/// an <see cref="AudioSource"/> to the ball's <c>GameObject</c> and starts
	/// playing the assigned <see cref="RollingSoundAsset"/> in a loop at zero volume.
	/// Every <c>Update</c> frame the volume and pitch of each source are smoothly
	/// adjusted based on the current ball state read from <see cref="PhysicsEngine"/>.
	/// </para>
	/// <para>
	/// Assign a looping <see cref="SoundEffectAsset"/> (with the <em>Loop</em> flag
	/// checked in the asset inspector) as the <see cref="RollingSoundAsset"/>. For
	/// authentic 3D positioning, set the asset's <em>Type</em> to <em>Mechanical</em>
	/// so that Unity's 3D spatialisation is applied from the ball's world position.
	/// </para>
	/// </remarks>
	[AddComponentMenu("Pinball/Sound/Ball Rolling Sound")]
	public class BallRollingSoundComponent : MonoBehaviour
	{
		#region Inspector fields

		[Tooltip("The looping sound asset to play while the ball is rolling. " +
		         "Make sure the SoundEffectAsset has its Loop flag enabled.")]
		public SoundEffectAsset RollingSoundAsset;

		[Tooltip("Master volume multiplier applied on top of the speed-based volume.")]
		[Range(0f, 1f)]
		public float Volume = 1f;

		[Tooltip("Ball speed (VPX units/s) below which the rolling sound is completely silent.")]
		public float MinSpeed = 1f;

		[Tooltip("Ball speed (VPX units/s) at which the rolling sound reaches its full volume.")]
		public float MaxVolumeSpeed = 50f;

		[Tooltip("Pitch range of the rolling sound.\n" +
		         "x = pitch when the ball's angular speed is near zero.\n" +
		         "y = pitch when the ball's angular speed is at or above MaxPitchAngularSpeed.")]
		public Vector2 PitchRange = new Vector2(0.8f, 1.5f);

		[Tooltip("Angular speed (rad/s) of the ball at which the maximum pitch (PitchRange.y) is applied.")]
		public float MaxPitchAngularSpeed = 200f;

		[Tooltip("How fast the AudioSource volume tracks the target value each frame.\n" +
		         "1 = instant, 0.01 = very slow/smooth.")]
		[Range(0.01f, 1f)]
		public float VolumeSmoothing = 0.15f;

		[Tooltip("How fast the AudioSource pitch tracks the target value each frame.\n" +
		         "1 = instant, 0.01 = very slow/smooth.")]
		[Range(0.01f, 1f)]
		public float PitchSmoothing = 0.1f;

		#endregion

		#region Private state

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Player _player;
		private PhysicsEngine _physicsEngine;

		/// <summary>Per-ball audio state, keyed by ball instance ID.</summary>
		private readonly Dictionary<int, BallRollingAudio> _ballAudio = new();

		/// <summary>
		/// Snapshot list used during <c>Update</c> to avoid modifying the dictionary
		/// while iterating over it when a ball-destroyed event fires.
		/// </summary>
		private readonly List<int> _updateIds = new();

		#endregion

		#region Unity lifecycle

		private void Awake()
		{
			_physicsEngine = GetComponentInChildren<PhysicsEngine>();
			_player = GetComponentInChildren<Player>();

			if (_physicsEngine == null)
				Logger.Warn("[BallRollingSoundComponent] PhysicsEngine not found as a child. " +
				            "Make sure this component is on the table root GameObject.");

			if (_player == null)
				Logger.Warn("[BallRollingSoundComponent] Player not found as a child. " +
				            "Make sure this component is on the table root GameObject.");
		}

		private void OnEnable()
		{
			if (_player != null) {
				_player.OnBallCreated += OnBallCreated;
				_player.OnBallDestroyed += OnBallDestroyed;
			}
		}

		private void OnDisable()
		{
			if (_player != null) {
				_player.OnBallCreated -= OnBallCreated;
				_player.OnBallDestroyed -= OnBallDestroyed;
			}
			StopAllRollingSounds();
		}

		private void OnDestroy()
		{
			StopAllRollingSounds();
		}

		private void Update()
		{
			if (_physicsEngine == null || _ballAudio.Count == 0)
				return;

			// Snapshot the keys so that a ball-destroyed event received inside the
			// loop cannot mutate the dictionary while we iterate.
			_updateIds.Clear();
			foreach (var id in _ballAudio.Keys)
				_updateIds.Add(id);

			foreach (var ballId in _updateIds) {
				if (!_ballAudio.TryGetValue(ballId, out var audio))
					continue;

				if (!_physicsEngine.BallExists(ballId)) {
					// The ball was destroyed; silence the source and wait for the event.
					audio.SetTarget(0f, PitchRange.x);
				} else {
					ref var ball = ref _physicsEngine.BallState(ballId);
					UpdateAudioForBall(ref ball, audio);
				}

				audio.Apply(VolumeSmoothing, PitchSmoothing);
			}
		}

		#endregion

		#region Audio helpers

		private void UpdateAudioForBall(ref BallState ball, BallRollingAudio audio)
		{
			if (ball.IsFrozen) {
				audio.SetTarget(0f, PitchRange.x);
				return;
			}

			// --- Volume ---
			// Linear ramp from MinSpeed to MaxVolumeSpeed.
			var speed = math.length(ball.Velocity);
			var volumeT = math.saturate(
				(speed - MinSpeed) / math.max(MaxVolumeSpeed - MinSpeed, 1e-4f)
			);
			var targetVolume = volumeT * Volume;

			// --- Pitch ---
			// Derive angular speed (rad/s) from angular momentum.
			// AngularMomentum = inertia * angularVelocity  ⟹  ω = L / I
			var angularSpeed = math.length(ball.AngularMomentum) / ball.Inertia;
			var pitchT = math.saturate(angularSpeed / math.max(MaxPitchAngularSpeed, 1e-4f));
			var targetPitch = math.lerp(PitchRange.x, PitchRange.y, pitchT);

			audio.SetTarget(targetVolume, targetPitch);
		}

		private void OnBallCreated(object sender, BallEvent e)
		{
			if (RollingSoundAsset == null || !RollingSoundAsset.IsValid()) {
				Logger.Warn("[BallRollingSoundComponent] No valid RollingSoundAsset assigned. " +
				            "Assign a looping SoundEffectAsset in the Inspector.");
				return;
			}

			if (_ballAudio.ContainsKey(e.BallId))
				return;

			// Add an AudioSource directly to the ball's GameObject so Unity's 3D audio
			// engine automatically places the sound at the ball's world position.
			var audioSource = e.Ball.AddComponent<AudioSource>();
			RollingSoundAsset.ConfigureAudioSource(audioSource);

			// Override settings that we control ourselves at runtime.
			audioSource.volume = 0f;         // Start silent; volume driven by speed.
			audioSource.pitch = PitchRange.x; // Start at minimum pitch.
			audioSource.loop = true;           // Must loop regardless of asset setting.
			audioSource.Play();

			_ballAudio[e.BallId] = new BallRollingAudio(audioSource);
			Logger.Debug($"[BallRollingSoundComponent] Started rolling sound for ball {e.BallId}.");
		}

		private void OnBallDestroyed(object sender, BallEvent e)
		{
			if (_ballAudio.TryGetValue(e.BallId, out var audio)) {
				// Stop playback; the ball's GameObject (and its AudioSource) will be
				// destroyed immediately after this event returns.
				audio.Stop();
				_ballAudio.Remove(e.BallId);
				Logger.Debug($"[BallRollingSoundComponent] Stopped rolling sound for ball {e.BallId}.");
			}
		}

		private void StopAllRollingSounds()
		{
			foreach (var audio in _ballAudio.Values)
				audio.Stop();
			_ballAudio.Clear();
		}

		#endregion
	}

	/// <summary>
	/// Holds the <see cref="AudioSource"/> attached to a single ball and
	/// smoothly drives it towards target volume and pitch values.
	/// </summary>
	internal sealed class BallRollingAudio
	{
		private readonly AudioSource _audioSource;
		private float _targetVolume;
		private float _targetPitch;

		internal BallRollingAudio(AudioSource audioSource)
		{
			_audioSource = audioSource;
			_targetVolume = 0f;
			_targetPitch = 1f;
		}

		/// <summary>Set the desired volume and pitch for the next <see cref="Apply"/> call.</summary>
		internal void SetTarget(float volume, float pitch)
		{
			_targetVolume = volume;
			_targetPitch = pitch;
		}

		/// <summary>
		/// Smoothly lerp the <see cref="AudioSource"/> towards the target values.
		/// </summary>
		/// <param name="volumeSmoothing">Lerp factor for volume (0 = no change, 1 = instant).</param>
		/// <param name="pitchSmoothing">Lerp factor for pitch (0 = no change, 1 = instant).</param>
		internal void Apply(float volumeSmoothing, float pitchSmoothing)
		{
			if (_audioSource == null)
				return;

			_audioSource.volume = Mathf.Lerp(_audioSource.volume, _targetVolume, volumeSmoothing);
			_audioSource.pitch = Mathf.Lerp(_audioSource.pitch, _targetPitch, pitchSmoothing);
		}

		/// <summary>Stop audio playback immediately.</summary>
		internal void Stop()
		{
			if (_audioSource != null)
				_audioSource.Stop();
		}
	}
}
