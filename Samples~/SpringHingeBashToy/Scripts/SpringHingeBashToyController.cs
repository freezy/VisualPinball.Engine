// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VisualPinball.Unity.Samples.SpringHingeBashToy
{
	[DisallowMultipleComponent]
	[AddComponentMenu("Pinball/Samples/Spring Hinge Bash Toy Controller")]
	public sealed class SpringHingeBashToyController : MonoBehaviour
	{
		[Header("Fixture")]
		public Player Player;
		public SpringHingeComponent SpringHinge;
		public MagnetComponent OwnedMagnet;
		[Tooltip("Place on the playfield and point its forward axis toward the toy.")]
		public Transform ShotMarker;
		public GameObject BallPrefab;

		[Header("Shots")]
		[Min(0f)] public float WeakShotSpeed = 8f;
		[Min(0f)] public float MediumShotSpeed = 18f;
		[Min(0f)] public float StrongShotSpeed = 30f;
		[Min(0f)] public float ReleaseDelaySeconds = 1.5f;

		[Header("Diagnostics")]
		public bool ShowControls = true;
		public bool TraceEvents = true;

		private readonly List<GameObject> _spawnedBalls = new();
		private Coroutine _timedRelease;
		private string _lastTrace = "Ready";
		private bool _subscribed;

		private void Awake()
		{
			Player = Player ? Player : GetComponentInParent<Player>();
			SpringHinge = SpringHinge ? SpringHinge : GetComponentInChildren<SpringHingeComponent>(true);
			OwnedMagnet = OwnedMagnet ? OwnedMagnet : GetComponentInChildren<MagnetComponent>(true);
		}

		private void Start()
		{
			if (!IsReady) {
				Debug.LogError("Spring Hinge Bash Toy sample needs a Player, Spring Hinge, Owned Magnet, and Shot Marker.", this);
				return;
			}
			Player.OnBallCreated += OnBallCreated;
			Player.OnBallDestroyed += OnBallDestroyed;
			SpringHinge.OnAnimationValueChanged += OnAngleChanged;
			SpringHinge.SpringHingeApi.Hit += OnHingeHit;
			OwnedMagnet.MagnetApi.BallGrabbed += OnBallGrabbed;
			OwnedMagnet.MagnetApi.BallReleased += OnBallReleased;
			_subscribed = true;
			Trace("fixture started; hardware output is disabled");
		}

		private void OnDestroy()
		{
			if (!_subscribed) {
				return;
			}
			if (Player) {
				Player.OnBallCreated -= OnBallCreated;
				Player.OnBallDestroyed -= OnBallDestroyed;
			}
			if (SpringHinge) {
				SpringHinge.OnAnimationValueChanged -= OnAngleChanged;
				if (SpringHinge.SpringHingeApi != null) {
					SpringHinge.SpringHingeApi.Hit -= OnHingeHit;
				}
			}
			if (OwnedMagnet && OwnedMagnet.MagnetApi != null) {
				OwnedMagnet.MagnetApi.BallGrabbed -= OnBallGrabbed;
				OwnedMagnet.MagnetApi.BallReleased -= OnBallReleased;
			}
			_subscribed = false;
		}

		private bool IsReady => Player && SpringHinge && OwnedMagnet && ShotMarker
		                            && Player.BallManager != null
		                            && SpringHinge.SpringHingeApi != null
		                            && OwnedMagnet.MagnetApi != null;

		private void Update()
		{
			var keyboard = Keyboard.current;
			if (!IsReady || keyboard == null) {
				return;
			}
			if (keyboard.digit1Key.wasPressedThisFrame) LaunchWeak();
			if (keyboard.digit2Key.wasPressedThisFrame) LaunchMedium();
			if (keyboard.digit3Key.wasPressedThisFrame) LaunchStrong();
			if (keyboard.mKey.wasPressedThisFrame) ToggleMagnet();
			if (keyboard.tKey.wasPressedThisFrame) ReleaseAfterDelay();
			if (keyboard.rKey.wasPressedThisFrame) ResetFixture();
		}

		private void OnGUI()
		{
			if (!ShowControls || !IsReady) {
				return;
			}
			GUILayout.BeginArea(new Rect(12f, 12f, 230f, 235f), GUI.skin.box);
			GUILayout.Label("Spring Hinge Bash Toy");
			if (GUILayout.Button("Weak Shot [1]")) LaunchWeak();
			if (GUILayout.Button("Medium Shot [2]")) LaunchMedium();
			if (GUILayout.Button("Strong Shot [3]")) LaunchStrong();
			if (GUILayout.Button("Toggle Magnet [M]")) ToggleMagnet();
			if (GUILayout.Button("Timed Release [T]")) ReleaseAfterDelay();
			if (GUILayout.Button("Reset [R]")) ResetFixture();
			GUILayout.Label(_lastTrace);
			GUILayout.EndArea();
		}

		[ContextMenu("Launch Weak Shot")]
		public void LaunchWeak() => Launch(WeakShotSpeed);

		[ContextMenu("Launch Medium Shot")]
		public void LaunchMedium() => Launch(MediumShotSpeed);

		[ContextMenu("Launch Strong Shot")]
		public void LaunchStrong() => Launch(StrongShotSpeed);

		public int Launch(float speed)
		{
			if (!IsReady || speed <= 0f) {
				return 0;
			}
			var playfield = Player.Playfield.transform;
			var start = (float3)ShotMarker.position.TranslateToVpx(playfield);
			var ahead = (float3)(ShotMarker.position + ShotMarker.forward).TranslateToVpx(playfield);
			var direction = math.normalizesafe((ahead - start).xy, new float2(0f, -1f));
			var angle = math.degrees(math.atan2(direction.x, -direction.y));
			var ballId = Player.BallManager.CreateBall(new DebugBallCreator(
				start.x, start.y, start.z, angle, speed), 25f, 1f, BallPrefab);
			Trace($"shot {ballId}: {speed:0.##} VPU / normalized time");
			return ballId;
		}

		[ContextMenu("Toggle Magnet")]
		public void ToggleMagnet()
		{
			if (!IsReady) {
				return;
			}
			OwnedMagnet.MagnetApi.IsEnabled = !OwnedMagnet.MagnetApi.IsEnabled;
			Trace(OwnedMagnet.MagnetApi.IsEnabled ? "magnet on" : "magnet off");
		}

		[ContextMenu("Release After Delay")]
		public void ReleaseAfterDelay()
		{
			if (!IsReady) {
				return;
			}
			if (_timedRelease != null) {
				StopCoroutine(_timedRelease);
			}
			_timedRelease = StartCoroutine(ReleaseAfterDelayRoutine());
			Trace($"release scheduled in {ReleaseDelaySeconds:0.##} s");
		}

		private IEnumerator ReleaseAfterDelayRoutine()
		{
			yield return new WaitForSeconds(ReleaseDelaySeconds);
			_timedRelease = null;
			ReleaseNow();
		}

		public void ReleaseNow()
		{
			if (!IsReady) {
				return;
			}
			OwnedMagnet.MagnetApi.ReleaseBall();
			OwnedMagnet.MagnetApi.IsEnabled = false;
			Trace("magnetic hold released");
		}

		[ContextMenu("Reset Fixture")]
		public void ResetFixture()
		{
			if (!IsReady) {
				return;
			}
			if (_timedRelease != null) {
				StopCoroutine(_timedRelease);
				_timedRelease = null;
			}
			ReleaseNow();
			SpringHinge.SpringHingeApi.Reset(SpringHinge.InitialAngle);
			for (var i = _spawnedBalls.Count - 1; i >= 0; i--) {
				var ball = _spawnedBalls[i];
				if (ball && ball.TryGetComponent<BallComponent>(out var component)) {
					Player.BallManager.DestroyBall(component.Id);
				}
			}
			_spawnedBalls.Clear();
			Trace("fixture reset");
		}

		private void OnBallCreated(object sender, BallEvent args) => _spawnedBalls.Add(args.Ball);

		private void OnBallDestroyed(object sender, BallEvent args) => _spawnedBalls.Remove(args.Ball);

		private void OnAngleChanged(float radians) => Trace($"hinge angle {math.degrees(radians):0.##}°");

		private void OnHingeHit(object sender, HitEventArgs args) => Trace($"hinge impact by ball {args.BallId}");

		private void OnBallGrabbed(object sender, HitEventArgs args) => Trace($"ball {args.BallId} captured");

		private void OnBallReleased(object sender, HitEventArgs args) => Trace($"ball {args.BallId} released");

		private void Trace(string message)
		{
			_lastTrace = message;
			if (TraceEvents) {
				Debug.Log($"[SpringHingeBashToy] {message}", this);
			}
		}
	}
}
