using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace VisualPinball.Engine.Common
{
	public class EngineProvider<T> where T : IEngine
	{
		public bool Exists { get; private set; }

		public static EngineProvider<T> Instance => _instance ?? (_instance = new EngineProvider<T>());

		private static EngineProvider<T> _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private T _selectedEngine;
		private Dictionary<string, T> _availableEngines;

		public IEnumerable<T> GetAll()
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

		public void Set(string id)
		{
			if (_availableEngines == null) {
				GetAll();
			}
			if (!_availableEngines.ContainsKey(id)) {
				throw new ArgumentException($"Unknown {typeof(T)} engine {id}.");
			}
			_selectedEngine = _availableEngines[id];
			Logger.Info("Set {0} engine to {1}.", typeof(T), id);
			Exists = true;
		}

		public T Get()
		{
			if (_selectedEngine == null) {
				throw new InvalidOperationException($"Must select {typeof(T)} engine before retrieving!");
			}
			return _selectedEngine;
		}

		public string GetId(object obj)
		{
			return obj.GetType().FullName;
		}
	}

	public interface IEngine
	{
		string Name { get; }
	}
}
