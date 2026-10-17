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
using System.Diagnostics;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Plugins;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// What was built: the app version, the commit of every VPE repository that went into it
	/// and the version of every VPE package.
	/// </summary>
	[Serializable]
	public sealed class VpeBuildInfo
	{
		public string version;
		public string buildTime;
		public bool development;
		public List<Repository> repositories = new();
		public List<Package> packages = new();

		[Serializable]
		public sealed class Repository
		{
			public string name;
			public string commit;
			public bool dirty;
		}

		[Serializable]
		public sealed class Package
		{
			public string name;
			public string version;
		}

		/// <summary>
		/// Short commit of the first repository, with a "+dirty" marker, or null.
		/// </summary>
		public string Commit => repositories.Count == 0 || string.IsNullOrEmpty(repositories[0].commit)
			? null
			: repositories[0].commit + (repositories[0].dirty ? "+dirty" : "");

		public void Describe(DiagnosticsWriter w)
		{
			w.Add("version", version);
			w.Add("build-time", buildTime);
			w.Add("development", development);
			w.Add("flavor", BuildInfo.Flavor);
			foreach (var repository in repositories) {
				w.Add("commit." + repository.name, repository.commit + (repository.dirty ? " (dirty)" : ""));
			}
			foreach (var package in packages) {
				w.Add("package." + package.name, package.version);
			}
		}
	}

	/// <summary>
	/// Reads the build info a build wrote into <c>Resources/VpeBuildInfo.json</c>. In the
	/// editor, it's collected live from the package manager and git instead.
	/// </summary>
	public static class BuildInfo
	{
		public const string ResourceName = "VpeBuildInfo";
		private const string PackagePrefix = "org.visualpinball.";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static VpeBuildInfo _current;

		/// <summary>
		/// "release", or for the profile build "profile", "profile-dev" when it's a development
		/// build. Decided by how this code was compiled, so it can't disagree with it.
		/// </summary>
		public static string Flavor => !VpeProfiler.IsProfileBuild ? "release" : UnityEngine.Debug.isDebugBuild && !Application.isEditor ? "profile-dev" : "profile";

		/// <summary>
		/// The build info of the running app. Main thread on first access.
		/// </summary>
		public static VpeBuildInfo Current => _current ??= Load();

		/// <summary>
		/// Collects the editor's build info again on next access: with domain reload off, the
		/// version or HEAD may have changed since the last play session. No effect in builds.
		/// </summary>
		public static void Invalidate()
		{
#if UNITY_EDITOR
			_current = null;
#endif
		}

		private static VpeBuildInfo Load()
		{
#if UNITY_EDITOR
			return Collect(development: true, checkDirty: false);
#else
			try {
				var asset = Resources.Load<TextAsset>(ResourceName);
				if (asset != null) {
					var info = JsonUtility.FromJson<VpeBuildInfo>(asset.text);
					if (info != null) {
						return info;
					}
				}
			} catch (Exception e) {
				Logger.Warn(e, "Cannot read the build info.");
			}
			return new VpeBuildInfo { version = Application.version, development = UnityEngine.Debug.isDebugBuild };
#endif
		}

#if UNITY_EDITOR
		/// <summary>
		/// Collects the build info from the package manager and git. Editor only; also used by
		/// the build step that writes the resource.
		/// </summary>
		/// <param name="checkDirty">Whether to ask git about uncommitted changes, which takes
		/// a process per repository. Commits are read from the .git folder directly.</param>
		public static VpeBuildInfo Collect(bool development, bool checkDirty)
		{
			var info = new VpeBuildInfo {
				version = Application.version,
				buildTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
				development = development,
			};
			AddRepository(info, System.IO.Path.GetDirectoryName(Application.dataPath), checkDirty);
			try {
				var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
					.Where(p => p.name.StartsWith(PackagePrefix, StringComparison.Ordinal))
					.OrderBy(p => p.name, StringComparer.Ordinal);
				foreach (var package in packages) {
					info.packages.Add(new VpeBuildInfo.Package { name = package.name, version = package.version });
					if (package.source == UnityEditor.PackageManager.PackageSource.Local) {
						AddRepository(info, package.resolvedPath, checkDirty);
					}
				}
			} catch (Exception e) {
				Logger.Warn(e, "Cannot list the VPE packages for the build info.");
			}
			return info;
		}

		private static void AddRepository(VpeBuildInfo info, string path, bool checkDirty)
		{
			var top = FindRepositoryRoot(path);
			if (top == null) {
				return;
			}
			var name = System.IO.Path.GetFileName(top.TrimEnd('/', '\\'));
			// packages of the same repository share one entry
			if (info.repositories.Any(r => r.name == name)) {
				return;
			}
			var commit = ReadHead(top);
			if (commit == null) {
				return;
			}
			info.repositories.Add(new VpeBuildInfo.Repository {
				name = name,
				commit = commit.Substring(0, System.Math.Min(12, commit.Length)),
				dirty = checkDirty && !string.IsNullOrEmpty(Git(top, "status --porcelain --untracked-files=no")),
			});
		}

		private static string FindRepositoryRoot(string path)
		{
			for (var dir = path; !string.IsNullOrEmpty(dir); dir = System.IO.Path.GetDirectoryName(dir)) {
				var git = System.IO.Path.Combine(dir, ".git");
				if (System.IO.Directory.Exists(git) || System.IO.File.Exists(git)) {
					return dir;
				}
			}
			return null;
		}

		/// <summary>
		/// The commit HEAD points to, read from the .git folder (also of a worktree).
		/// </summary>
		private static string ReadHead(string top)
		{
			try {
				var gitDir = System.IO.Path.Combine(top, ".git");
				if (System.IO.File.Exists(gitDir)) {
					// a worktree: "gitdir: <path>"
					var pointer = System.IO.File.ReadAllText(gitDir).Trim();
					gitDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(top, pointer.Substring("gitdir:".Length).Trim()));
				}
				var head = System.IO.File.ReadAllText(System.IO.Path.Combine(gitDir, "HEAD")).Trim();
				if (!head.StartsWith("ref:", StringComparison.Ordinal)) {
					return head;
				}
				var reference = head.Substring(4).Trim();
				// refs live in the common dir for worktrees
				var commonDir = gitDir;
				var commonFile = System.IO.Path.Combine(gitDir, "commondir");
				if (System.IO.File.Exists(commonFile)) {
					commonDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(gitDir, System.IO.File.ReadAllText(commonFile).Trim()));
				}
				foreach (var dir in new[] { gitDir, commonDir }) {
					var refFile = System.IO.Path.Combine(dir, reference);
					if (System.IO.File.Exists(refFile)) {
						return System.IO.File.ReadAllText(refFile).Trim();
					}
				}
				var packed = System.IO.Path.Combine(commonDir, "packed-refs");
				if (System.IO.File.Exists(packed)) {
					foreach (var line in System.IO.File.ReadAllLines(packed)) {
						if (line.EndsWith(" " + reference, StringComparison.Ordinal)) {
							return line.Substring(0, line.IndexOf(' '));
						}
					}
				}
			} catch (Exception e) {
				Logger.Debug(e, "Cannot read the git HEAD of {Path:l}.", top);
			}
			return null;
		}

		private static string Git(string workingDirectory, string arguments)
		{
			try {
				using var process = Process.Start(new ProcessStartInfo("git", arguments) {
					WorkingDirectory = workingDirectory,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
				});
				if (process == null) {
					return null;
				}
				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit(5000);
				return process.ExitCode == 0 ? output.Trim() : null;
			} catch (Exception) {
				// no git, or not a repository
				return null;
			}
		}
#endif
	}
}
