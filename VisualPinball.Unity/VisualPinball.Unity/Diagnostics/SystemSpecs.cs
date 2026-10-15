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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Plugins;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The hardware and software VPE runs on, collected once per session.
	/// </summary>
	public static class SystemSpecs
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Writes the system section. Main thread only.
		/// </summary>
		public static void Describe(DiagnosticsWriter w)
		{
			w.Add("os", SystemInfo.operatingSystem);
			Try(w, "os.detail", OsDetail);
			w.Add("os.architecture", RuntimeInformation.OSArchitecture.ToString());
			w.Add("process.architecture", RuntimeInformation.ProcessArchitecture.ToString());

			w.Add("cpu", SystemInfo.processorType);
			w.Add("cpu.logical-cores", SystemInfo.processorCount);
			Try(w, "cpu.physical-cores", () => PhysicalCores()?.ToString());
			w.Add("cpu.frequency-mhz", SystemInfo.processorFrequency);
			w.Add("ram-mb", SystemInfo.systemMemorySize);

			w.Add("gpu", SystemInfo.graphicsDeviceName);
			w.Add("gpu.vendor", SystemInfo.graphicsDeviceVendor);
			w.Add("gpu.id", $"{SystemInfo.graphicsDeviceVendorID:x4}:{SystemInfo.graphicsDeviceID:x4}");
			w.Add("gpu.api", SystemInfo.graphicsDeviceVersion);
			Try(w, "gpu.driver", () => GpuDriverVersion(SystemInfo.graphicsDeviceVendorID, SystemInfo.graphicsDeviceID));
			w.Add("gpu.vram-mb", SystemInfo.graphicsMemorySize);
			w.Add("gpu.shader-level", SystemInfo.graphicsShaderLevel);
			w.Add("gpu.ray-tracing", SystemInfo.supportsRayTracing);

			var current = Screen.currentResolution;
			w.Add("screen", $"{current.width}x{current.height} @ {current.refreshRateRatio.value:0.##} Hz, {Screen.fullScreenMode}");
			w.Add("displays", Display.displays.Length);
			for (var i = 0; i < Display.displays.Length; i++) {
				var display = Display.displays[i];
				w.Add($"display.{i}", $"{display.systemWidth}x{display.systemHeight}{(display.active ? ", active" : "")}");
			}

			w.Add("unity", Application.unityVersion);
			w.Add("render-pipeline", GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.GetType().Name : "built-in");
			w.Add("graphics-api", SystemInfo.graphicsDeviceType.ToString());
#if ENABLE_IL2CPP
			w.Add("scripting-backend", "IL2CPP");
#else
			w.Add("scripting-backend", "Mono");
#endif
			w.Add("development-build", Debug.isDebugBuild);
			w.Add("editor", Application.isEditor);
			w.Add("command-line", CommandLine(Environment.GetCommandLineArgs().Skip(1).ToArray(), Application.isEditor));
		}

		/// <summary>
		/// The command line without secrets. In the editor, only VPE's own arguments: the rest
		/// is the Hub's, including access tokens.
		/// </summary>
		internal static string CommandLine(IReadOnlyList<string> args, bool editor)
		{
			var parts = new List<string>();
			for (var i = 0; i < args.Count; i++) {
				var arg = args[i];
				var isOption = arg.StartsWith("-");
				if (editor && !(isOption && arg.TrimStart('-').StartsWith("vpe", StringComparison.OrdinalIgnoreCase))) {
					continue;
				}
				var equals = arg.IndexOf('=');
				var name = (equals > 0 ? arg.Substring(0, equals) : arg).TrimStart('-');
				if (isOption && DiagnosticsWriter.IsSecret(name)) {
					if (equals > 0) {
						parts.Add(arg.Substring(0, equals + 1) + "<redacted>");
					} else {
						parts.Add(arg);
						if (i + 1 < args.Count && !args[i + 1].StartsWith("-")) {
							parts.Add("<redacted>");
							i++;
						}
					}
					continue;
				}
				parts.Add(arg);
				// in the editor, keep the value that belongs to a -vpe option
				if (editor && equals < 0 && i + 1 < args.Count && !args[i + 1].StartsWith("-")) {
					parts.Add(args[++i]);
				}
			}
			return string.Join(" ", parts);
		}

		private static void Try(DiagnosticsWriter w, string key, Func<string> value)
		{
			try {
				var v = value();
				if (!string.IsNullOrEmpty(v)) {
					w.Add(key, v);
				}
			} catch (Exception e) {
				Logger.Debug(e, "Cannot read {Key:l} for diagnostics.", key);
			}
		}

		private static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
		private static bool IsLinux => Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor;
		private static bool IsMac => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

		#region OS

		private static string OsDetail()
		{
			if (IsLinux) {
				string distro = null;
				if (File.Exists("/etc/os-release")) {
					var line = File.ReadAllLines("/etc/os-release").FirstOrDefault(l => l.StartsWith("PRETTY_NAME="));
					distro = line?.Substring("PRETTY_NAME=".Length).Trim('"');
				}
				var session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
				var parts = new List<string>();
				if (!string.IsNullOrEmpty(distro)) {
					parts.Add(distro);
				}
				if (!string.IsNullOrEmpty(session)) {
					parts.Add("session " + session);
				}
				return string.Join(", ", parts);
			}
			if (IsMac) {
				return SysctlInt("sysctl.proc_translated") == 1 ? "running under Rosetta" : null;
			}
			return null;
		}

		#endregion

		#region CPU

		private static int? PhysicalCores()
		{
			if (IsWindows) {
				return WindowsPhysicalCores();
			}
			if (IsLinux) {
				return LinuxPhysicalCores();
			}
			if (IsMac) {
				return SysctlInt("hw.physicalcpu");
			}
			return null;
		}

		private static int? WindowsPhysicalCores()
		{
			const int relationProcessorCore = 0;
			uint length = 0;
			GetLogicalProcessorInformationEx(relationProcessorCore, IntPtr.Zero, ref length);
			if (length == 0) {
				return null;
			}
			var buffer = Marshal.AllocHGlobal((int)length);
			try {
				if (!GetLogicalProcessorInformationEx(relationProcessorCore, buffer, ref length)) {
					return null;
				}
				// variable-size records, each starting with int Relationship, uint Size
				var cores = 0;
				var offset = 0;
				while (offset < length) {
					var size = Marshal.ReadInt32(buffer, offset + 4);
					if (size <= 0) {
						break;
					}
					cores++;
					offset += size;
				}
				return cores;
			} finally {
				Marshal.FreeHGlobal(buffer);
			}
		}

		private static int? LinuxPhysicalCores()
		{
			if (!File.Exists("/proc/cpuinfo")) {
				return null;
			}
			var cores = new HashSet<string>();
			var physicalId = "0";
			foreach (var line in File.ReadAllLines("/proc/cpuinfo")) {
				if (line.StartsWith("physical id")) {
					physicalId = line.Substring(line.IndexOf(':') + 1).Trim();
				} else if (line.StartsWith("core id")) {
					cores.Add(physicalId + ":" + line.Substring(line.IndexOf(':') + 1).Trim());
				}
			}
			return cores.Count > 0 ? cores.Count : (int?)null;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

		#endregion

		#region GPU driver

		/// <summary>
		/// Windows only: the installed driver version of the display adapter with the given
		/// PCI ids, from the registry's display adapter class.
		/// </summary>
		private static string GpuDriverVersion(int vendorId, int deviceId)
		{
			if (!IsWindows) {
				return null;
			}
			const string classKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\";
			var match = $"ven_{vendorId:x4}&dev_{deviceId:x4}";
			for (var i = 0; i < 32; i++) {
				var key = classKey + i.ToString("0000");
				var matching = ReadRegistryString(key, "MatchingDeviceId");
				if (matching == null || matching.IndexOf(match, StringComparison.OrdinalIgnoreCase) < 0) {
					continue;
				}
				var version = ReadRegistryString(key, "DriverVersion");
				var date = ReadRegistryString(key, "DriverDate");
				return date != null ? $"{version} ({date})" : version;
			}
			return null;
		}

		private static string ReadRegistryString(string subKey, string value)
		{
			var hklm = new UIntPtr(0x80000002u);
			const uint rrfRtRegSz = 0x00000002;
			uint size = 0;
			if (RegGetValue(hklm, subKey, value, rrfRtRegSz, IntPtr.Zero, null, ref size) != 0 || size == 0) {
				return null;
			}
			var buffer = new StringBuilder((int)(size / 2) + 1);
			return RegGetValue(hklm, subKey, value, rrfRtRegSz, IntPtr.Zero, buffer, ref size) == 0 ? buffer.ToString() : null;
		}

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegGetValueW")]
		private static extern int RegGetValue(UIntPtr hkey, string subKey, string value, uint flags, IntPtr type, StringBuilder data, ref uint dataSize);

		#endregion

		#region macOS

		private static int? SysctlInt(string name)
		{
			var size = (IntPtr)4;
			var value = 0;
			return sysctlbyname(name, ref value, ref size, IntPtr.Zero, IntPtr.Zero) == 0 ? value : (int?)null;
		}

		[DllImport("libc")]
		private static extern int sysctlbyname(string name, ref int oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

		#endregion
	}
}
