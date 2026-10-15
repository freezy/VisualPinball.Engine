// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using NLog;
using NLog.Targets;
using UnityEngine;
using ILogger = UnityEngine.ILogger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Writes to Unity's console (Editor.log / Player.log).
	/// </summary>
	///
	/// <remarks>
	/// Exceptions are rendered into the message text by the layout. This target never calls
	/// <c>Debug.LogException</c>: Unity treats that as an unhandled exception, and error
	/// reporting would then report every logged exception, warnings included.
	/// </remarks>
	[Target("Unity")]
	internal class UnityTarget : TargetWithLayout
	{
		/// <summary>
		/// True while this target writes on the calling thread, so the bridge that forwards
		/// Unity's console into NLog can skip these lines.
		/// </summary>
		[ThreadStatic]
		internal static bool IsWriting;

		private static readonly ILogger Logger = Debug.unityLogger;
		private const string Tag = "VisualPinball";

		protected override void Write(LogEventInfo logEvent)
		{
			var msg = RenderLogEvent(Layout, logEvent);
			IsWriting = true;
			try {
				if (logEvent.Level >= LogLevel.Error) {
					Logger.LogError(Tag, msg);

				} else if (logEvent.Level >= LogLevel.Warn) {
					Logger.LogWarning(Tag, msg);

				} else {
					Logger.Log(Tag, msg);
				}
			} finally {
				IsWriting = false;
			}
		}
	}
}
