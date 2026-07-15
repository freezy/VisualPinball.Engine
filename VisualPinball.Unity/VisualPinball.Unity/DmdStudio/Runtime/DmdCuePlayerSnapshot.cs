// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Read-only scheduler state intended for authoring tools and diagnostics.
	/// </summary>
	public readonly struct DmdCuePlayerSnapshot
	{
		private readonly string[] _holdStackCueIds;
		private readonly string[] _queuedCueIds;

		public string BaseCueId { get; }
		public string ActiveCueId { get; }
		public string[] HoldStackCueIds => _holdStackCueIds ?? Array.Empty<string>();
		public string[] QueuedCueIds => _queuedCueIds ?? Array.Empty<string>();

		internal DmdCuePlayerSnapshot(string baseCueId, string activeCueId, string[] holdStackCueIds,
			string[] queuedCueIds)
		{
			BaseCueId = baseCueId;
			ActiveCueId = activeCueId;
			_holdStackCueIds = holdStackCueIds ?? Array.Empty<string>();
			_queuedCueIds = queuedCueIds ?? Array.Empty<string>();
		}
	}
}
