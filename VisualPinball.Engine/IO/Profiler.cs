using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NLog;

namespace VisualPinball.Engine.IO
{
	public static class Profiler
	{
		private static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
		private static List<Profile> RootProfiles { get; } = new List<Profile>();
		private static Profile _parent;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void Start(string key)
		{
			if (Profiles.ContainsKey(key)) {
				_parent = Profiles[key];
				Profiles[key].Start();

			} else {
				_parent = new Profile(key, _parent);
				Profiles.Add(key, _parent);
				if (_parent.Parent == null) {
					RootProfiles.Add(_parent);
				}
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

	internal class Profile
	{
		private string Name { get; }
		public Profile Parent { get; }
		private readonly List<Profile> _children = new List<Profile>();
		private bool _isRunning;

		private readonly int _level;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Profile(string name, Profile parent)
		{
			Name = name;
			Parent = parent;
			_level = parent?._level + 1 ?? 0;
			parent?._children.Add(this);
			Start();
		}

		public void Start()
		{
			if (!_isRunning) {
				_stopwatch.Start();
			}
			_isRunning = true;
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

		public void Print()
		{
			Logger.Debug(this);
			_children.ForEach(c => c.Print());
		}

		public override string ToString()
		{
			var childrenSum = _children.Select(c => c._stopwatch.ElapsedMilliseconds).Sum();
			var str = $"{Name}: {_stopwatch.ElapsedMilliseconds}ms";
			if (_children.Count > 0) {
				str += $" (delta = {childrenSum - _stopwatch.ElapsedMilliseconds}ms)";
			}
			var children = _children.Count > 0 ? "\n" + string.Join("\n", _children.Select(c => c.ToString())) : "";
			return str.PadLeft(str.Length + _level * 3) + children;
		}
	}
}
