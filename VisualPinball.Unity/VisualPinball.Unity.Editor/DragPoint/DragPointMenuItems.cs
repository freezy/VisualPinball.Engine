using UnityEditor;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
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
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				inspector.PrepareUndo("Toggle drag point slingshot");
				dragPoint.IsSlingshot = !dragPoint.IsSlingshot;
			}
		}

		[MenuItem(ControlPointsMenuPath + "/IsSlingshot", true)]
		private static bool SlingshotValidate(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null || inspector.IsItemLocked()) {
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

		[MenuItem(ControlPointsMenuPath + "/IsSmooth", false, 1)]
		private static void Smooth(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			var dragPoint = RetrieveDragPoint(inspector, command.userData);
			if (dragPoint != null) {
				inspector.PrepareUndo("Toggle drag point smooth");
				dragPoint.IsSmooth = !dragPoint.IsSmooth;
			}
		}

		[MenuItem(ControlPointsMenuPath + "/IsSmooth", true)]
		private static bool SmoothValidate(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null || inspector.IsItemLocked()) {
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
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			if (EditorUtility.DisplayDialog("Drag point removal", "Are you sure you want to remove this drag point?", "Yes", "No")) {
				inspector.RemoveDragPoint(command.userData);
			}
		}

		[MenuItem(ControlPointsMenuPath + "/Remove Point", true)]
		private static bool RemoveValidate(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null || inspector.IsItemLocked()) {
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
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.CopyDragPoint(command.userData);
		}

		[MenuItem(ControlPointsMenuPath + "/Paste Point", false, 302)]
		private static void Paste(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.PasteDragPoint(command.userData);
		}

		[MenuItem(ControlPointsMenuPath + "/Paste Point", true)]
		private static bool PasteValidate(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null || inspector.IsItemLocked()) {
				return false;
			}

			return true;
		}

		//Curve Traveller
		[MenuItem(CurveTravellerMenuPath + "/Add Point", false, 1)]
		private static void Add(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.AddDragPointOnTraveller();
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/X", false, 101)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/X", false, 201)]
		private static void FlipX(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.X);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Y", false, 102)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Y", false, 202)]
		private static void FlipY(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.Y);
		}

		[MenuItem(CurveTravellerMenuPath + "/Flip Drag Points/Z", false, 103)]
		[MenuItem(ControlPointsMenuPath + "/Flip Drag Points/Z", false, 203)]
		private static void FlipZ(MenuCommand command)
		{
			var inspector = command.context as DragPointsItemInspector;
			if (inspector == null) {
				return;
			}

			inspector.FlipDragPoints(FlipAxis.Z);
		}
	}
}
