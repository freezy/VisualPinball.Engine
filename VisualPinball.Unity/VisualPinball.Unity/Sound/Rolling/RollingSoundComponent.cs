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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Table-wide rolling audio. Physics publishes contacts; only this main-thread owner touches audio.</summary>
	[DisallowMultipleComponent, PackAs("RollingSound")]
	[AddComponentMenu("Pinball/Sound/Rolling Sound")]
	public sealed class RollingSoundComponent : MonoBehaviour, IPackable
	{
		public BallSoundMaterialAsset DefaultBallMaterial;
		[Min(0)] public float Volume = 1;
		[Min(0)] public float PlayfieldVolume = 0.5f;
		[Min(0)] public float RampVolume = 0.5f;
		public bool MaterialTransitionSounds = true;
		[Min(1024)] public int TransitionCapacity = 4096;
		private readonly List<RollingSoundMaterialAsset> _materials = new() { null };
		private readonly Dictionary<int, Session> _sessions = new();
		private readonly Dictionary<string, Dictionary<int, int>> _slots = new();
		private readonly List<Control> _staged = new(128);
		private readonly List<RollingTransition> _transitions = new(4096);
		private readonly List<int> _removed = new();
		private readonly List<OneShot> _oneShots = new();
		private readonly HashSet<(RollingSoundMaterialAsset, BallSoundMaterialAsset)> _missingResponses = new();
		private PhysicsEngineContext _context;
		private Player _player;
		private long _overflow;
		private bool _lostTransitions;
		private bool _resuming = true;
		private bool _paused;
		public long TransitionOverflows { get; private set; }
		public long LoopStarts { get; private set; }
		public long PitchClamps { get; private set; }

		internal void Prepare(PhysicsEngineContext context)
		{
			_context = context; _player = GetComponent<Player>();
			_materials.Clear(); _materials.Add(null);
			context.Rolling = new RollingPhysicsState(128, System.Math.Max(TransitionCapacity, 1024), Allocator.Persistent);
			context.RollingRenderer = this;
		}

		/// <summary>Called while colliders are generated, before the producer starts. Material inheritance ignores PhysicsOverwrite.</summary>
		internal static int SurfaceId(Component item, PhysicsMaterialAsset physicalMaterial)
		{
			if (!item) return 0;
			var controller = item.GetComponentInParent<RollingSoundComponent>();
			if (!controller || controller._context == null) return 0;
			var setting = item.GetComponent<RollingSoundSurfaceComponent>();
			var surface = setting && setting.enabled && setting.Mode != RollingSurfaceMode.Inherit
				? (setting.Mode == RollingSurfaceMode.Silent ? null : setting.Material) : physicalMaterial ? physicalMaterial.RollingSoundMaterial : null;
			if (setting && setting.enabled && setting.UseLocalSupport) {
				var collidable = item.GetComponents<MonoBehaviour>().OfType<ICollidableComponent>().FirstOrDefault();
				var playfield = item.GetComponentInParent<PlayfieldComponent>();
				if (collidable != null && playfield) {
					var local = math.normalizesafe(((float3)setting.LocalSupportAcceleration).TranslateToVpx());
					var matrix = collidable.GetLocalToPlayfieldMatrixInVpx(playfield.transform.worldToLocalMatrix);
					controller._context.Rolling.SupportFrames[UnityObjectId.Get(item.gameObject)] = new RollingSupportFrame { LocalDirection = local, StaticDirection = math.normalizesafe(matrix.MultiplyVector(local)) };
				}
			}
			if (!surface) return 0;
			var index = controller._materials.IndexOf(surface);
			if (index >= 0) return index;
			controller._materials.Add(surface); return controller._materials.Count - 1;
		}

		// Both methods run on the main thread. Capture is inside PhysicsLock; Apply runs after gameplay callbacks.
		internal void Capture()
		{
			_staged.Clear(); _transitions.Clear();
			if (_context == null || !_context.Rolling.IsCreated) return;
			var native = _context.Rolling;
			var watermark = native.Transitions.Cursors[1];
			_lostTransitions = native.Transitions.Cursors[2] != _overflow;
			_overflow = native.Transitions.Cursors[2]; TransitionOverflows = _overflow;
			using (var balls = native.Balls.GetEnumerator()) while (balls.MoveNext()) {
				var ball = balls.Current.Value;
				_staged.Add(new Control { Ball = ball.BallId, Generation = ball.Generation, Frozen = ball.Frozen, Supported = ball.Supported, SupportItemId = ball.SupportItemId, Surface = ball.Surface, Reserved = ball.ReservedSurface, Speed = ball.Speed, Velocity = ball.Velocity, Position = ball.Position });
			}
			while (native.Transitions.TryPop(watermark, out var transition)) _transitions.Add(transition);

		}

		internal void Apply()
		{
			if (!isActiveAndEnabled || _context == null) { StopAll(); return; }
			if (_paused) return;
			var overflowed = _lostTransitions;
			var suppressBoundaries = overflowed || _resuming;
			if (overflowed) StopAll();
			_lostTransitions = _resuming = false;
			foreach (var control in _staged) {
				if (!IsCurrent(control.Ball, control.Generation, out _)) continue;
				if (control.Frozen) { ForgetBall(control.Ball); continue; }
				if (!_sessions.TryGetValue(control.Ball, out var session) || session.Generation != control.Generation) {
					if (session != null) DisposeSession(control.Ball, session);
					_sessions[control.Ball] = new Session { Generation = control.Generation };
				}
			}
			// Cross-ball slot ownership must follow the producer's sequence, not native-map enumeration order.
			if (!overflowed) foreach (var transition in _transitions) {
				if (!IsCurrent(transition.BallId, transition.Generation, out var component)
					|| !_sessions.TryGetValue(transition.BallId, out var session) || session.Generation != transition.Generation) continue;
				ChangeSlot(transition.BallId, session, transition.To);
				if (MaterialTransitionSounds && !suppressBoundaries) PlayBoundary(session, component.SoundMaterial ? component.SoundMaterial : DefaultBallMaterial, transition);
			}
			foreach (var control in _staged) {
				if (!IsCurrent(control.Ball, control.Generation, out var component) || control.Frozen
					|| !_sessions.TryGetValue(control.Ball, out var session)) continue;
				ApplyControl(control, component, session);
			}
			_removed.Clear();
			foreach (var pair in _sessions) if (!_context.BallComponents.ContainsKey(pair.Key)) _removed.Add(pair.Key);
			foreach (var ball in _removed) ForgetBall(ball);
		}

		private bool IsCurrent(int ball, uint generation, out BallComponent component)
			=> _context.BallComponents.TryGetValue(ball, out component) && component
				&& _context.RollingGenerations.TryGetValue(ball, out var current) && current == generation;

		private void ApplyControl(Control control, BallComponent component, Session session)
		{
			var ballTag = component.SoundMaterial ? component.SoundMaterial : DefaultBallMaterial;
			ChangeSlot(control.Ball, session, control.Reserved);
			var material = Material(control.Surface);
			var response = material ? material.FindResponse(ballTag) : null;
			if (material && response == null && _missingResponses.Add((material, ballTag))) Debug.LogWarning($"No rolling response for {material.name} and {(ballTag ? ballTag.name : "untagged ball")}; this pair is silent.", material);
			var desired = response != null ? response.Loop : null;
			if (desired && !desired.Loop) desired = null;
			var speed = response != null ? response.Speed(control.Velocity, control.Speed) : 0;
			var gain = desired ? response.Gain(speed) * Volume * (material.Group == RollingSoundGroup.Playfield ? PlayfieldVolume : RampVolume) : 0;
			if (!desired) session.PendingBank = null;
			else if (!Matches(session.Active, desired, SelectedSlot(desired, material, session.Slot)) || session.SlotChanged) Retarget(session, desired, response, material);
			else session.PendingBank = null;
			session.SlotChanged = false;
			if (session.Active != null) {
				var voice = session.Active;
				voice.ReleaseWhenSilent = control.Reserved == 0 || (control.Supported && !desired);
				var target = !desired || voice.Bank == desired ? math.saturate(voice.BaseGain * gain) : voice.Target;
				if (desired && voice.Bank == desired) voice.Response = response;
				voice.FadeTo(target, target > voice.Level ? (response?.AttackSeconds ?? 0.01f) : (response?.ReleaseSeconds ?? voice.Response?.ReleaseSeconds ?? 0.02f));
				SetPosition(voice.Source.transform, control.Position);
				if (response != null && voice.Bank == desired && voice.Source.clip) {
					var pitch = voice.BasePitch * response.Pitch(speed, voice.Source.clip.frequency);
					if (pitch > 3 || pitch < 0.01f) PitchClamps++;
					voice.Source.pitch = math.clamp(pitch, 0.01f, 3);
				}
			}
			if (session.Outgoing != null) SetPosition(session.Outgoing.Source.transform, control.Position);
		}

		private void Update() => UpdateVoices(Time.unscaledDeltaTime);

		internal void UpdateVoices(float deltaTime)
		{
			if (_paused) return;
			foreach (var session in _sessions.Values) {
				Advance(ref session.Active, deltaTime); Advance(ref session.Outgoing, deltaTime);
				if (session.PendingBank && session.Outgoing == null) {
					var bank = session.PendingBank; var response = session.PendingResponse; var material = session.PendingMaterial;
					session.PendingBank = null; Retarget(session, bank, response, material);
				}
			}
			for (var i = _oneShots.Count - 1; i >= 0; i--) if (!_oneShots[i].Source || !_oneShots[i].Source.isPlaying) { DisposeVoice(_oneShots[i].Source); _oneShots.RemoveAt(i); }
		}

		private void Retarget(Session session, SoundEffectAsset bank, RollingSoundResponse response, RollingSoundMaterialAsset material)
		{
			var slot = SelectedSlot(bank, material, session.Slot);
			if (!Matches(session.Active, bank, slot) && Matches(session.Outgoing, bank, slot)) {
				var previous = session.Active; session.Active = session.Outgoing; session.Outgoing = previous;
			}
			if (Matches(session.Active, bank, slot)) {
				// Returning to an existing loop preserves its playback position, even if it is quieter.
				session.PendingBank = null;
				if (session.Outgoing != null) { session.Outgoing.FadeTo(0, 0.003f); session.Outgoing.ReleaseWhenSilent = true; }
				return;
			}
			// At most two loops per ball. On a mid-fade retarget, de-click the quieter voice before reusing its place.
			if (session.Outgoing != null) {
				if (session.Active != null && session.Outgoing.Level > session.Active.Level) {
					var quieter = session.Active; session.Active = session.Outgoing; session.Outgoing = quieter;
				}
				session.Outgoing.FadeTo(0, 0.003f); session.Outgoing.ReleaseWhenSilent = true;
				session.PendingBank = bank; session.PendingResponse = response; session.PendingMaterial = material;
				return;
			}
			session.Outgoing = session.Active;
			if (session.Outgoing != null) { session.Outgoing.FadeTo(0, response.CrossfadeSeconds); session.Outgoing.ReleaseWhenSilent = true; }
			var source = CreateSource(bank);
			if (!source) { session.Active = null; return; }
			if (slot >= 0) source.clip = bank.Clips[slot];
			session.Active = new Voice { Source = source, Bank = bank, Slot = slot, BaseGain = MechanicalSoundGain.AuthoredLevel(source), BasePitch = source.pitch, Response = response };
			MechanicalSoundGain.SetAuthoredLevel(source, 0); source.Play(); LoopStarts++;
		}
		private static bool Matches(Voice voice, SoundEffectAsset bank, int slot) => voice != null && voice.Bank == bank && voice.Slot == slot;
		private static int SelectedSlot(SoundEffectAsset bank, RollingSoundMaterialAsset material, int slot)
			=> material.VariantMode == RollingVariantMode.FirstFreeSlot && bank.Clips != null && slot >= 0 && slot < bank.Clips.Length ? slot : -1;

		private AudioSource CreateSource(SoundEffectAsset bank)
		{
			if (!bank || bank.Clips == null || bank.Clips.Length == 0) return null;
			var go = new GameObject("Rolling: " + bank.name); go.transform.SetParent(transform, false);
			var source = go.AddComponent<AudioSource>(); bank.ConfigureAudioSource(source); source.dopplerLevel = 0;
			MechanicalSoundGain.Register(source); return source;
		}

		private void ChangeSlot(int ball, Session session, int surface)
		{
			var material = Material(surface);
			var pool = material && material.VariantMode == RollingVariantMode.FirstFreeSlot ? material.SlotPool ?? material.name : null;
			if (session.Surface == surface) return;
			session.Surface = surface;
			if (session.Pool != null && _slots.TryGetValue(session.Pool, out var previous)) previous.Remove(ball);
			session.Pool = pool; session.Slot = -1; session.SlotChanged = true;
			if (pool == null) return;
			if (!_slots.TryGetValue(pool, out var slots)) _slots[pool] = slots = new Dictionary<int, int>();
			for (var slot = 0; slot < System.Math.Max(1, material.SlotCount); slot++) if (!slots.ContainsValue(slot)) { session.Slot = slot; break; }
			// Overflow remains audible without stealing another ball's slot.
			if (session.Slot < 0) session.Slot = (int)((uint)ball % (uint)System.Math.Max(1, material.SlotCount));
			slots[ball] = session.Slot;
		}

		private void PlayBoundary(Session session, BallSoundMaterialAsset ballTag, RollingTransition transition)
		{
			BoundaryBank(session, Material(transition.From), ballTag, false, transition);
			BoundaryBank(session, Material(transition.To), ballTag, true, transition);
		}

		private void BoundaryBank(Session session, RollingSoundMaterialAsset material, BallSoundMaterialAsset ballTag, bool enter, RollingTransition transition)
		{
			var response = material ? material.FindResponse(ballTag) : null;
			if (response == null || transition.Speed < response.TransitionMinimumSpeed || (transition.Air && !(enter ? response.AirArrival : response.AirDeparture))) return;
			var key = ((long)(uint)transition.From << 32) | (uint)transition.To;
			// Exit and enter have independent cooldowns, scoped to this ball and boundary.
			key = unchecked(key * 2 + (enter ? 1 : 0));
			if (session.BoundaryTimes.TryGetValue(key, out var time) && transition.Time - time < response.TransitionCooldownSeconds) return;
			var bank = enter ? response.Enter : response.Exit;
			if (!bank || bank.Loop) return;
			session.BoundaryTimes[key] = transition.Time;
			if (_oneShots.Count >= 32) { DisposeVoice(_oneShots[0].Source); _oneShots.RemoveAt(0); }
			var source = CreateSource(bank); if (!source) return;
			SetPosition(source.transform, transition.Position);
			MechanicalSoundGain.SetAuthoredLevel(source, MechanicalSoundGain.AuthoredLevel(source) * Volume * (material.Group == RollingSoundGroup.Playfield ? PlayfieldVolume : RampVolume));
			source.Play(); _oneShots.Add(new OneShot { Source = source });
		}

		private void Advance(ref Voice voice, float deltaTime)
		{
			if (voice == null) return;
			voice.FadeElapsed += deltaTime;
			voice.Level = math.lerp(voice.FadeStart, voice.Target, math.saturate(voice.FadeElapsed / math.max(0.001f, voice.FadeSeconds)));
			MechanicalSoundGain.SetAuthoredLevel(voice.Source, voice.Level);
			if (voice.ReleaseWhenSilent && voice.Level <= 0) { DisposeVoice(voice.Source); voice = null; }
		}

		private RollingSoundMaterialAsset Material(int id) => id > 0 && id < _materials.Count ? _materials[id] : null;
		private void SetPosition(Transform target, float3 position) => target.position = _player ? (Vector3)math.transform(_player.PlayfieldToWorldMatrix, Physics.TranslateToWorld(position.x, position.y, position.z)) : transform.position;
		private static void DisposeVoice(AudioSource source) { if (!source) return; MechanicalSoundGain.Unregister(source); source.Stop(); if (Application.isPlaying) Destroy(source.gameObject); else DestroyImmediate(source.gameObject); }
		private void DisposeSession(int ball, Session session) { if (session.Active != null) DisposeVoice(session.Active.Source); if (session.Outgoing != null) DisposeVoice(session.Outgoing.Source); if (session.Pool != null && _slots.TryGetValue(session.Pool, out var slots)) slots.Remove(ball); }
		internal void ForgetBall(int ball) { if (_sessions.TryGetValue(ball, out var session)) { DisposeSession(ball, session); _sessions.Remove(ball); } }
		private void StopAll() { foreach (var pair in _sessions) DisposeSession(pair.Key, pair.Value); _sessions.Clear(); _slots.Clear(); foreach (var shot in _oneShots) DisposeVoice(shot.Source); _oneShots.Clear(); }
		public void SetPaused(bool paused)
		{
			if (_paused == paused) return;
			_paused = paused;
			foreach (var session in _sessions.Values) {
				PauseSource(session.Active?.Source, paused); PauseSource(session.Outgoing?.Source, paused);
			}
			foreach (var shot in _oneShots) PauseSource(shot.Source, paused);
			if (!paused) _resuming = true;
		}
		private static void PauseSource(AudioSource source, bool paused) { if (!source) return; if (paused) source.Pause(); else source.UnPause(); }
		private void OnApplicationPause(bool paused) => SetPaused(paused);
		private void OnEnable() { _resuming = true; _paused = false; }
		private void OnDisable() => StopAll();
		private void OnDestroy() => StopAll();

		[Serializable] private struct Data { public float Volume, PlayfieldVolume, RampVolume; public bool Transitions; public int Capacity; }
		public byte[] Pack() => PackageApi.Packer.Pack(new Data { Volume = Volume, PlayfieldVolume = PlayfieldVolume, RampVolume = RampVolume, Transitions = MaterialTransitionSounds, Capacity = TransitionCapacity });
		public void Unpack(byte[] bytes) { var data = PackageApi.Packer.Unpack<Data>(bytes); Volume = data.Volume; PlayfieldVolume = data.PlayfieldVolume; RampVolume = data.RampVolume; MaterialTransitionSounds = data.Transitions; TransitionCapacity = data.Capacity; }
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => PackageApi.Packer.Pack(files.Rolling.AddBall(DefaultBallMaterial));
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) => DefaultBallMaterial = files.Rolling.Ball(PackageApi.Packer.Unpack<int>(bytes));

		private struct Control { public int Ball, Surface, Reserved, SupportItemId; public bool Frozen, Supported; public uint Generation; public float Speed; public float3 Velocity, Position; }
		private sealed class Voice
		{
			public AudioSource Source;
			public SoundEffectAsset Bank;
			public RollingSoundResponse Response;
			public int Slot;
			public float BaseGain, BasePitch, Level, Target, FadeSeconds, FadeStart, FadeElapsed;
			public bool ReleaseWhenSilent;
			public void FadeTo(float target, float seconds)
			{
				if (Target == target && FadeSeconds == seconds) return;
				FadeStart = Level; FadeElapsed = 0; Target = target; FadeSeconds = seconds;
			}
		}
		private sealed class Session { public uint Generation; public int Surface; public string Pool; public bool SlotChanged; public int Slot = -1; public SoundEffectAsset PendingBank; public RollingSoundResponse PendingResponse; public RollingSoundMaterialAsset PendingMaterial; public Voice Active, Outgoing; public readonly Dictionary<long, double> BoundaryTimes = new(); }
		private struct OneShot { public AudioSource Source; }
	}
}
