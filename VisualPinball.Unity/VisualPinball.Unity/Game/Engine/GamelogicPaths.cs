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
using System.IO;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Folders that gamelogic engines need outside of the table, set by whoever hosts the table
	/// (the player app, or the editor). Lives here so hosts don't need to reference each gamelogic
	/// engine's assembly.
	/// </summary>
	public static class GamelogicPaths
	{
		/// <summary>
		/// The folder(s) emulators look for ROMs in, separated by ';'. The player's own setting if it
		/// set one, otherwise the host's. Null when neither is set, in which case the gamelogic engine
		/// falls back to its own default.
		/// </summary>
		public static string RomFolder => RomFolderOverride ?? HostRomFolder;

		/// <summary>
		/// The ROM folder(s) of the environment the table runs in, e.g. the editor's setting.
		/// </summary>
		public static string HostRomFolder
		{
			get => _hostRomFolder;
			set => _hostRomFolder = NormalizeFolderList(value);
		}

		/// <summary>
		/// The ROM folder(s) the player app chose, which take precedence over the host's. Reset at the
		/// start of each play session, so it never outlives the session that set it (also when the
		/// editor doesn't reload the domain on play).
		/// </summary>
		public static string RomFolderOverride
		{
			get => _romFolderOverride;
			set => _romFolderOverride = NormalizeFolderList(value);
		}

		private static string _hostRomFolder;
		private static string _romFolderOverride;

		/// <summary>
		/// A gamelogic engine couldn't find the ROM the table needs. Raised on the main thread with
		/// the ROM's name and the folder(s) that were searched, so the host can ask for the ROM
		/// folder.
		/// </summary>
		public static event Action<string, string> RomMissing;

		/// <summary>
		/// Called by a gamelogic engine on the main thread when the table's ROM isn't there.
		/// </summary>
		/// <param name="rom">Name of the ROM, e.g. <c>t2_l8</c></param>
		/// <param name="searchedFolders">The ';'-separated folders it looked in, or null</param>
		public static void ReportRomMissing(string rom, string searchedFolders)
		{
			RomMissing?.Invoke(rom, searchedFolders);
		}

		/// <summary>
		/// The root of VPE's own data on this machine (<c>%LOCALAPPDATA%\Visual Pinball Engine</c>
		/// on Windows), shared by all VPE apps. Gamelogic engines keep their data in a sub folder.
		/// </summary>
		public static string DataRoot
		{
			get {
				var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				if (string.IsNullOrEmpty(localAppData)) {
					localAppData = Application.persistentDataPath;
				}
				return Path.Combine(localAppData, "Visual Pinball Engine");
			}
		}

		/// <summary>
		/// Trims each folder of a ';'-separated list and drops empty entries.
		/// </summary>
		/// <returns>The normalized list, or null if it has no folders.</returns>
		public static string NormalizeFolderList(string folders)
		{
			if (string.IsNullOrWhiteSpace(folders)) {
				return null;
			}
			var entries = folders.Split(';').Select(folder => folder.Trim()).Where(folder => folder.Length > 0).ToArray();
			return entries.Length > 0 ? string.Join(";", entries) : null;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetPlaySession()
		{
			_romFolderOverride = null;
			RomMissing = null;
		}
	}
}
