// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

namespace VisualPinball.Engine.Common
{
	public static class Profiler
	{
		private static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
		private static List<Profile> RootProfiles { get; } = new List<Profile>();
		private static Profile _parent;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void Start(string key)
		{
			GetProfile(key).Start();
		}

		public static IDisposable StartUsing(string key)
		{
			return new ProfileSpan(GetProfile(key));
		}

		private static Profile GetProfile(string key)
		{
			if (Profiles.ContainsKey(key)) {
				_parent = Profiles[key];
				return _parent;

			} else {
				_parent = new Profile(key, _parent);
				Profiles.Add(key, _parent);
				if (_parent.Parent == null) {
					RootProfiles.Add(_parent);
				}
				return _parent;
			}
		}

		public static void Stop(string key)
		{
			Profiles[key].Stop();
			_parent = Profiles[key].Parent;
		}

		public static void Print()
		{
			var profile = string.Join("\n", RootProfiles.Select(p => p.ToString()));
			Logger.Debug("Profiling data:\n-------\n{0}\n-------\n\n", profile);
		}

		public static void Reset()
		{
			Profiles.Clear();
			RootProfiles.Clear();
			_parent = null;
		}
	}

	internal class ProfileSpan : IDisposable
	{
		public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

		private readonly Profile _profile;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		public ProfileSpan(Profile profile)
		{
			_profile = profile;
			_stopwatch.Start();
		}

		public void Dispose()
		{
			_stopwatch.Stop();
			_profile.Add(this);
		}
	}

	internal class Profile
	{
		public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds + _spanSum;

		private string Name { get; }
		public Profile Parent { get; }
		private readonly List<Profile> _children = new List<Profile>();
		private bool _isRunning;
		private int _count;

		private readonly int _level;
		private readonly Stopwatch _stopwatch = new Stopwatch();
		private long _spanSum = 0;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Profile(string name, Profile parent)
		{
			Name = name;
			Parent = parent;
			_level = parent?._level + 1 ?? 0;
			parent?._children.Add(this);
		}

		public Profile Start()
		{
			if (!_isRunning) {
				_stopwatch.Start();
			}
			_count++;
			_isRunning = true;
			return this;
		}

		public void Stop()
		{
			if (!_isRunning) {
				return;
			}
			_stopwatch.Stop();
			_children.ForEach(c => c.Stop());
			_isRunning = false;
		}

		public override string ToString()
		{
			var childrenSum = _children.Select(c => c.ElapsedMilliseconds).Sum();
			var str = $"{Name}: {ElapsedMilliseconds}ms / {_count}";
			if (_children.Count > 0) {
				str += $" (delta = {childrenSum - ElapsedMilliseconds}ms)";
			}
			var children = _children.Count > 0 ? "\n" + string.Join("\n", _children.Select(c => c.ToString())) : "";
			return str.PadLeft(str.Length + _level * 3) + children;
		}

		public void Add(ProfileSpan span)
		{
			_count++;
			_spanSum += span.ElapsedMilliseconds;
		}
	}
}
