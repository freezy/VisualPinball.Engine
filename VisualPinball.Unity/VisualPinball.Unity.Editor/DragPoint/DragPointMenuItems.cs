using UnityEditor;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.DragPoint
{
	public static class DragPointMenuItems
	{
		public const string ControlPointsMenuPath = "CONTEXT/DragPointsItemInspector/ControlPoint";
		public const string CurveTravellerMenuPath = "CONTEXT/DragPointsItemInspector/CurveTraveller";

		private static DragPointData RetrieveDragPoint(DragPointsItemInspector inspector, int controlId)
		{
			return inspector == null ? null : inspector.GetDragPoint(controlId);
		}

		// Drag Points
		[MenuItem(ControlPointsMenuPath + "/IsSlingshot", false, 1)]
		private static void SlingShot(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			var dpoint = RetrieveDragPoint(inspector, command.userData);
			if (dpoint != null) {
				inspector.PrepareUndo("Changing DragPoint IsSlingshot");
				dpoint.IsSlingshot = !dpoint.IsSlingshot;
			}
		}

		[MenuItem(ControlPointsMenuPath + "/IsSlingshot", true)]
		private static bool SlingshotValidate(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null || inspector.IsItemLocked()) {
				return false;
			}

			if (!inspector.HasDragPointExposition(DragPointExposition.SlingShot)) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSlingshot", false);
				return false;
			}

			var dpoint = RetrieveDragPoint(inspector, command.userData);
			if (dpoint != null) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSlingshot", dpoint.IsSlingshot);
			}

			return true;
		}

		[MenuItem(ControlPointsMenuPath + "/IsSmooth", false, 1)]
		private static void Smooth(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			var dpoint = RetrieveDragPoint(inspector, command.userData);
			if (dpoint != null) {
				inspector.PrepareUndo("Changing DragPoint IsSmooth");
				dpoint.IsSmooth = !dpoint.IsSmooth;
			}
		}

		[MenuItem(ControlPointsMenuPath + "/IsSmooth", true)]
		private static bool SmoothValidate(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null || inspector.IsItemLocked()) {
				return false;
			}

			if (!inspector.HasDragPointExposition(DragPointExposition.Smooth)) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSmooth", false);
				return false;
			}

			var dpoint = RetrieveDragPoint(inspector, command.userData);
			if (dpoint != null) {
				Menu.SetChecked($"{ControlPointsMenuPath}/IsSmooth", dpoint.IsSmooth);
			}

			return true;
		}

		[MenuItem(ControlPointsMenuPath + "/Remove Point", false, 101)]
		private static void RemoveDP(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			if (EditorUtility.DisplayDialog("DragPoint Removal", "Are you sure you want to remove this Dragpoint ?", "Yes", "No")) {
				inspector.RemoveDragPoint(command.userData);
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Remove Point", true)]
		private static bool RemoveDPValidate(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null || inspector.IsItemLocked()) {
				return false;
			}

			return true;
		}

		[MenuItem(ControlPointsMenuPath + "/Copy Point", false, 301)]
		private static void CopyDP(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.CopyDragPoint(command.userData);
		}

		[MenuItem(ControlPointsMenuPath + "/Paste Point", false, 302)]
		private static void PasteDP(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.PasteDragPoint(command.userData);
		}

		//Curve Traveller
		[MenuItem(CurveTravellerMenuPath + "/Add Point", false, 1)]
		private static void AddDP(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.AddDragPointOnTraveller();
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/X", false, 101)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/X", false, 201)]
		private static void FlipXDP(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.X);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Y", false, 102)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Y", false, 202)]
		private static void FlipYDP(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.Y);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Z", false, 103)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Z", false, 203)]
		private static void FlipZDP(MenuCommand command)
		{
			DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.Z);
		}
	}
}
