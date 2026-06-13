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
using System.Threading;
using System.Threading.Tasks;
using NLog;
using UnityEditor;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Drives a <see cref="PackageWriter"/> export from the editor: shows a cancelable progress bar and
	/// keeps the editor responsive. The writer yields between stages and offloads the heavy texture
	/// byte-load to worker threads; this runner routes the writer's (possibly worker-thread) progress
	/// reports back to the main thread via <see cref="Progress{T}"/>, so the UI calls below are legal.
	/// </summary>
	public static class VpeExportRunner
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Runs <paramref name="writer"/> against <paramref name="path"/> without blocking the editor.
		/// Returns true on success, false if the user canceled or the export failed (the failure is
		/// logged and surfaced in a dialog). Must be called from the main thread.
		/// </summary>
		public static async Task<bool> RunAsync(PackageWriter writer, string path, string title = "Exporting Table")
		{
			using var cts = new CancellationTokenSource();

			// Progress<T> captures the current (main-thread) SynchronizationContext at construction, so
			// this handler runs on the main thread even though the writer reports from worker threads —
			// which is what makes the EditorUtility calls here safe.
			//
			// It also delivers reports *asynchronously* (SynchronizationContext.Post), so a report can
			// land AFTER the export has returned and we've cleared the bar — re-opening it and leaving it
			// stuck showing "Done.". `completed` (read/written only on the main thread) makes every late
			// report a no-op, so the ClearProgressBar() in finally always wins, whatever the post order.
			var completed = false;
			var progress = new Progress<ExportProgress>(p => {
				if (completed) {
					return;
				}
				if (EditorUtility.DisplayCancelableProgressBar(title, p.Message, p.Fraction)) {
					cts.Cancel();
				}
			});

			try {
				await writer.WritePackageAsync(path, progress, cts.Token);
				return true;

			} catch (OperationCanceledException) {
				Logger.Info("Export canceled by user.");
				return false;

			} catch (Exception ex) {
				Logger.Error(ex, "Export failed.");
				Debug.LogException(ex);
				EditorUtility.DisplayDialog(title, $"Export failed:\n{ex.Message}", "OK");
				return false;

			} finally {
				completed = true;
				EditorUtility.ClearProgressBar();
			}
		}
	}
}
