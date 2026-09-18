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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The authored settings of the deformation renderer, plus what it would animate
	/// with the rubber's current bake.
	/// </summary>
	[CustomEditor(typeof(RubberDeformationRenderer)), CanEditMultipleObjects]
	public sealed class RubberDeformationRendererInspector : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			if (targets.Length != 1) {
				return;
			}
			var renderer = (RubberDeformationRenderer)target;
			var rubber = renderer.GetComponent<RubberComponent>();
			if (!rubber) {
				EditorGUILayout.HelpBox(
					"The deformation renderer belongs on a rubber; it animates that rubber's render mesh.",
					MessageType.Error);
				return;
			}
			if (!renderer.GetComponent<MeshFilter>()) {
				EditorGUILayout.HelpBox(
					"This rubber has no mesh filter, so there is nothing to displace.",
					MessageType.Warning);
			}

			var collider = renderer.GetComponent<RubberColliderComponent>();
			if (!collider || collider.Mode != RubberColliderMode.Physical
				|| !collider.DeformationEnabled) {
				EditorGUILayout.HelpBox(
					"Enable the compliant rebound on the rubber's Physical collider, otherwise no span ever deflects and nothing animates.",
					MessageType.Info);
				return;
			}

			var eligible = 0;
			var descriptors = collider.SpanDescriptors;
			for (var i = 0; i < descriptors.Count; i++) {
				if (descriptors[i].ReboundEligible) {
					eligible++;
				}
			}
			EditorGUILayout.LabelField("Animated free spans", eligible.ToString());
			if (eligible == 0) {
				EditorGUILayout.HelpBox(
					"The current bake has no rebound-eligible free span. Bake the rubber's compliant rebound again.",
					MessageType.Warning);
			}
			EditorGUILayout.HelpBox(
				"The kick curve and its duration also drive the arm of a physical slingshot bound to one of this rubber's spans.",
				MessageType.None);
		}
	}
}
