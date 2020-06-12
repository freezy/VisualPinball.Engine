using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Editor.Inspectors;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Editors
{
	public class DragPointsEditor 
	{
		public DragPointsEditor(ItemInspector _inspector) { _itemInspector = _inspector; }

		public class ControlPoint
		{
			static public float ScreenRadius = 0.25f;
			public DragPointData DragPoint;
			public Vector3 WorldPos = Vector3.zero;
			public Vector3 ScrPos = Vector3.zero;
			public bool IsSelected = false;
			public int ControlId = 0;

			public ControlPoint(ref DragPointData dp, int controlID)
			{
				DragPoint = dp;
				ControlId = controlID;
			}
		}

		private Object _target = null;
		private ItemInspector _itemInspector = null;

		//Control points storing & rendering
		private List<ControlPoint> _controlPoints = new List<ControlPoint>();
		private List<Vector3> _pathPoints = new List<Vector3>();

		//Control points position Handle
		private List<ControlPoint> _selectedCP = new List<ControlPoint>();
		private int _positionHandleControlId = 0;
		private Vector3 _positionHandlePosition = Vector3.zero;

		//Curve Traveller 
		static public float CurveTravellerSizeRatio = 0.75f;
		private int _curveTravellerControlId = 0;
		private Vector3 _curveTravellerPosition = Vector3.zero;
		private bool _curveTravellerVisible = false;
		private int _curveTravellerControlPointIdx = -1;

		private bool _needMeshRebuilt = false;

		//Drop down PopupMenus
		class MenuItems
		{
			public const string CONTROLPOINTS_MENUPATH = "CONTEXT/DragPointsEditor/ControlPoint";
			public const string CURVETRAVELLER_MENUPATH = "CONTEXT/DragPointsEditor/CurveTraveller";

			private static DragPointData RetrieveDragPoint(DragPointsEditor editor, int controlId)
			{
				if (editor == null)
					return null;
				return editor.GetDragPoint(controlId);
			}

			//Drag Points
			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsLocked", false, 1)]
			private static void Lock(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return;
				var dpoint = RetrieveDragPoint(editor.DragPointsEditor, command.userData);
				if (dpoint != null)
				{
					editor.DragPointsEditor.PrepareUndo("Changing DragPoint IsLocked");
					dpoint.IsLocked = !dpoint.IsLocked;
				}
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsLocked", true)]
			private static bool LockValidate(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return false;
				var dpoint = RetrieveDragPoint(editor.DragPointsEditor, command.userData);
				if (dpoint != null)
				{
					Menu.SetChecked(CONTROLPOINTS_MENUPATH + "/IsLocked", dpoint.IsLocked);
				}

				return true;
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSlingshot", false, 1)]
			private static void SlingShot(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return;
				var dpoint = RetrieveDragPoint(editor.DragPointsEditor, command.userData);
				if (dpoint != null)
				{
					editor.DragPointsEditor.PrepareUndo("Changing DragPoint IsSlingshot");
					dpoint.IsSlingshot = !dpoint.IsSlingshot;
				}
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSlingshot", true)]
			private static bool SlingshotValidate(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return false;
				var dpoint = RetrieveDragPoint(editor.DragPointsEditor, command.userData);
				if (dpoint != null)
				{
					Menu.SetChecked(CONTROLPOINTS_MENUPATH + "/IsSlingshot", dpoint.IsSlingshot);
				}

				return true;
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSmooth", false, 1)]
			private static void Smooth(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return;
				var dpoint = RetrieveDragPoint(editor.DragPointsEditor, command.userData);
				if (dpoint != null)
				{
					editor.DragPointsEditor.PrepareUndo("Changing DragPoint IsSmooth");
					dpoint.IsSmooth = !dpoint.IsSmooth;
				}
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSmooth", true)]
			private static bool SmoothValidate(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return false;
				var dpoint = RetrieveDragPoint(editor.DragPointsEditor, command.userData);
				if (dpoint != null)
				{
					Menu.SetChecked(CONTROLPOINTS_MENUPATH + "/IsSmooth", dpoint.IsSmooth);
				}

				return true;
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/Properties", false, 101)]
			private static void Properties(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return;
				var cpoint = editor.DragPointsEditor.GetControlPoint(command.userData);
				if (cpoint != null)
				{
					editor.DragPointsEditor.PrepareUndo("Opening Dragpoint Properties");
					DragPointEditorWindow.ShowDragPointEditorWindow(editor.DragPointsEditor, cpoint.DragPoint, cpoint.ScrPos);
				}
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/Remove Point", false, 201)]
			private static void RemoveDP(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return;

				if (EditorUtility.DisplayDialog("DragPoint Removal", "Are you sure you want to remove this Dragpoint ?", "Yes", "No"))
				{
					editor.DragPointsEditor.RemoveDragPoint(command.userData);
				}
			}

			//Curve Traveller
			[MenuItem(CURVETRAVELLER_MENUPATH + "/Add Point")]
			static void AddDP(MenuCommand command)
			{
				ItemInspector editor = command.context as ItemInspector;
				if (editor == null || editor.DragPointsEditor == null)
					return;

				editor.DragPointsEditor.AddDragPointOnTraveller();
			}
		}

		//Dragpoint Edition
		class DragPointEditorWindow : EditorWindow
		{
			public static void ShowDragPointEditorWindow(DragPointsEditor editor, DragPointData dpdata, Vector2 pos)
			{
				DragPointEditorWindow window = GetWindow(typeof(DragPointEditorWindow)) as DragPointEditorWindow;
				var scrPos = GUIUtility.GUIToScreenPoint(pos);
				window.position = new Rect(scrPos.x, scrPos.y, 0, 0);
				window._editor = editor;
				window._dragPointData = dpdata;
				window.Show();
			}

			void OnGUI()
			{
				EditorGUI.BeginChangeCheck();

				GUILayout.BeginHorizontal("TextureCoord");
				GUILayout.Label("Texture Coord");
				_dragPointData.TextureCoord = GUILayout.HorizontalSlider(_dragPointData.TextureCoord, 0.0f, 1.0f);
				GUILayout.EndHorizontal();

				if (EditorGUI.EndChangeCheck())
				{
					SceneView.RepaintAll();
				}
			}

			public DragPointsEditor _editor { get; set; }
			public DragPointData _dragPointData { get; set; }
		}

		public DragPointData GetDragPoint(int controlId)
		{
			var cpoint = _controlPoints.Find(cp => cp.ControlId == controlId);
			if (cpoint != null)
			{
				return cpoint.DragPoint;
			}
			return null;
		}

		public ControlPoint GetControlPoint(int controlId)
		{
			return _controlPoints.Find(cp => cp.ControlId == controlId);
		}

		public void OnInspectorGUI(Object target)
		{
			_target = target;
			IDragPointsEditable dpeditable = _target as IDragPointsEditable;
			if (dpeditable == null) return;

			string enabledString = dpeditable.DragPointEditEnabled ? "(ON)" : "(OFF)";
			if (GUILayout.Button($"Edit Drag Points {enabledString}")) {
				dpeditable.DragPointEditEnabled = !dpeditable.DragPointEditEnabled;
				SceneView.RepaintAll();
			}
		}

		protected void RebuildControlPoints(IDragPointsEditable dpEditable)
		{
			_controlPoints.Clear();

			for (int i = 0; i < dpEditable.GetDragPoints().Length; ++i)
			{
				_controlPoints.Add(new ControlPoint(ref dpEditable.GetDragPoints()[i], GUIUtility.GetControlID(FocusType.Passive)));
			}

			_positionHandleControlId = GUIUtility.GetControlID(FocusType.Passive);
			_curveTravellerControlId = GUIUtility.GetControlID(FocusType.Passive);
		}

		public void PrepareUndo(string message)
		{
			if (_target == null)
				return;

			_needMeshRebuilt = true;

			Undo.RecordObject(_target as Behaviour, message);
		}

		public void AddDragPointOnTraveller()
		{
			IDragPointsEditable dpeditable = _target as IDragPointsEditable;
			Behaviour bh = _target as Behaviour;
			if (dpeditable == null || bh == null)
				return;

			if (_curveTravellerControlPointIdx < 0 || _curveTravellerControlPointIdx >= _controlPoints.Count)
				return;

			PrepareUndo("Adding Drag Point at position " + _curveTravellerPosition.ToString());

			//compute ratio between the two control points
			var cp0 = _controlPoints[_curveTravellerControlPointIdx];
			var cp1 = _controlPoints[_curveTravellerControlPointIdx+1];
			Vector3 segment = cp1.WorldPos - cp0.WorldPos;
			float ratio = segment.magnitude > 0.0f ? (_curveTravellerPosition - cp0.WorldPos).magnitude / segment.magnitude : 0.0f;

			List<DragPointData> dpoints = new List<DragPointData>(dpeditable.GetDragPoints());
			DragPointData dpoint = new DragPointData(dpeditable.GetDragPoints()[_curveTravellerControlPointIdx]);
			dpoint.IsLocked = false;
			dpoint.CalcHeight = cp0.DragPoint.CalcHeight + ((cp1.DragPoint.CalcHeight - cp0.DragPoint.CalcHeight) * ratio);

			Vector3 offset = dpeditable.GetEditableOffset();
			Vector3 dpos = bh.transform.worldToLocalMatrix.MultiplyPoint(_curveTravellerPosition);
			dpos -= offset;
			dpos.z -= dpoint.CalcHeight;
			dpoint.Vertex = dpos.ToVertex3D();

			dpoints.Insert(_curveTravellerControlPointIdx + 1, dpoint);
			dpeditable.SetDragPoints(dpoints.ToArray());
		}

		public void RemoveDragPoint(int controlId)
		{
			IDragPointsEditable dpeditable = _target as IDragPointsEditable;
			if (dpeditable == null)
				return;
			var idx = _controlPoints.FindIndex(cpoint => cpoint.ControlId == controlId);
			if (idx >= 0)
			{
				bool removalOK = !_controlPoints[idx].DragPoint.IsLocked;
				if (!removalOK)
				{
					removalOK = EditorUtility.DisplayDialog("Locked DragPoint Removal", "This Dragpoint is Locked !!\nAre you really sure you want to remove it ?", "Yes", "No");
				}

				if (removalOK)
				{
					PrepareUndo("Removing Drag Point");
					List<DragPointData> dpoints = new List<DragPointData>(dpeditable.GetDragPoints());
					dpoints.RemoveAt(idx);
					dpeditable.SetDragPoints(dpoints.ToArray());
				}
			}
		}

		public void OnSceneGUI(Object target)
		{
			_target = target;
			IEditableItemBehavior editable = _target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = _target as IDragPointsEditable;
			Behaviour bh = _target as Behaviour;

			if (bh == null || dpeditable == null || !dpeditable.DragPointEditEnabled)
				return;

			if (_controlPoints.Count != dpeditable.GetDragPoints().Length)
				RebuildControlPoints(dpeditable);

			Vector3 offset = dpeditable.GetEditableOffset();
			Matrix4x4 lwMat = bh.transform.localToWorldMatrix;
			Matrix4x4 wlMat = bh.transform.worldToLocalMatrix;

			switch (Event.current.type)
			{
				case EventType.Layout:
					{
						_selectedCP.Clear();
						//Setup Screen positions & controlID for controlpoints (in case of modification of dragpoints ccordinates outside)
						foreach (var cpoint in _controlPoints)
						{
							cpoint.WorldPos = cpoint.DragPoint.Vertex.ToUnityVector3();
							cpoint.WorldPos.z += cpoint.DragPoint.CalcHeight;
							cpoint.WorldPos += offset;
							cpoint.WorldPos = lwMat.MultiplyPoint(cpoint.WorldPos);
							cpoint.ScrPos = Handles.matrix.MultiplyPoint(cpoint.WorldPos);
							if (cpoint.IsSelected)
							{
								if (cpoint.DragPoint.IsLocked)
									cpoint.IsSelected = false;
								else
									_selectedCP.Add(cpoint);
							}
							HandleUtility.AddControl(cpoint.ControlId, HandleUtility.DistanceToCircle(cpoint.ScrPos, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius * 0.5f));
						}

						//Setup PositionHandle if some control points are selected
						if (_selectedCP.Count > 0)
						{
							_positionHandlePosition = Vector3.zero;
							foreach (var sCp in _selectedCP)
							{
								_positionHandlePosition += sCp.WorldPos;
							}
							_positionHandlePosition /= _selectedCP.Count;
						}

						if (_curveTravellerVisible)
							HandleUtility.AddControl(_curveTravellerControlId, HandleUtility.DistanceToCircle(Handles.matrix.MultiplyPoint(_curveTravellerPosition), HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio * 0.5f));
					}
					break;

				case EventType.MouseDown:
					{
						if (Event.current.button == 0)
						{
							var nearCP = _controlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);
							if (nearCP != null && !nearCP.DragPoint.IsLocked)
							{
								if (Event.current.clickCount > 1)
								{
									foreach(var cpoint in _controlPoints)
									{
										cpoint.IsSelected = false;
									}
									nearCP.IsSelected = true;
								}
								else
								{
									nearCP.IsSelected = !nearCP.IsSelected;
								}
								Event.current.Use();
							}
						}
						else if (Event.current.button == 1)
						{
							var nearCP = _controlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);
							if (nearCP != null)
							{
								MenuCommand command = new MenuCommand(_itemInspector, nearCP.ControlId);
								EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), MenuItems.CONTROLPOINTS_MENUPATH, command);
								Event.current.Use();
							}
							else if (HandleUtility.nearestControl == _curveTravellerControlId)
							{
								MenuCommand command = new MenuCommand(_itemInspector, 0);
								EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), MenuItems.CURVETRAVELLER_MENUPATH, command);
								Event.current.Use();
							}
						}
					}
					break;

				case EventType.Repaint:
					{
						_curveTravellerVisible = false;
					}
					break;
			}

			//Handle the common position handler for all selected control points
			if (_selectedCP.Count > 0)
			{
				EditorGUI.BeginChangeCheck();
				Vector3 newHandlePos = Handles.PositionHandle(_positionHandlePosition, Quaternion.identity);
				if (EditorGUI.EndChangeCheck())
				{
					PrepareUndo("Change DragPoint Position for " + _selectedCP.Count + " Control points.");

					Vector3 deltaPosition = newHandlePos - _positionHandlePosition;
					foreach (var cpoint in _selectedCP)
					{
						cpoint.WorldPos += deltaPosition;
						Vector3 dpos = wlMat.MultiplyPoint(cpoint.WorldPos);
						dpos -= offset;
						dpos.z -= cpoint.DragPoint.CalcHeight;
						cpoint.DragPoint.Vertex = dpos.ToVertex3D();
					}
				}
			}

			//Display Curve & handle curvetraveller
			if (_controlPoints.Count > 3)
			{
				List<DragPointData> transformedDPoints = new List<DragPointData>();
				foreach(var cpoint in _controlPoints)
				{
					DragPointData newDp = new DragPointData(cpoint.DragPoint);
					newDp.Vertex = cpoint.WorldPos.ToVertex3D();
					transformedDPoints.Add(newDp);
				}

				var cross = transformedDPoints[1].Vertex.Clone().Sub(transformedDPoints[0].Vertex).Cross(transformedDPoints[2].Vertex.Clone().Sub(transformedDPoints[0].Vertex));
				var areaSq = cross.LengthSq();
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(transformedDPoints.ToArray(), dpeditable.PointsAreLooping(), areaSq * 0.000001f);

				if (vVertex.Length > 0)
				{
					float width = 10.0f;
					Handles.color = UnityEngine.Color.blue;
					_pathPoints.Clear();
					foreach (RenderVertex3D v in vVertex)
					{
						_pathPoints.Add(new Vector3(v.X, v.Y, v.Z));
					}
					Handles.DrawAAPolyLine(width, _pathPoints.ToArray());
				}

				//World Position of the curve traveller
				_curveTravellerPosition = HandleUtility.ClosestPointToPolyLine(_pathPoints.ToArray());

				//Render Control Points and check traveler distance from CP
				float distToCPoint = Mathf.Infinity;
				for (int i = 0; i < _controlPoints.Count; ++i)
				{
					var cpoint = _controlPoints[i];
					Handles.color = cpoint.DragPoint.IsLocked ? UnityEngine.Color.red : (cpoint.IsSelected ? UnityEngine.Color.green : UnityEngine.Color.gray);
					Handles.SphereHandleCap(0, cpoint.WorldPos, Quaternion.identity, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius, EventType.Repaint);
					float decal = (HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius * 0.1f);
					Handles.Label(cpoint.WorldPos - Vector3.right * decal + Vector3.forward * decal  * 2.0f, i.ToString());
					float dist = Vector3.Distance(_curveTravellerPosition, cpoint.WorldPos);
					distToCPoint = Mathf.Min(distToCPoint, dist);
				}

				_curveTravellerControlPointIdx = -1;

				if (distToCPoint > HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius)
				{
					//Find the surrounding control points for the traveller
					int curCPIdx = 0;
					for (int i = 0; i < _pathPoints.Count - 1; ++i)
					{
						Vector3 p0 = _pathPoints[i];
						Vector3 p1 = _pathPoints[i + 1];
						if (curCPIdx < _controlPoints.Count && p0 == _controlPoints[curCPIdx].WorldPos)
							curCPIdx++;
						Vector3 seg = p1 - p0;
						Vector3 tSeg = _curveTravellerPosition - p0;
						float dot = Vector3.Dot(seg.normalized, tSeg.normalized);
						Vector3 projectedTraveller = Vector3.Project(tSeg, seg);
						if (dot >= 0.999999f && projectedTraveller.magnitude <= seg.magnitude)
						{
							_curveTravellerControlPointIdx = curCPIdx - 1;
							break;
						}
					}

					SceneView.RepaintAll();
					Handles.color = UnityEngine.Color.grey;
					Handles.SphereHandleCap(_curveTravellerControlId, _curveTravellerPosition, Quaternion.identity, HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio, EventType.Repaint);
					_curveTravellerVisible = true;
				}
			}

			if (_needMeshRebuilt && Event.current.type == EventType.Repaint)
			{
				//Set Meshdirty to true there so it'll trigger again after Undo
				if (editable != null)
					editable.MeshDirty = true;
				_needMeshRebuilt = false;
			}
		}
	}
}
