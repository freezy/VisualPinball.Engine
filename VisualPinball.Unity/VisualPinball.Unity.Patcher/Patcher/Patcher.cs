using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Patcher
{
	public class Patcher
	{
		private readonly List<object> _patchers = new List<object>();
		private readonly Table _table;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Patcher(Table table, string fileName)
		{
			_table = table;
			var types = typeof(Patcher).Assembly.GetTypes();
			foreach (var type in types) {
				var classMatchers = type
					.GetCustomAttributes(typeof(TableMatchAttribute), false)
					.Select(a => a as TableMatchAttribute)
					.Where(a => a != null)
					.Where(a => a.Matches(table, fileName))
					.ToArray();

				if (classMatchers.Length > 0) {
					_patchers.Add(Activator.CreateInstance(type));
				}
			}
			Logger.Info("Table will be patched using the following patchers: [ {0} ]", string.Join(", ", _patchers.Select(o => o.GetType().Name)));
		}

		public void ApplyPatches(IRenderable item, RenderObject renderObject, GameObject gameObject)
		{
			foreach (var patcher in _patchers) {
				var methods = patcher.GetType().GetMembers().Where(member => member.MemberType == MemberTypes.Method);
				foreach (var method in methods) {
					var methodMatchers = Attribute
						.GetCustomAttributes(method, typeof(ItemMatchAttribute))
						.Select(a => a as ItemMatchAttribute)
						.Where(a => a != null);

					var methodInfo = method as MethodInfo;
					if (methodInfo != null) {
						foreach (var methodMatcher in methodMatchers) {
							if (methodMatcher.Matches(_table, item, renderObject, gameObject)) {
								Logger.Info($"Patching element {item.Name} based on match by {patcher.GetType().Name}.{method.Name}");
								methodInfo.Invoke(patcher, new object[] {gameObject});
							}
						}
					}
				}
			}
		}
	}
}
