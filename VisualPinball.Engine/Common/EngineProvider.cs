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

// ReSharper disable StaticMemberInGenericType

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace VisualPinball.Engine.Common
{
	/// <summary>
	/// A factory class that allows instantiation of configurable
	/// implementations at runtime.
	///
	/// We use it for swapping the physics engine and the debug UI. It takes in
	/// an interface, searches the assemblies for implementations, and provides
	/// a simple way to set and use them.
	/// </summary>
	/// <typeparam name="T">Type to instantiate</typeparam>
	public static class EngineProvider<T> where T : IEngine
	{
		/// <summary>
		/// Returns true if an implementation has been set.
		/// </summary>
		public static bool Exists { get; private set; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static T _selectedEngine;
		private static Dictionary<string, T> _availableEngines;

		/// <summary>
		/// Returns a list of all implementations.
		/// </summary>
		public static IEnumerable<T> GetAll()
		{
			var t = typeof(T);

			if (_availableEngines == null) {
				var engines = AppDomain.CurrentDomain.GetAssemblies()
					.Where(x => x.FullName.StartsWith("VisualPinball."))
					.SelectMany(x => x.GetTypes())
					.Where(x => x.IsClass && t.IsAssignableFrom(x))
					.Select(x => (T) Activator.CreateInstance(x));

				_availableEngines = new Dictionary<string, T>();
				foreach (var engine in engines) {
					_availableEngines[GetId(engine)] = engine;
				}

				// be kind: if there's only one, set it.
				if (_availableEngines.Count == 1) {
					_selectedEngine = _availableEngines.Values.First();
				}
			}
			return _availableEngines.Values;
		}

		/// <summary>
		/// Sets the implementation for this interface.
		/// </summary>
		/// <param name="id">Implementation ID, which is the full class name</param>
		/// <see cref="GetId">How to get the implementation ID</see>
		/// <exception cref="ArgumentException">On invalid ID</exception>
		public static void Set(string id)
		{
			if (id == null) {
				return;
			}
			if (_availableEngines == null) {
				GetAll();
			}
			if (!_availableEngines.ContainsKey(id)) {
				throw new ArgumentException($"Unknown {typeof(T)} engine {id} (available: [ {string.Join(", ", _availableEngines.Keys)} ]).");
			}
			_selectedEngine = _availableEngines[id];
			Logger.Info("Set {0} engine to {1}.", typeof(T), id);
			Exists = true;
		}

		/// <summary>
		/// Returns the currently selected implementation.
		/// </summary>
		/// <exception cref="InvalidOperationException">If none is selected</exception>
		public static T Get()
		{
			if (_selectedEngine == null) {
				throw new InvalidOperationException($"Must select {typeof(T)} engine before retrieving!");
			}
			return _selectedEngine;
		}

		/// <summary>
		/// Returns the implementation ID of a given instance
		/// </summary>
		/// <remarks>
		/// The goal of using strings as references it to be able to serialize
		/// it, i.e. assign an implementation of a given interface to the table
		/// in a persistent way.
		/// </remarks>
		/// <param name="obj">Implementation instance</param>
		/// <returns>ID of the implementation</returns>
		public static string GetId(object obj)
		{
			return obj.GetType().FullName;
		}
	}

	/// <summary>
	/// Base interface for all engines used by the factory.
	/// </summary>
	public interface IEngine
	{
		string Name { get; }
	}
}
