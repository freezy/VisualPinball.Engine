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
using System.Diagnostics;
using NLog;
using UnityEngine;
using VisualPinball.Plugins;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Where the time goes between launching the player and its first frame with a usable
	/// menu.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// The timeline starts when the OS created the process, so the time before the first
	/// managed code runs (Unity's engine init, the graphics device, and on Linux the preload
	/// of every native plugin) shows up as the first interval. After that come Unity's
	/// initialization stages, then whatever the host marks (settings, plugins, table list),
	/// until the host says the menu is ready. The first frame rendered after that ends it.
	/// </para>
	/// <para>
	/// When it ends, it logs one line and publishes the <see cref="DiagnosticsContext.Startup"/>
	/// section, so error reports carry it. In the editor it starts with play mode instead.
	/// Main thread only.
	/// </para>
	/// </remarks>
	public static class StartupTimeline
	{
		public const string ProcessStart = "process-start";
		public const string FirstManagedCode = "managed";
		public const string MenuReady = "menu-ready";
		public const string FirstFrame = "first-frame";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static Timeline _timeline;
		private static bool _menuReady;
		private static bool _completed;

		/// <summary>
		/// The timeline so far, or null before the first managed code ran.
		/// </summary>
		public static Timeline Current => _timeline;

		/// <summary>
		/// Whether the menu was ready and its first frame rendered.
		/// </summary>
		public static bool IsComplete => _completed;

		/// <summary>
		/// Raised once start-up is complete, with the finished timeline.
		/// </summary>
		public static event Action<Timeline> Completed;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnSubsystemRegistration()
		{
			// the host may have started it already (see EnsureStarted)
			Mark("subsystem-registration");
		}

#if UNITY_EDITOR
		// With domain reload off, the editor keeps this across play sessions. Reset before the
		// next one starts, rather than in a SubsystemRegistration hook, whose order relative to
		// the host's own hooks isn't defined.
		[UnityEditor.InitializeOnLoadMethod]
		private static void ResetOnPlay()
		{
			UnityEditor.EditorApplication.playModeStateChanged += state => {
				if (state == UnityEditor.PlayModeStateChange.ExitingEditMode) {
					_timeline = null;
					_menuReady = false;
					_completed = false;
				}
			};
		}
#endif

		/// <summary>
		/// Starts the timeline if it isn't yet. The host calls this first thing in its earliest
		/// hook, so its own set-up (logging, config) is measured instead of falling into the
		/// time before managed code.
		/// </summary>
		public static void EnsureStarted() => Begin();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void OnAfterAssembliesLoaded() => Mark("assemblies-loaded");

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
		private static void OnBeforeSplashScreen() => Mark("before-splash");

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void OnBeforeSceneLoad() => Mark("before-scene-load");

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void OnAfterSceneLoad() => Mark("after-scene-load");

		private static void Begin()
		{
			if (_timeline != null) {
				return;
			}
			if (Application.isEditor || !TryGetProcessAge(out var processAge)) {
				_timeline = new Timeline("startup", FirstManagedCode);
				_timeline.Fact("from", Application.isEditor ? "play mode" : "first managed code");
				return;
			}
			var now = Stopwatch.GetTimestamp();
			var ageTicks = (long)(processAge.TotalSeconds * Stopwatch.Frequency);
			_timeline = new Timeline("startup", ProcessStart, now - ageTicks, 0);
			_timeline.Mark(FirstManagedCode);
		}

		/// <summary>
		/// Records that a start-up step finished. Ignored once start-up is complete.
		/// </summary>
		public static void Mark(string milestone)
		{
			if (_completed) {
				return;
			}
			Begin();
			_timeline.Mark(milestone);
		}

		/// <summary>
		/// Records a fact about this start-up, like whether it's the first since boot.
		/// </summary>
		public static void Fact(string key, string value)
		{
			Begin();
			_timeline.Fact(key, value);
			if (_completed) {
				Publish();
			}
		}

		/// <summary>
		/// The host's menu is built and takes input. Start-up ends with the next rendered frame.
		/// </summary>
		public static void SetMenuReady()
		{
			if (_menuReady || _completed) {
				return;
			}
			_menuReady = true;
			Mark(MenuReady);
		}

		/// <summary>
		/// Called at the end of every frame by <see cref="PerformanceMonitor"/>.
		/// </summary>
		internal static void OnFrameEnd()
		{
			if (!_menuReady || _completed || _timeline == null) {
				return;
			}
			Complete(FirstFrame);
		}

		/// <summary>
		/// The host goes straight into a table (e.g. from the command line), so the menu's
		/// first frame would only come after the load: start-up ends here instead, and the load
		/// has its own timeline.
		/// </summary>
		public static void CompleteAtTableLaunch()
		{
			if (_completed) {
				return;
			}
			Begin();
			if (!_menuReady) {
				_menuReady = true;
				_timeline.Mark(MenuReady);
			}
			_timeline.Fact("ends", "at a table launch, before its first frame");
			Complete("table-launch");
		}

		private static void Complete(string milestone)
		{
			_timeline.Mark(milestone);
			_completed = true;
			var preManaged = _timeline.MsTo(FirstManagedCode);
			Logger.Info("Startup: {Total:l} to the menu{PreManaged:l}: {Steps:l}",
				Seconds(_timeline.TotalMs),
				preManaged >= 0 && _timeline.Milestones[0].Name == ProcessStart ? ", " + Seconds(preManaged) + " before managed code" : "",
				_timeline.Format(minMs: 20));
			Publish();
			try {
				Completed?.Invoke(_timeline);
			} catch (Exception e) {
				Logger.Error(e, "A start-up completion handler failed");
			}
		}

		private static void Publish()
		{
			var w = new DiagnosticsWriter();
			_timeline.Describe(w);
			DiagnosticsContext.Set(DiagnosticsContext.Startup, w, log: false);
		}

		private static string Seconds(double ms) => (ms / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";

		private static bool TryGetProcessAge(out TimeSpan age)
		{
			try {
				using var process = Process.GetCurrentProcess();
				age = DateTime.Now - process.StartTime;
				// a clock change or a bogus start time: better no gap than a wrong one
				return age > TimeSpan.Zero && age < TimeSpan.FromMinutes(30);
			} catch (Exception) {
				age = TimeSpan.Zero;
				return false;
			}
		}
	}
}
