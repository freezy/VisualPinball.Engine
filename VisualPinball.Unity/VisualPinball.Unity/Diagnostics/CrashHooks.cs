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
using System.Threading.Tasks;
using NLog;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Logs exceptions nothing else caught. These hooks only log: capturing them for error
	/// reports is the reporting SDK's job, so their events are flagged with
	/// <see cref="VpeLog.HookProperty"/>.
	/// </summary>
	internal static class CrashHooks
	{
		private static readonly Logger Logger = LogManager.GetLogger("VisualPinball.Unity.CrashHooks");
		private static bool _installed;

		public static void Install()
		{
			if (_installed) {
				return;
			}
			_installed = true;
			AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		}

		private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var level = e.IsTerminating ? LogLevel.Fatal : LogLevel.Error;
			Log(level, e.ExceptionObject as Exception, "Unhandled exception ({ExceptionObject})", e.ExceptionObject?.GetType().FullName);
			VpeLog.Flush(TimeSpan.FromSeconds(2));
		}

		private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Log(LogLevel.Error, e.Exception, "Unobserved task exception");
			e.SetObserved();
		}

		private static void Log(LogLevel level, Exception exception, string message, params object[] args)
		{
			var logEvent = LogEventInfo.Create(level, Logger.Name, exception, null, message, args);
			logEvent.Properties[VpeLog.HookProperty] = true;
			Logger.Log(logEvent);
		}
	}
}
