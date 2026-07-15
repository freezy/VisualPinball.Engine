// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class DmdCuePlayerTests
	{
		private readonly List<DmdCueAsset> _cues = new List<DmdCueAsset>();
		private DmdProjectAsset _project;
		private RecordingSink _sink;

		[SetUp]
		public void SetUp()
		{
			_project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			_project.DisplayId = "studio";
			_project.Width = 4;
			_project.Height = 2;
			_project.FrameRate = 10;
			_project.ColorMode = DmdColorMode.Mono16;
			_sink = new RecordingSink();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var cue in _cues) {
				Object.DestroyImmediate(cue);
			}
			Object.DestroyImmediate(_project);
		}

		[Test]
		public void StartIsIdempotentAndAnnouncesThenEmitsConfiguredBase()
		{
			var cue = Cue("base", CuePriority.Base, 0, 255);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.SetBase(cue.EffectiveId);
				player.Start();
				player.Start();

				Assert.That(_sink.Requests, Has.Count.EqualTo(1));
				Assert.That(_sink.Frames, Has.Count.EqualTo(1));
				Assert.That(_sink.Frames[0].Format, Is.EqualTo(DisplayFrameFormat.Dmd4));
				Assert.That(_sink.Snapshots[0][0], Is.EqualTo(15));
			}
			Assert.That(_sink.Clears, Is.EqualTo(new[] { "studio" }));
		}

		[Test]
		public void StaticContentEmitsOnlyOnceWhileItsLifecycleStillAdvances()
		{
			var cue = Cue("hold", CuePriority.Award, 0, 200);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.Play(cue.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.Tick(1d);

				Assert.That(_sink.Frames, Has.Count.EqualTo(1));
			}
		}

		[Test]
		public void PlayAfterStartImmediatelyEmitsFrameZeroAndOneFrameCueRemainsVisibleForOneTick()
		{
			var cue = Cue("one-frame", CuePriority.Award, 1, 255);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var finished = 0;
				player.OnCueFinished += (_, __) => finished++;
				player.Start();
				player.Tick(0d);

				var handle = player.Play(cue.EffectiveId);

				Assert.That(handle.IsValid, Is.True);
				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1][0], Is.EqualTo(15));
				Assert.That(finished, Is.Zero);
				player.Tick(0.099d);
				Assert.That(finished, Is.Zero);
				player.Tick(0.1d);
				Assert.That(finished, Is.EqualTo(1));
			}
		}

		[Test]
		public void PreemptingAnActiveTransitionStartsFromTheLastCompositedSnapshot()
		{
			var first = Cue("first-transition", CuePriority.Mode, 0, 255);
			first.EnterTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.WipeOn, Direction = DmdDirection.Right, DurationFrames = 4
			};
			var incoming = Cue("snapshot-preempt", CuePriority.System, 0, 128);
			incoming.EnterTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack, DurationFrames = 2
			};
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.Play(first.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.Tick(0.2d);
				var blended = (byte[])_sink.Snapshots[_sink.Snapshots.Count - 1].Clone();

				player.Play(incoming.EffectiveId);

				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1], Is.EqualTo(blended));
			}
		}

		[Test]
		public void NaturalEndUsesOutgoingExitInsteadOfSuccessorEnter()
		{
			var outgoing = Cue("natural-outgoing", CuePriority.Award, 4, 255);
			outgoing.ExitTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack, DurationFrames = 2
			};
			var successor = Cue("natural-successor", CuePriority.Status, 0, 128, CueInterruptPolicy.Queue);
			successor.EnterTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.WipeOn, Direction = DmdDirection.Right, DurationFrames = 2
			};
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.Play(outgoing.EffectiveId);
				player.Play(successor.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.Tick(0.3d);

				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1].Distinct().Count(), Is.EqualTo(1),
					"Fade-through-black is spatially uniform; a successor wipe would split the frame.");
				player.Tick(0.4d);
				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1], Is.All.EqualTo(8));
			}
		}

		[Test]
		public void MalformedReferencedAssetSkipsOnlyItsLayerAndCueStillPlays()
		{
			var cue = Cue("partial-assets", CuePriority.Award, 0, 255);
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			sprite.Frames.Add(new DmdBitmapData { Width = 2, Height = 1, Pixels = new byte[1] });
			sprite.FrameDurations.Add(1);
			_project.Sprites.Add(sprite);
			cue.Layers.Add(new BitmapLayer { Sprite = sprite });
			try {
				using (var player = new DmdCuePlayer(_project, _sink)) {
					var diagnostics = new List<string>();
					player.OnValidationError += (_, message) => diagnostics.Add(message);
					var handle = player.Play(cue.EffectiveId);
					player.Start();

					Assert.That(handle.IsValid, Is.True);
					Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1], Is.All.EqualTo(15));
					Assert.That(diagnostics.Any(message => message.Contains("malformed") || message.Contains("Pixels")),
						Is.True);
				}
			} finally {
				Object.DestroyImmediate(sprite);
			}
		}

		[Test]
		public void HitchAdvancesEveryLifecycleEventButRendersOnlyNewestFrame()
		{
			var first = Cue("first", CuePriority.Award, 1, 100);
			var second = Cue("second", CuePriority.Award, 1, 200, CueInterruptPolicy.Queue);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var finished = new List<CueHandle>();
				player.OnCueFinished += (_, handle) => finished.Add(handle);
				var firstHandle = player.Play(first.EffectiveId);
				var secondHandle = player.Play(second.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.Tick(1d);

				Assert.That(finished, Is.EqualTo(new[] { firstHandle, secondHandle }));
				Assert.That(_sink.Frames, Has.Count.EqualTo(2));
			}
		}

		[Test]
		public void ActiveStopFiresFinishedOnlyAfterExitTransitionCompletes()
		{
			var cue = Cue("exit", CuePriority.Award, 0, 255);
			cue.ExitTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack, DurationFrames = 2
			};
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var finished = 0;
				player.OnCueFinished += (_, __) => finished++;
				var handle = player.Play(cue.EffectiveId);
				player.Start();
				player.Tick(0d);
				Assert.That(player.StopCue(handle), Is.True);
				player.Tick(0.1d);
				Assert.That(finished, Is.Zero);
				Assert.That(player.UpdateCue(handle, new DmdParams().Set("duringExit", 1)), Is.True);
				player.Tick(0.2d);
				Assert.That(finished, Is.EqualTo(1));
				Assert.That(player.UpdateCue(handle, new DmdParams().Set("afterExit", 1)), Is.False);
			}
		}

		[Test]
		public void StoppingQueuedCueFinishesImmediatelyAndMakesItsHandleStale()
		{
			var active = Cue("active", CuePriority.Mode, 0, 100);
			var queued = Cue("queued", CuePriority.Status, 0, 200);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.Play(active.EffectiveId);
				var handle = player.Play(queued.EffectiveId);
				CueHandle finished = default;
				player.OnCueFinished += (_, value) => finished = value;

				Assert.That(player.StopCue(handle), Is.True);
				Assert.That(finished, Is.EqualTo(handle));
				Assert.That(player.StopCue(handle), Is.False);
			}
		}

		[Test]
		public void CoalescedPlayMergesParametersWithoutRestartingOrChangingHandle()
		{
			var cue = Cue("award", CuePriority.Award, 0, 255);
			cue.CoalesceKey = "jackpot";
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var first = player.Play(cue.EffectiveId, new DmdParams().Set("value", 1));
				var second = player.Play(cue.EffectiveId, new DmdParams().Set("value", 2));

				Assert.That(second, Is.EqualTo(first));
				Assert.That(player.UpdateCue(first, new DmdParams().Set("other", 3)), Is.True);
			}
		}

		[Test]
		public void CoalescedPlayDoesNotAbandonAnEnterTransition()
		{
			var cue = Cue("coalesced-transition", CuePriority.Award, 0, 255);
			cue.CoalesceKey = "jackpot";
			cue.EnterTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.WipeOn,
				Direction = DmdDirection.Right,
				DurationFrames = 4
			};
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var handle = player.Play(cue.EffectiveId, new DmdParams().Set("value", 1));
				player.Start();
				player.Tick(0d);
				player.Tick(0.1d);

				Assert.That(player.Play(cue.EffectiveId, new DmdParams().Set("value", 2)), Is.EqualTo(handle));
				player.Tick(0.2d);

				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1],
					Is.EqualTo(new byte[] { 15, 15, 0, 0, 15, 15, 0, 0 }));
			}
		}

		[Test]
		public void QueuedPlayDoesNotAbandonExitOrFinishOutgoingEarly()
		{
			var outgoing = Cue("outgoing", CuePriority.Mode, 0, 255);
			outgoing.ExitTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack,
				DurationFrames = 4
			};
			var successor = Cue("successor", CuePriority.Status, 0, 128);
			var queued = Cue("queued-during-exit", CuePriority.Base, 0, 64, CueInterruptPolicy.Queue);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var outgoingHandle = player.Play(outgoing.EffectiveId);
				player.Play(successor.EffectiveId);
				player.Start();
				player.Tick(0d);
				var finished = new List<CueHandle>();
				player.OnCueFinished += (_, handle) => finished.Add(handle);

				Assert.That(player.StopCue(outgoingHandle), Is.True);
				player.Play(queued.EffectiveId);
				Assert.That(finished, Is.Empty);
				player.Tick(0.1d);
				Assert.That(finished, Is.Empty);
				player.Tick(0.4d);

				Assert.That(finished, Is.EqualTo(new[] { outgoingHandle }));
			}
		}

		[Test]
		public void ShortSuccessorStillFinishesOnTimeBehindALongerOutgoingExit()
		{
			var outgoing = Cue("long-exit", CuePriority.Mode, 0, 255);
			outgoing.ExitTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack,
				DurationFrames = 4
			};
			var shortSuccessor = Cue("short-successor", CuePriority.Status, 2, 128);
			var final = Cue("final", CuePriority.Base, 0, 64);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var outgoingHandle = player.Play(outgoing.EffectiveId);
				var shortHandle = player.Play(shortSuccessor.EffectiveId);
				player.Play(final.EffectiveId);
				player.Start();
				player.Tick(0d);
				var finished = new List<CueHandle>();
				player.OnCueFinished += (_, handle) => finished.Add(handle);

				player.StopCue(outgoingHandle);
				player.Tick(0.1d);
				Assert.That(finished, Is.Empty);
				player.Tick(0.2d);
				Assert.That(finished, Is.EqualTo(new[] { shortHandle }));
				player.Tick(0.4d);
				Assert.That(finished, Is.EqualTo(new[] { shortHandle, outgoingHandle }));
			}
		}

		[Test]
		public void ExitThatConsumesTheWholeFiniteLifetimeStartsAtFrameZero()
		{
			var cue = Cue("whole-life-exit", CuePriority.Award, 4, 255);
			cue.ExitTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack,
				DurationFrames = 4
			};
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var finished = 0;
				player.OnCueFinished += (_, __) => finished++;
				player.Play(cue.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.Tick(0.3d);
				Assert.That(finished, Is.Zero);
				player.Tick(0.4d);
				Assert.That(finished, Is.EqualTo(1));
			}
		}

		[Test]
		public void SuppressedExitUsesSuccessorEnterAfterOwningTransitionHasFinished()
		{
			var outgoing = Cue("owning-exit", CuePriority.Mode, 0, 255);
			outgoing.ExitTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack,
				DurationFrames = 2
			};
			var suppressed = Cue("suppressed-exit", CuePriority.Status, 3, 128);
			suppressed.ExitTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.FadeThroughBlack,
				DurationFrames = 2
			};
			var final = Cue("enter-after-suppressed", CuePriority.Base, 0, 64);
			final.EnterTransition = new DmdTransitionSpec {
				Type = DmdTransitionType.WipeOn,
				Direction = DmdDirection.Right,
				DurationFrames = 2
			};
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var outgoingHandle = player.Play(outgoing.EffectiveId);
				player.Play(suppressed.EffectiveId);
				player.Play(final.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.StopCue(outgoingHandle);
				player.Tick(0.2d);

				player.Tick(0.3d);

				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1],
					Is.EqualTo(new byte[] { 8, 8, 8, 8, 8, 8, 8, 8 }));
				player.Tick(0.5d);
				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1],
					Is.EqualTo(new byte[] { 4, 4, 4, 4, 4, 4, 4, 4 }));
			}
		}

		[Test]
		public void AnimatedTicksReuseDisplayFrameAndDataIdentities()
		{
			var cue = Cue("animated", CuePriority.Award, 0, 255);
			cue.Layers[0].Tracks.Add(new DmdPropertyTrack {
				Property = DmdAnimatableProperty.X,
				Keys = {
					new DmdKeyframe { Frame = 0, Value = 0 },
					new DmdKeyframe { Frame = 10, Value = 1, Interp = DmdInterpolation.Linear }
				}
			});
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.Play(cue.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.Tick(0.1d);
				player.Tick(0.2d);

				Assert.That(_sink.Frames, Has.Count.EqualTo(3));
				Assert.That(_sink.Frames[1], Is.SameAs(_sink.Frames[0]));
				Assert.That(_sink.Frames[2].Data, Is.SameAs(_sink.Frames[0].Data));
			}
		}

		[Test]
		public void FormatRequestsRespectColorModeAndRebuildOnlyWhenAccepted()
		{
			Cue("base", CuePriority.Base, 0, 255);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.RequestFormat(DisplayFrameFormat.Dmd2);
				player.RequestFormat(DisplayFrameFormat.Dmd8);
				player.Start();
				Assert.That(_sink.Frames[0].Format, Is.EqualTo(DisplayFrameFormat.Dmd8));
			}
		}

		[Test]
		public void RgbProjectIgnoresAllMonoFormatRequests()
		{
			_project.ColorMode = DmdColorMode.Rgb24;
			Cue("rgb", CuePriority.Base, 0, 255);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.RequestFormat(DisplayFrameFormat.Dmd2);
				player.RequestFormat(DisplayFrameFormat.Dmd4);
				player.RequestFormat(DisplayFrameFormat.Dmd8);
				player.Start();

				Assert.That(_sink.Frames[0].Format, Is.EqualTo(DisplayFrameFormat.Dmd24));
			}
		}

		[Test]
		public void TickReannouncesOnceAfterDelay()
		{
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.Start();
				player.Tick(10d);
				player.Tick(11.99d);
				player.Tick(12d);
				player.Tick(20d);

				Assert.That(_sink.Requests, Has.Count.EqualTo(2));
				Assert.That(_sink.Frames, Has.Count.EqualTo(2));
			}
		}

		[TestCase(true, 1)]
		[TestCase(false, 0)]
		public void LoopAndZeroDurationCuesRemainActiveUntilExplicitlyStopped(bool loop, int duration)
		{
			var cue = Cue("indefinite", CuePriority.Award, duration, 255);
			cue.Loop = loop;
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var finished = 0;
				player.OnCueFinished += (_, __) => finished++;
				var handle = player.Play(cue.EffectiveId);
				player.Start();
				player.Tick(0d);
				player.Tick(10d);
				Assert.That(finished, Is.Zero);
				Assert.That(player.StopCue(handle), Is.True);
				Assert.That(finished, Is.EqualTo(1));
			}
		}

		[Test]
		public void StopAllFinishesWaitingWorkThenActiveAndReturnsToBase()
		{
			var baseCue = Cue("base", CuePriority.Base, 0, 32);
			var held = Cue("held", CuePriority.Status, 0, 64);
			var active = Cue("active", CuePriority.Mode, 0, 128);
			var queued = Cue("queued", CuePriority.Status, 0, 255, CueInterruptPolicy.Queue);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				player.SetBase(baseCue.EffectiveId);
				player.Play(held.EffectiveId);
				player.Play(active.EffectiveId);
				player.Play(queued.EffectiveId);
				var finished = new List<CueHandle>();
				player.OnCueFinished += (_, handle) => finished.Add(handle);
				player.Start();

				player.StopAll();
				player.Tick(0d);
				player.Tick(0.1d);

				Assert.That(finished, Has.Count.EqualTo(3));
				Assert.That(_sink.Snapshots[_sink.Snapshots.Count - 1][0], Is.EqualTo(2));
			}
		}

		[Test]
		public void EarlierTickTimeResetsOriginWithoutRepeatingFinishedEvents()
		{
			var cue = Cue("finite", CuePriority.Award, 1, 255);
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var finished = 0;
				player.OnCueFinished += (_, __) => finished++;
				player.Play(cue.EffectiveId);
				player.Start();
				player.Tick(5d);
				player.Tick(5.1d);
				player.Tick(1d);
				player.Tick(1.1d);

				Assert.That(finished, Is.EqualTo(1));
			}
		}

		[Test]
		public void AnimatedFiveLayerTickAndEmissionAllocateNothingAcrossThreeHundredFrames()
		{
			_project.FrameRate = 30;
			var cue = PerformanceCue(out var sprite, out var font);
			var sink = new AllocationSink();
			try {
				using (var player = new DmdCuePlayer(_project, sink)) {
					player.Play(cue.EffectiveId, new DmdParams().Set("score", 100));
					player.Start();
					player.Tick(0d);
					player.Tick(1d / 30d);
					var before = GC.GetAllocatedBytesForCurrentThread();
					for (var tick = 2; tick <= 301; tick++) {
						player.Tick(tick / 30d);
					}
					var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

					Assert.That(allocated, Is.Zero);
				}
			} finally {
				Object.DestroyImmediate(font);
				Object.DestroyImmediate(sprite);
			}
		}

		[TestCase(128, 32, 0.25d)]
		[TestCase(192, 64, 1.0d)]
		public void AnimatedFiveLayerComposeAndEmitMeetsPerformanceBudget(int width, int height,
			double targetMilliseconds)
		{
			_project.Width = width;
			_project.Height = height;
			_project.FrameRate = 30;
			var cue = PerformanceCue(out var sprite, out var font);
			try {
				using (var player = new DmdCuePlayer(_project, new AllocationSink())) {
					player.Play(cue.EffectiveId, new DmdParams().Set("score", 100));
					player.Start();
					player.Tick(0d);
					for (var tick = 1; tick <= 30; tick++) player.Tick(tick / 30d);

					var stopwatch = Stopwatch.StartNew();
					for (var tick = 31; tick <= 330; tick++) player.Tick(tick / 30d);
					stopwatch.Stop();
					var milliseconds = stopwatch.Elapsed.TotalMilliseconds / 300d;
					var measurement = $"DMD Studio {width}x{height}, 5 layers: {milliseconds:F4} ms/frame " +
					                  $"(target {targetMilliseconds:F2} ms, CI gate {targetMilliseconds * 4d:F2} ms)";
					TestContext.Progress.WriteLine(measurement);
					UnityEngine.Debug.Log(measurement);

					Assert.That(milliseconds, Is.LessThanOrEqualTo(targetMilliseconds * 4d));
				}
			} finally {
				Object.DestroyImmediate(font);
				Object.DestroyImmediate(sprite);
			}
		}

		[Test]
		public void InvalidTimeAndDisposedCallsFollowThePublicContract()
		{
			var player = new DmdCuePlayer(_project, _sink);
			Assert.Throws<ArgumentOutOfRangeException>(() => player.Tick(double.NaN));
			player.Dispose();
			player.Dispose();
			Assert.Throws<ObjectDisposedException>(() => player.Start());
		}

		[Test]
		public void FinishedHandlerMayDisposeWithoutBreakingTheCompletingTick()
		{
			var cue = Cue("dispose-handler", CuePriority.Award, 1, 255);
			var player = new DmdCuePlayer(_project, _sink);
			player.OnCueFinished += (_, __) => player.Dispose();
			player.Play(cue.EffectiveId);
			player.Start();
			player.Tick(0d);

			Assert.DoesNotThrow(() => player.Tick(0.1d));
			Assert.That(_sink.Clears, Is.EqualTo(new[] { "studio" }));
		}

		[Test]
		public void ValidationHandlerMayDisposeAfterTheDiagnosticFrameIsEmitted()
		{
			var font = DmdTestFont.Create();
			try {
				_project.Fonts.Add(font);
				var cue = Cue("validation-handler", CuePriority.Award, 0, 0);
				cue.Layers.Add(new TextLayer { Font = font, Text = "{missing}" });
				var player = new DmdCuePlayer(_project, _sink);
				player.OnValidationError += (_, __) => player.Dispose();
				player.Play(cue.EffectiveId);

				Assert.DoesNotThrow(() => player.Start());
				Assert.That(_sink.Frames, Has.Count.EqualTo(1));
				Assert.That(_sink.Clears, Is.EqualTo(new[] { "studio" }));
			} finally {
				Object.DestroyImmediate(font);
			}
		}

		[Test]
		public void UnknownCueReportsOnceAndLeavesBaseUnchanged()
		{
			using (var player = new DmdCuePlayer(_project, _sink)) {
				var errors = new List<string>();
				player.OnValidationError += (_, error) => errors.Add(error);
				player.SetBase("missing");
				Assert.That(player.Play("missing").IsValid, Is.False);
				Assert.That(errors, Has.Count.EqualTo(1));
			}
		}

		private DmdCueAsset Cue(string id, CuePriority priority, int duration, byte intensity,
			CueInterruptPolicy interrupt = CueInterruptPolicy.Replace)
		{
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			cue.CueId = id;
			cue.Priority = priority;
			cue.Interrupt = interrupt;
			cue.DurationFrames = duration;
			cue.Layers.Add(new ShapeLayer {
				Shape = DmdShapeType.Rect,
				Width = _project.Width,
				Height = _project.Height,
				Filled = true,
				Shade = new DmdShade { Intensity = intensity }
			});
			_project.Cues.Add(cue);
			_cues.Add(cue);
			return cue;
		}

		private DmdCueAsset PerformanceCue(out DmdSpriteAsset sprite, out DmdFontAsset font)
		{
			var cue = Cue("performance", CuePriority.Award, 300, 48);
			cue.Loop = true;
			cue.Layers[0].Tracks.Add(new DmdPropertyTrack {
				Property = DmdAnimatableProperty.X,
				Keys = {
					new DmdKeyframe { Frame = 0, Value = 0, Interp = DmdInterpolation.Linear },
					new DmdKeyframe { Frame = 299, Value = 4 }
				}
			});
			cue.Layers.Add(new ShapeLayer {
				Shape = DmdShapeType.Rect, X = 2, Y = 2, Width = _project.Width / 2,
				Height = _project.Height / 2, Filled = true, Shade = new DmdShade { Intensity = 96 },
				Tracks = { new DmdPropertyTrack {
					Property = DmdAnimatableProperty.Opacity,
					Keys = {
						new DmdKeyframe { Frame = 0, Value = 0.25f, Interp = DmdInterpolation.Linear },
						new DmdKeyframe { Frame = 299, Value = 1f }
					}
				} }
			});
			sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			sprite.Frames.Add(new DmdBitmapData { Width = 2, Height = 1, Pixels = new byte[] { 0, 255 } });
			sprite.Frames.Add(new DmdBitmapData { Width = 2, Height = 1, Pixels = new byte[] { 255, 0 } });
			sprite.FrameDurations.AddRange(new[] { 1, 1 });
			_project.Sprites.Add(sprite);
			cue.Layers.Add(new BitmapLayer { Sprite = sprite, Loop = DmdLoopMode.Loop, X = 4, Y = 4 });
			font = DmdTestFont.Create();
			_project.Fonts.Add(font);
			cue.Layers.Add(new TextLayer { Font = font, Text = "A{score:0}", X = 8, Y = 8 });
			cue.Layers.Add(new NumberLayer {
				Font = font, ParamName = "score", Format = "0", CountUpSeconds = 1f, X = 8, Y = 16
			});
			return cue;
		}

		private sealed class RecordingSink : IDmdFrameSink
		{
			public readonly List<RequestedDisplays> Requests = new List<RequestedDisplays>();
			public readonly List<DisplayFrameData> Frames = new List<DisplayFrameData>();
			public readonly List<byte[]> Snapshots = new List<byte[]>();
			public readonly List<string> Clears = new List<string>();

			public void RequestDisplays(RequestedDisplays displays) => Requests.Add(displays);

			public void UpdateFrame(DisplayFrameData frame)
			{
				Frames.Add(frame);
				Snapshots.Add((byte[])frame.Data.Clone());
			}

			public void Clear(string displayId) => Clears.Add(displayId);
		}

		private sealed class AllocationSink : IDmdFrameSink
		{
			public void RequestDisplays(RequestedDisplays displays) { }
			public void UpdateFrame(DisplayFrameData frame) { }
			public void Clear(string displayId) { }
		}
	}
}
