﻿// Visual Pinball Engine
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

using UnityEditor;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	public static class DragPointMenuItems
	{
		public const string ControlPointsMenuPath = "CONTEXT/DragPointsItemInspector/ControlPoint";
		public const string CurveTravellerMenuPath = "CONTEXT/DragPointsItemInspector/CurveTraveller";

		private static DragPointData RetrieveDragPoint(IDragPointsItemInspector inspector, int controlId)
		{
			return inspector?.GetDragPoint(controlId);
		}

		// Drag Points
		[MenuItem(ControlPointsMenuPath + "/Is Slingshot", false, 1)]
		private static void SlingShot(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				inspector.PrepareUndo("Toggle Drag Point Slingshot");
				dragPoint.IsSlingshot = !dragPoint.IsSlingshot;
				inspector.RebuildMeshes();
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Is Slingshot", true)]
		private static bool SlingshotValidate(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector) || inspector.IsItemLocked()) {
				return false;
			}

			if (!inspector.HasDragPointExposure(DragPointExposure.SlingShot)) {
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
			var inspector = command.context as IDragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				inspector.PrepareUndo("Toggle Drag Point Smooth");
				dragPoint.IsSmooth = !dragPoint.IsSmooth;
				inspector.RebuildMeshes();
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Is Smooth", true)]
		private static bool SmoothValidate(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector) || inspector.IsItemLocked()) {
				return false;
			}

			if (!inspector.HasDragPointExposure(DragPointExposure.Smooth)) {
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
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			if (EditorUtility.DisplayDialog("Drag point removal", "Are you sure you want to remove this drag point?", "Yes", "No")) {
				inspector.RemoveDragPoint(command.userData);
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Remove Point", true)]
		private static bool RemoveValidate(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector) || inspector.IsItemLocked()) {
				return false;
			}

			if (inspector.DragPointsHandler?.ControlPoints.Count <= 2) {
				Menu.SetChecked($"{ControlPointsMenuPath}/Remove Point", false);
				return false;
			}

			return true;
		}

		[MenuItem(ControlPointsMenuPath + "/Copy Point", false, 301)]
		private static void Copy(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			inspector.CopyDragPoint(command.userData);
		}

		[MenuItem(ControlPointsMenuPath + "/Paste Point", false, 302)]
		private static void Paste(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			inspector.PasteDragPoint(command.userData);
		}

		[MenuItem(ControlPointsMenuPath + "/Paste Point", true)]
		private static bool PasteValidate(MenuCommand command)
		{
			return command.context is IDragPointsItemInspector inspector && !inspector.IsItemLocked();
		}

		//Curve Traveller
		[MenuItem(CurveTravellerMenuPath + "/Add Point", false, 1)]
		private static void Add(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			inspector.AddDragPointOnTraveller();
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/X", false, 101)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/X", false, 201)]
		private static void FlipX(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.X);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Y", false, 102)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Y", false, 202)]
		private static void FlipY(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.Y);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Z", false, 103)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Z", false, 203)]
		private static void FlipZ(MenuCommand command)
		{
			if (!(command.context is IDragPointsItemInspector inspector)) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.Z);
		}
	}
}
