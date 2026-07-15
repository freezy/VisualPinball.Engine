// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal static class SpriteFrameClock
	{
		public static int TotalDuration(DmdSpriteAsset sprite)
		{
			if (sprite?.Frames == null) {
				return 0;
			}
			var duration = 0;
			for (var index = 0; index < sprite.Frames.Count; index++) {
				duration += Duration(sprite, index);
			}
			return duration;
		}

		public static bool TryGetFrame(DmdSpriteAsset sprite, int elapsedFrames, DmdLoopMode loop,
			int startFrame, out int frameIndex)
		{
			frameIndex = 0;
			if (sprite?.Frames == null || sprite.Frames.Count == 0 || elapsedFrames < 0) {
				return false;
			}

			var count = sprite.Frames.Count;
			startFrame = math.clamp(startFrame, 0, count - 1);
			if (count == 1) {
				if (loop == DmdLoopMode.Once && elapsedFrames >= Duration(sprite, 0)) {
					return false;
				}
				frameIndex = 0;
				return true;
			}

			switch (loop) {
				case DmdLoopMode.Once:
				case DmdLoopMode.HoldLast:
					for (var index = startFrame; index < count; index++) {
						var duration = Duration(sprite, index);
						if (elapsedFrames < duration) {
							frameIndex = index;
							return true;
						}
						elapsedFrames -= duration;
					}
					frameIndex = count - 1;
					return loop == DmdLoopMode.HoldLast;
				case DmdLoopMode.Loop:
					return TryResolveCycle(sprite, elapsedFrames, startFrame, count, false, out frameIndex);
				case DmdLoopMode.PingPong:
					return TryResolveCycle(sprite, elapsedFrames, startFrame, count, true, out frameIndex);
				default:
					return false;
			}
		}

		private static bool TryResolveCycle(DmdSpriteAsset sprite, int elapsedFrames, int startFrame,
			int count, bool pingPong, out int frameIndex)
		{
			var sequenceCount = pingPong ? count * 2 - 2 : count;
			var cycleDuration = 0;
			for (var sequence = 0; sequence < sequenceCount; sequence++) {
				cycleDuration += Duration(sprite, SequenceFrame(sequence, startFrame, count, pingPong));
			}
			if (cycleDuration <= 0) {
				frameIndex = startFrame;
				return true;
			}
			elapsedFrames %= cycleDuration;
			for (var sequence = 0; sequence < sequenceCount; sequence++) {
				frameIndex = SequenceFrame(sequence, startFrame, count, pingPong);
				var duration = Duration(sprite, frameIndex);
				if (elapsedFrames < duration) {
					return true;
				}
				elapsedFrames -= duration;
			}
			frameIndex = startFrame;
			return true;
		}

		private static int SequenceFrame(int sequence, int startFrame, int count, bool pingPong)
		{
			var logical = pingPong && sequence >= count ? count * 2 - 2 - sequence : sequence;
			return (startFrame + logical) % count;
		}

		private static int Duration(DmdSpriteAsset sprite, int index)
		{
			return sprite.FrameDurations != null && index < sprite.FrameDurations.Count
				? math.max(1, sprite.FrameDurations[index])
				: 1;
		}
	}
}
