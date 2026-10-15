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

using System;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Forwards messages logged through <see cref="UnityEngine.Debug"/> (Unity itself,
	/// third-party code) into NLog, so they end up in the session log and in error reports.
	/// </summary>
	///
	/// <remarks>
	/// Lines that VPE's own console target writes are skipped, and the console target in
	/// turn ignores the "Unity" logger, so nothing goes round in circles. Exceptions are
	/// flagged with <see cref="VpeLog.UnityExceptionProperty"/>: the error reporting SDK
	/// captures them itself, with a proper stack trace.
	/// </remarks>
	internal static class UnityLogBridge
	{
		private static readonly Logger Logger = LogManager.GetLogger(VpeLog.UnityLoggerName);
		private static bool _installed;

		public static void Install()
		{
			if (_installed) {
				return;
			}
			_installed = true;
			Application.logMessageReceivedThreaded += OnLogMessage;
		}

		private static void OnLogMessage(string message, string stackTrace, LogType type)
		{
			if (UnityTarget.IsWriting) {
				return;
			}
			try {
				switch (type) {
					case LogType.Log:
						Logger.Info(message);
						break;
					case LogType.Warning:
						Logger.Warn(message);
						break;
					case LogType.Error:
					case LogType.Assert:
						Logger.Error(message);
						break;
					case LogType.Exception: {
						var e = new LogEventInfo(LogLevel.Error, VpeLog.UnityLoggerName, string.IsNullOrEmpty(stackTrace) ? message : message + "\n" + stackTrace);
						e.Properties[VpeLog.UnityExceptionProperty] = true;
						Logger.Log(e);
						break;
					}
				}
			} catch (Exception) {
				// never throw back into Unity's logger
			}
		}
	}
}
