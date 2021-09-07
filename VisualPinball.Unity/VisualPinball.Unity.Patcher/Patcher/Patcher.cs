// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable UnusedType.Global

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Patcher
{
	public class Patcher : IPatcher
	{
		private readonly List<object> _patchers = new List<object>();
		private FileTableContainer _tableContainer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void Set(FileTableContainer tableContainer, string fileName)
		{
			_tableContainer = tableContainer;
			var types = typeof(Patcher).Assembly.GetTypes();
			foreach (var type in types) {
				var classMatchers = type
					.GetCustomAttributes(typeof(TableMatchAttribute), false)
					.Select(a => a as TableMatchAttribute)
					.Where(a => a != null)
					.Where(a => a.Matches(_tableContainer, fileName))
					.ToArray();

				if (classMatchers.Length > 0) {
					_patchers.Add(Activator.CreateInstance(type));
				}
			}
			Logger.Info("Table will be patched using the following patchers: [ {0} ]", string.Join(", ", _patchers.Select(o => o.GetType().Name)));
		}

		public void ApplyPatches(GameObject gameObject, GameObject tableGameObject)
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
							if (methodMatcher.Matches(_tableContainer, gameObject)) {
								var validArgs = true;
								var patcherParamInfos = methodInfo.GetParameters();
								var patcherParams = new object[patcherParamInfos.Length];

								foreach (var pi in patcherParamInfos) {

									// principal game object
									if (pi.ParameterType == typeof(GameObject)) {
										patcherParams[pi.Position] = gameObject;

									// game object reference
									} else if (pi.ParameterType == typeof(GameObject).MakeByRefType()) {
										if (methodMatcher.Ref == null) {
											Logger.Warn($"No Ref provided in {pi.ParameterType} {pi.Name} in patch method {patcher.GetType()}.{methodInfo.Name}(), skipping.");
											validArgs = false;

										} else {
											var goRef = tableGameObject.transform.Find(methodMatcher.Ref);
											if (goRef == null) {
												Logger.Warn($"No GameObject named {methodMatcher.Ref} found in {pi.ParameterType} {pi.Name} in patch method {patcher.GetType()}.{methodInfo.Name}(), skipping.");
												validArgs = false;

											} else {
												patcherParams[pi.Position] = goRef.gameObject;
											}
										}

									// component
									} else if (typeof(MonoBehaviour).IsAssignableFrom(pi.ParameterType)) {
										var comp = gameObject.GetComponent(pi.ParameterType);
										if (comp != null) {
											patcherParams[pi.Position] = comp;

										} else {
											Logger.Warn($"Component {pi.ParameterType} not found on element \"{gameObject.name}\".");
											validArgs = false;
										}

									// table object
									} else if (pi.ParameterType == typeof(Table)) {
										patcherParams[pi.Position] = _tableContainer.Table;

									// source table container
									} else if (pi.ParameterType == typeof(FileTableContainer)) {
										patcherParams[pi.Position] = _tableContainer;

									} else {
										Logger.Warn($"Unknown parameter {pi.ParameterType} {pi.Name} in patch method {patcher.GetType()}.{methodInfo.Name}(), skipping.");
										validArgs = false;
									}
								}

								if (validArgs) {
									Logger.Info($"Patching element \"{gameObject.name}\" based on match by {patcher.GetType().Name}.{method.Name}");
									methodInfo.Invoke(patcher, patcherParams);

								} else {
									Logger.Error($"NOT patching element \"{gameObject.name}\" based on match by {patcher.GetType().Name}.{method.Name}");
								}

							}
						}
					}
				}
			}
		}
	}
}
