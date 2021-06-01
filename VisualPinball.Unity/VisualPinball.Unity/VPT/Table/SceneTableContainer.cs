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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;
using Material = VisualPinball.Engine.VPT.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	[Serializable]
	public class SceneTableContainer : TableContainer
	{
		public new Table Table => _tableAuthoring.Table;
		public override Mappings Mappings => new Mappings(_tableAuthoring.Mappings);

		public const int ChildObjectsLayer = 16;

		[NonSerialized] private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();


		public override Material GetMaterial(string name)
		{
			if (string.IsNullOrEmpty(name)) {
				return null;
			}
			return _materials.ContainsKey(name.ToLower()) ? _materials[name.ToLower()] : null;
		}

		public override Texture GetTexture(string name)
		{
			throw new NotImplementedException();
		}

		private readonly TableAuthoring _tableAuthoring;

		public SceneTableContainer(TableAuthoring ta)
		{
			_tableAuthoring = ta;
		}

		public void Refresh()
		{
			var stopWatch = Stopwatch.StartNew();
			Clear();
			WalkChildren(_tableAuthoring.transform);

			foreach (var material in _tableAuthoring.Data.Materials) {
				_materials[material.Name.ToLower()] = material;
			}

			Logger.Info($"Refreshed {GameItems.Count()} game items in {stopWatch.ElapsedMilliseconds}ms.");
		}

		protected override void Clear()
		{
			base.Clear();
			_materials.Clear();
		}

		private void WalkChildren(IEnumerable node)
		{
			foreach (Transform childTransform in node) {
				RefreshChild(childTransform);
				WalkChildren(childTransform);
			}
		}

		private void RefreshChild(Component node)
		{
			Add(node.GetComponent<IItemMainAuthoring>());
		}

		private void Add(IItemMainAuthoring comp)
		{
			if (comp == null) {
				return;
			}
			switch (comp) {
				case BumperAuthoring bumperAuthoring:
					Add(comp.gameObject.name, bumperAuthoring.Item);
					break;
				case FlipperAuthoring flipperAuthoring:
					Add(comp.gameObject.name, flipperAuthoring.Item);
					break;
				case GateAuthoring gateAuthoring:
					Add(comp.gameObject.name, gateAuthoring.Item);
					break;
				case HitTargetAuthoring hitTargetAuthoring:
					Add(comp.gameObject.name, hitTargetAuthoring.Item);
					break;
				case KickerAuthoring kickerAuthoring:
					Add(comp.gameObject.name, kickerAuthoring.Item);
					break;
				case LightAuthoring lightAuthoring:
					Add(comp.gameObject.name, lightAuthoring.Item);
					break;
				case PlungerAuthoring plungerAuthoring:
					Add(comp.gameObject.name, plungerAuthoring.Item);
					break;
				case PrimitiveAuthoring primitiveAuthoring:
					Add(comp.gameObject.name, primitiveAuthoring.Item);
					break;
				case RampAuthoring rampAuthoring:
					Add(comp.gameObject.name, rampAuthoring.Item);
					break;
				case RubberAuthoring rubberAuthoring:
					Add(comp.gameObject.name, rubberAuthoring.Item);
					break;
				case SpinnerAuthoring spinnerAuthoring:
					Add(comp.gameObject.name, spinnerAuthoring.Item);
					break;
				case SurfaceAuthoring surfaceAuthoring:
					Add(comp.gameObject.name, surfaceAuthoring.Item);
					break;
				case TriggerAuthoring triggerAuthoring:
					Add(comp.gameObject.name, triggerAuthoring.Item);
					break;
				case TroughAuthoring troughAuthoring:
					Add(comp.gameObject.name, troughAuthoring.Item);
					break;
			}
		}

		private void Add<T>(string name, T item) where T : IItem
		{
			var dict = GetItemDictionary<T>();
			if (dict.ContainsKey(name)) {
				Logger.Warn($"{item.GetType()} {name} already added.");
			} else {
				dict.Add(name, item);
			}
		}
	}
}
