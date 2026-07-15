// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class CueSchedulerTests
	{
		private readonly List<DmdCueAsset> _assets = new List<DmdCueAsset>();
		private CueScheduler _scheduler;

		[SetUp]
		public void SetUp() => _scheduler = new CueScheduler();

		[TearDown]
		public void TearDown()
		{
			foreach (var asset in _assets) {
				Object.DestroyImmediate(asset);
			}
			_assets.Clear();
		}

		[Test]
		public void Row1CoalesceMergesBeforeAdmissionAndReturnsExistingInstance()
		{
			var cue = Cue("award", CuePriority.Award, key: "jackpot");
			var active = Create(cue, new DmdParams().Set("score", 10));
			_scheduler.Admit(active);

			var result = _scheduler.Admit(Create(cue, new DmdParams().Set("score", 20)));

			Assert.That(result.Kind, Is.EqualTo(CueAdmissionKind.Coalesced));
			Assert.That(result.Instance, Is.SameAs(active));
			Assert.That(active.Params.TryGet("score", out var value) && value.IntValue == 20, Is.True);
		}

		[Test]
		public void Row2QueuePolicyNeverInterruptsEvenAtHigherPriority()
		{
			var active = Create(Cue("status", CuePriority.Status));
			_scheduler.Admit(active);
			var incoming = Create(Cue("system", CuePriority.System, CueInterruptPolicy.Queue));

			var result = _scheduler.Admit(incoming);

			Assert.That(result.Kind, Is.EqualTo(CueAdmissionKind.Queued));
			Assert.That(_scheduler.Active, Is.SameAs(active));
		}

		[Test]
		public void Row3HigherPriorityPreemptsInterruptibleActive()
		{
			var active = Create(Cue("status", CuePriority.Status));
			_scheduler.Admit(active);
			var incoming = Create(Cue("mode", CuePriority.Mode));

			var result = _scheduler.Admit(incoming);

			Assert.That(result.Kind, Is.EqualTo(CueAdmissionKind.Preempted));
			Assert.That(_scheduler.Active, Is.SameAs(incoming));
			Assert.That(_scheduler.HoldStack, Does.Contain(active));
		}

		[Test]
		public void Row3SystemPreemptsNonInterruptibleActive()
		{
			var active = Create(Cue("mode", CuePriority.Mode, CueInterruptPolicy.NonInterruptible));
			_scheduler.Admit(active);
			var incoming = Create(Cue("tilt", CuePriority.System));

			Assert.That(_scheduler.Admit(incoming).Kind, Is.EqualTo(CueAdmissionKind.Preempted));
		}

		[Test]
		public void Row4NonInterruptibleActiveQueuesHigherNonSystemCue()
		{
			var active = Create(Cue("mode", CuePriority.Mode, CueInterruptPolicy.NonInterruptible));
			_scheduler.Admit(active);
			var incoming = Create(Cue("critical", CuePriority.Critical));

			Assert.That(_scheduler.Admit(incoming).Kind, Is.EqualTo(CueAdmissionKind.Queued));
			Assert.That(_scheduler.Active, Is.SameAs(active));
		}

		[Test]
		public void Row5EqualReplaceDiscardsInterruptibleActive()
		{
			var active = Create(Cue("one", CuePriority.Award));
			_scheduler.Admit(active);
			var incoming = Create(Cue("two", CuePriority.Award));

			var result = _scheduler.Admit(incoming);

			Assert.That(result.Kind, Is.EqualTo(CueAdmissionKind.Replaced));
			Assert.That(result.Displaced, Is.SameAs(active));
			Assert.That(_scheduler.TryFind(active.Handle, out _), Is.False);
		}

		[TestCase(CueInterruptPolicy.NonInterruptible, CueInterruptPolicy.Replace)]
		[TestCase(CueInterruptPolicy.Replace, CueInterruptPolicy.NonInterruptible)]
		public void Row6EqualNonInterruptibleCombinationQueues(CueInterruptPolicy activePolicy,
			CueInterruptPolicy incomingPolicy)
		{
			var active = Create(Cue("one", CuePriority.Award, activePolicy));
			_scheduler.Admit(active);
			var incoming = Create(Cue("two", CuePriority.Award, incomingPolicy));

			Assert.That(_scheduler.Admit(incoming).Kind, Is.EqualTo(CueAdmissionKind.Queued));
		}

		[Test]
		public void Row7LowerPriorityQueues()
		{
			var active = Create(Cue("mode", CuePriority.Mode));
			_scheduler.Admit(active);

			Assert.That(_scheduler.Admit(Create(Cue("status", CuePriority.Status))).Kind,
				Is.EqualTo(CueAdmissionKind.Queued));
		}

		[Test]
		public void DuplicateCueIdsRemainPreciselyAddressableByHandle()
		{
			var cue = Cue("award", CuePriority.Award, CueInterruptPolicy.Queue);
			var first = Create(cue);
			var second = Create(cue);
			_scheduler.Admit(first);
			_scheduler.Admit(second);

			Assert.That(first.Handle, Is.Not.EqualTo(second.Handle));
			Assert.That(_scheduler.TryFind(first.Handle, out var foundFirst) && foundFirst == first, Is.True);
			Assert.That(_scheduler.TryFind(second.Handle, out var foundSecond) && foundSecond == second, Is.True);
		}

		[Test]
		public void StringResolutionChecksCoalesceKeyBeforeCueId()
		{
			var active = Create(Cue("shared", CuePriority.Status, key: "active-key"));
			_scheduler.Admit(active);
			var queuedByKey = Create(Cue("other", CuePriority.Status, CueInterruptPolicy.Queue, "shared"));
			_scheduler.Admit(queuedByKey);

			Assert.That(_scheduler.Resolve("shared"), Is.SameAs(queuedByKey));
		}

		[Test]
		public void StringResolutionUsesActiveThenStackTopDownThenQueueOrderWithinEachMatchKind()
		{
			var heldBottom = Create(Cue("held-bottom", CuePriority.Status));
			_scheduler.Admit(heldBottom);
			var heldTop = Create(Cue("held-top", CuePriority.Award));
			_scheduler.Admit(heldTop);
			var active = Create(Cue("active", CuePriority.Mode));
			_scheduler.Admit(active);
			heldBottom.Cue.CoalesceKey = "lane";
			heldTop.Cue.CoalesceKey = "lane";
			active.Cue.CoalesceKey = "lane";
			var queuedFirst = Create(Cue("same-id", CuePriority.Status, CueInterruptPolicy.Queue));
			var queuedSecond = Create(Cue("same-id", CuePriority.Status, CueInterruptPolicy.Queue));
			_scheduler.Admit(queuedFirst);
			_scheduler.Admit(queuedSecond);

			Assert.That(_scheduler.Resolve("lane"), Is.SameAs(active));
			active.Cue.CoalesceKey = null;
			Assert.That(_scheduler.Resolve("lane"), Is.SameAs(heldTop));
			heldTop.Cue.CoalesceKey = null;
			Assert.That(_scheduler.Resolve("lane"), Is.SameAs(heldBottom));
			heldBottom.Cue.CoalesceKey = null;
			Assert.That(_scheduler.Resolve("same-id"), Is.SameAs(queuedFirst));
		}

		[Test]
		public void QueuePreservesFifoOrderWithinTheSamePriority()
		{
			_scheduler.Admit(Create(Cue("active", CuePriority.Mode)));
			var first = Create(Cue("first", CuePriority.Status, CueInterruptPolicy.Queue));
			var second = Create(Cue("second", CuePriority.Status, CueInterruptPolicy.Queue));
			var third = Create(Cue("third", CuePriority.Status, CueInterruptPolicy.Queue));
			_scheduler.Admit(first);
			_scheduler.Admit(second);
			_scheduler.Admit(third);

			Assert.That(_scheduler.Queue, Is.EqualTo(new[] { first, second, third }));
		}

		[Test]
		public void DrainChoosesHigherPriorityBetweenHoldTopAndQueueHeadThenHoldOnTie()
		{
			var held = Create(Cue("held", CuePriority.Status));
			_scheduler.Admit(held);
			var system = Create(Cue("system", CuePriority.System));
			_scheduler.Admit(system);
			var award = Create(Cue("award", CuePriority.Award, CueInterruptPolicy.Queue));
			_scheduler.Admit(award);

			Assert.That(_scheduler.EndActive(), Is.SameAs(award));
			Assert.That(_scheduler.EndActive(), Is.SameAs(held));
		}

		[Test]
		public void RestartReturnPolicyResetsTimelineAndInstanceState()
		{
			var held = Create(Cue("held", CuePriority.Status, returnPolicy: CueReturnPolicy.Restart));
			held.ElapsedFrames = 42;
			held.State.EnsureLayerCount(1);
			held.State.NumberTweens[0].Initialized = true;
			_scheduler.Admit(held);
			_scheduler.Admit(Create(Cue("system", CuePriority.System)));

			Assert.That(_scheduler.EndActive(), Is.SameAs(held));
			Assert.That(held.ElapsedFrames, Is.Zero);
			Assert.That(held.State.NumberTweens, Is.Empty);
		}

		[Test]
		public void DiscardReturnPolicyIsDroppedDuringDrain()
		{
			var held = Create(Cue("held", CuePriority.Status, returnPolicy: CueReturnPolicy.Discard));
			_scheduler.Admit(held);
			_scheduler.Admit(Create(Cue("system", CuePriority.System)));

			Assert.That(_scheduler.EndActive(), Is.Null);
			Assert.That(_scheduler.TryFind(held.Handle, out _), Is.False);
		}

		private CueInstance Create(DmdCueAsset cue, DmdParams parameters = null)
		{
			return _scheduler.Create(cue, parameters ?? new DmdParams());
		}

		private DmdCueAsset Cue(string id, CuePriority priority,
			CueInterruptPolicy interrupt = CueInterruptPolicy.Replace, string key = null,
			CueReturnPolicy returnPolicy = CueReturnPolicy.Resume)
		{
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			cue.CueId = id;
			cue.Priority = priority;
			cue.Interrupt = interrupt;
			cue.CoalesceKey = key;
			cue.Return = returnPolicy;
			_assets.Add(cue);
			return cue;
		}
	}
}
