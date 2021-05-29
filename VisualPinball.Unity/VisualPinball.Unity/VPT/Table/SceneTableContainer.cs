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
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;
using Light = VisualPinball.Engine.VPT.Light.Light;
using Material = VisualPinball.Engine.VPT.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	public class SceneTableContainer : TableContainer, IDisposable
	{
		public Table Table => _tableAuthoring.Table;

		public override Material GetMaterial(string name)
		{
			throw new NotImplementedException();
		}
		public override Texture GetTexture(string name)
		{
			throw new NotImplementedException();
		}

		private readonly TableAuthoring _tableAuthoring;

		public SceneTableContainer(TableAuthoring ta)
		{
			_tableAuthoring = ta;

#if UNITY_EDITOR
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
#endif
		}

		public void Refresh()
		{
			OnHierarchyChanged();
		}

		private void OnHierarchyChanged()
		{
			var stopWatch = Stopwatch.StartNew();
			Clear();
			WalkChildren(_tableAuthoring.transform);

			Logger.Info($"Refreshed {GameItems.Count()} game items in {stopWatch.ElapsedMilliseconds}ms.");
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
					GetItemDictionary<Bumper>().Add(comp.Name, bumperAuthoring.Item);
					break;
				case FlipperAuthoring flipperAuthoring:
					GetItemDictionary<Flipper>().Add(comp.Name, flipperAuthoring.Item);
					break;
				case GateAuthoring gateAuthoring:
					GetItemDictionary<Gate>().Add(comp.Name, gateAuthoring.Item);
					break;
				case HitTargetAuthoring hitTargetAuthoring:
					GetItemDictionary<HitTarget>().Add(comp.Name, hitTargetAuthoring.Item);
					break;
				case KickerAuthoring kickerAuthoring:
					GetItemDictionary<Kicker>().Add(comp.Name, kickerAuthoring.Item);
					break;
				case LightAuthoring lightAuthoring:
					GetItemDictionary<Light>().Add(comp.Name, lightAuthoring.Item);
					break;
				case PlungerAuthoring plungerAuthoring:
					GetItemDictionary<Plunger>().Add(comp.Name, plungerAuthoring.Item);
					break;
				case PrimitiveAuthoring primitiveAuthoring:
					GetItemDictionary<Primitive>().Add(comp.Name, primitiveAuthoring.Item);
					break;
				case RampAuthoring rampAuthoring:
					GetItemDictionary<Ramp>().Add(comp.Name, rampAuthoring.Item);
					break;
				case RubberAuthoring rubberAuthoring:
					GetItemDictionary<Rubber>().Add(comp.Name, rubberAuthoring.Item);
					break;
				case SpinnerAuthoring spinnerAuthoring:
					GetItemDictionary<Spinner>().Add(comp.Name, spinnerAuthoring.Item);
					break;
				case SurfaceAuthoring surfaceAuthoring:
					GetItemDictionary<Surface>().Add(comp.Name, surfaceAuthoring.Item);
					break;
				case TriggerAuthoring triggerAuthoring:
					GetItemDictionary<Trigger>().Add(comp.Name, triggerAuthoring.Item);
					break;
				case TroughAuthoring troughAuthoring:
					GetItemDictionary<Trough>().Add(comp.Name, troughAuthoring.Item);
					break;
			}
		}

		public void Dispose()
		{
#if UNITY_EDITOR
			EditorApplication.hierarchyChanged -= OnHierarchyChanged;
#endif
		}
	}
}
