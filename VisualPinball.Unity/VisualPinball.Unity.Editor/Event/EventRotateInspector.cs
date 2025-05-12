// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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

using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(EventRotateComponent))]
	public class EventRotateInspector : UnityEditor.Editor
	{
		public override VisualElement CreateInspectorGUI()
		{
			var ui = new VisualElement();
			var eventEmitterComponents = (target as MonoBehaviour)?.GetComponents<IPinballEventEmitter>();
			if (eventEmitterComponents == null || eventEmitterComponents.Length == 0) {
				var csharpHelpBox = new HelpBox("This component must be added on a component that emits events.", HelpBoxMessageType.Error);
				ui.Add(csharpHelpBox);
				return ui;
			}

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Event/EventRotateInspector.uxml");
			visualTree.CloneTree(ui);

			var nameDropdown = ui.Q<DropdownField>("eventName");
			var validUnits = new[] {
				PinballEventUnit.None,
				PinballEventUnit.Degrees,
				PinballEventUnit.DegreesPerSecond,
				PinballEventUnit.Radians,
				PinballEventUnit.RadiansPerSecond,
			};
			nameDropdown.choices = eventEmitterComponents
				.SelectMany(c => c.Events)
				.Where(c => validUnits.Contains(c.Unit))
				.Select(c => c.Name)
				.ToList();

			return ui;
		}
	}
}
