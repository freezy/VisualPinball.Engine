// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	public static class DragPointMenuItems
	{
		public const string ControlPointsMenuPath = "CONTEXT/DragPointsItemInspector/ControlPoint";
		public const string CurveTravellerMenuPath = "CONTEXT/DragPointsItemInspector/CurveTraveller";

		private static DragPointData RetrieveDragPoint(IDragPointsInspector inspector, int controlId)
		{
			return inspector?.DragPointsHelper.GetDragPoint(controlId);
		}

		// Drag Points
		[MenuItem(ControlPointsMenuPath + "/Is Slingshot", false, 1)]
		private static void SlingShot(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				inspector.DragPointsHelper.PrepareUndo("Toggle Drag Point Slingshot");
				dragPoint.IsSlingshot = !dragPoint.IsSlingshot;
				inspector.DragPointsHelper.RebuildMeshes();
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Is Slingshot", true)]
		private static bool SlingshotValidate(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector) || inspector.DragPointsHelper.IsItemLocked()) {
				return false;
			}

			if (!inspector.DragPointsHelper.HasDragPointExposure(DragPointExposure.SlingShot)) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSlingshot", false);
				return false;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSlingshot", dragPoint.IsSlingshot);
			}

			return true;
		}

		[MenuItem(ControlPointsMenuPath + "/Is Smooth", false, 1)]
		private static void Smooth(MenuCommand command)
		{
			var inspector = command.context as IDragPointsInspector;
			if (inspector == null) {
				return;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				inspector.DragPointsHelper.PrepareUndo("Toggle Drag Point Smooth");
				dragPoint.IsSmooth = !dragPoint.IsSmooth;
				inspector.DragPointsHelper.RebuildMeshes();
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Is Smooth", true)]
		private static bool SmoothValidate(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector) || inspector.DragPointsHelper.IsItemLocked()) {
				return false;
			}

			if (!inspector.DragPointsHelper.HasDragPointExposure(DragPointExposure.Smooth)) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSmooth", false);
				return false;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSmooth", dragPoint.IsSmooth);
			}

			return true;
		}

		[MenuItem(ControlPointsMenuPath + "/Remove Point", false, 101)]
		private static void Remove(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			if (EditorUtility.DisplayDialog("Drag point removal", "Are you sure you want to remove this drag point?", "Yes", "No")) {
				inspector.DragPointsHelper.RemoveDragPoint(command.userData);
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Remove Point", true)]
		private static bool RemoveValidate(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector) || inspector.DragPointsHelper.IsItemLocked()) {
				return false;
			}

			if (inspector.DragPointsHelper.DragPointsHandler?.ControlPoints.Count <= 2) {
				Menu.SetChecked($"{ControlPointsMenuPath}/Remove Point", false);
				return false;
			}

			return true;
		}

		[MenuItem(ControlPointsMenuPath + "/Copy Point", false, 301)]
		private static void Copy(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			inspector.DragPointsHelper.CopyDragPoint(command.userData);
		}

		[MenuItem(ControlPointsMenuPath + "/Paste Point", false, 302)]
		private static void Paste(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			inspector.DragPointsHelper.PasteDragPoint(command.userData);
		}

		[MenuItem(ControlPointsMenuPath + "/Paste Point", true)]
		private static bool PasteValidate(MenuCommand command)
		{
			return command.context is IDragPointsInspector inspector && !inspector.DragPointsHelper.IsItemLocked();
		}

		//Curve Traveller
		[MenuItem(CurveTravellerMenuPath + "/Add Point", false, 1)]
		private static void Add(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			inspector.DragPointsHelper.AddDragPointOnTraveller();
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/X", false, 101)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/X", false, 201)]
		private static void FlipX(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			inspector.DragPointsHelper.FlipDragPoints(FlipAxis.X);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Y", false, 102)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Y", false, 202)]
		private static void FlipY(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			inspector.DragPointsHelper.FlipDragPoints(FlipAxis.Y);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Z", false, 103)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Z", false, 203)]
		private static void FlipZ(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			inspector.DragPointsHelper.FlipDragPoints(FlipAxis.Z);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Z", true, 103)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Z", true, 203)]
		private static bool FlipZValidate(MenuCommand command)
		{
			if (command.context is IDragPointsInspector inspector) {
				return inspector.HandleType == DragPointTransformType.ThreeD;
			}
			return false;
		}

		[MenuItem(CurveTravellerMenuPath + "/Reverse", false, 501)]
		[MenuItem(ControlPointsMenuPath + "/Reverse", false, 601)]
		private static void Reverse(MenuCommand command)
		{
			if (!(command.context is IDragPointsInspector inspector)) {
				return;
			}

			inspector.DragPointsHelper.Reverse();
		}
	}
}
