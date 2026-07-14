// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class GleDisplayEmitterTests
	{
		[Test]
		public void ForwardsEachCallWithoutReplacingPayloads()
		{
			var calls = new List<string>();
			RequestedDisplays receivedDisplays = null;
			DisplayFrameData receivedFrame = null;
			string receivedClear = null;
			var emitter = new GleDisplayEmitter(
				displays => {
					calls.Add("request");
					receivedDisplays = displays;
				},
				frame => {
					calls.Add("frame");
					receivedFrame = frame;
				},
				id => {
					calls.Add("clear");
					receivedClear = id;
				});
			var displays = new RequestedDisplays(new DisplayConfig("studio", 140, 36));
			var frame = new DisplayFrameData("studio", DisplayFrameFormat.Dmd8, new byte[140 * 36]);

			emitter.RequestDisplays(displays);
			emitter.UpdateFrame(frame);
			emitter.Clear("studio");

			Assert.That(calls, Is.EqualTo(new[] { "request", "frame", "clear" }));
			Assert.That(receivedDisplays, Is.SameAs(displays));
			Assert.That(receivedFrame, Is.SameAs(frame));
			Assert.That(receivedClear, Is.EqualTo("studio"));
		}

		[Test]
		public void RequiresAllRaiseDelegates()
		{
			Assert.That(() => new GleDisplayEmitter(null, _ => { }, _ => { }),
				Throws.ArgumentNullException.With.Property("ParamName").EqualTo("raiseDisplaysRequested"));
			Assert.That(() => new GleDisplayEmitter(_ => { }, null, _ => { }),
				Throws.ArgumentNullException.With.Property("ParamName").EqualTo("raiseDisplayUpdateFrame"));
			Assert.That(() => new GleDisplayEmitter(_ => { }, _ => { }, null),
				Throws.ArgumentNullException.With.Property("ParamName").EqualTo("raiseDisplayClear"));
		}
	}
}
