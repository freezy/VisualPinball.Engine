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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Engine.Math;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class MagnetDetectionWindow : EditorWindow
	{
		private const string WindowTitle = "Detect Magnets";
		private static readonly Regex NewDeviceRegex = new(@"^\s*Set\s+(?<name>[A-Za-z_]\w*)\s*=\s*New\s+(?<type>cvpmMagnet|cvpmTurntable)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex WithRegex = new(@"^\s*With\s+(?<name>[A-Za-z_]\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex EndWithRegex = new(@"^\s*End\s+With\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex InitMagnetRegex = new(@"(?<name>[A-Za-z_]\w*)\.InitMagnet\s+(?<trigger>[A-Za-z_]\w*)\s*,\s*(?<strength>[-+]?\d+(?:\.\d+)?|[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex InitTurntableRegex = new(@"(?<name>[A-Za-z_]\w*)\.InitTurnTable\s+(?<trigger>[A-Za-z_]\w*)\s*,\s*(?<strength>[-+]?\d+(?:\.\d+)?|[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex GrabCenterRegex = new(@"(?<name>[A-Za-z_]\w*)\.GrabCenter\s*=\s*(?<value>True|False|0|1)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex SolenoidRegex = new(@"(?<name>[A-Za-z_]\w*)\.Solenoid\s*=\s*(?<coil>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex SolCallbackRegex = new(@"SolCallback\s*\(\s*(?<coil>[^)]+)\s*\)\s*=\s*""(?<name>[A-Za-z_]\w*)\.(?<member>MagnetOn|MotorOn|SpinCW)\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex CreateEventsRegex = new(@"(?<name>[A-Za-z_]\w*)\.CreateEvents\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex BallTrackingRegex = new(@"(?<name>[A-Za-z_]\w*)\.(AddBall|RemoveBall)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex StrengthAssignmentRegex = new(@"(?<name>[A-Za-z_]\w*)\.Strength\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		[SerializeField] private TableComponent _tableComponent;
		[SerializeField] private Object _scriptAsset;
		[SerializeField] private string _scriptPath = string.Empty;

		private readonly List<DetectionCandidate> _candidates = new();
		private Vector2 _scroll;
		private string _status = string.Empty;

		[MenuItem("Pinball/Tools/Detect Magnets", false, 412)]
		public static void ShowWindow()
		{
			var window = GetWindow<MagnetDetectionWindow>();
			window.titleContent = new GUIContent(WindowTitle);
			window.TryUseSelectedTable();
			window.Show();
		}

		private void OnEnable()
		{
			titleContent = new GUIContent(WindowTitle);
			if (!_tableComponent) {
				TryUseSelectedTable();
			}
		}

		private void OnGUI()
		{
			EditorGUILayout.Space(6f);
			_tableComponent = (TableComponent)EditorGUILayout.ObjectField("Table", _tableComponent, typeof(TableComponent), true);
			_scriptAsset = EditorGUILayout.ObjectField("Script Asset", _scriptAsset, typeof(Object), false);

			using (new EditorGUILayout.HorizontalScope()) {
				_scriptPath = EditorGUILayout.TextField("Script Path", _scriptPath);
				if (GUILayout.Button("Browse", GUILayout.Width(72))) {
					var path = EditorUtility.OpenFilePanel("Select VPX Table Script", Application.dataPath, "vbs");
					if (!string.IsNullOrEmpty(path)) {
						_scriptPath = path;
						_scriptAsset = null;
					}
				}
			}

			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button("Use Selection", GUILayout.Width(110))) {
					TryUseSelectedTable();
				}
				if (GUILayout.Button("Scan", GUILayout.Width(80))) {
					Scan();
				}
				using (new EditorGUI.DisabledScope(_candidates.All(c => !c.Selected || !c.CanCreate))) {
					if (GUILayout.Button("Create Selected", GUILayout.Width(120))) {
						CreateSelected();
					}
				}
			}

			if (!string.IsNullOrEmpty(_status)) {
				EditorGUILayout.HelpBox(_status, MessageType.Info);
			}

			EditorGUILayout.Space(6f);
			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			foreach (var candidate in _candidates) {
				DrawCandidate(candidate);
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawCandidate(DetectionCandidate candidate)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
				using (new EditorGUILayout.HorizontalScope()) {
					candidate.Selected = EditorGUILayout.Toggle(candidate.Selected, GUILayout.Width(18));
					EditorGUILayout.LabelField($"{candidate.TypeLabel}: {candidate.Name}", EditorStyles.boldLabel);
					EditorGUILayout.LabelField($"line {candidate.Line}", GUILayout.Width(62));
				}

				EditorGUILayout.LabelField("Trigger", candidate.TriggerName);
				EditorGUILayout.LabelField("Strength", candidate.Strength.ToString(CultureInfo.InvariantCulture));
				EditorGUILayout.LabelField("Grab", candidate.GrabBall ? "Yes" : "No");
				if (candidate.Kind == DetectionKind.Magnet) {
					EditorGUILayout.LabelField("Coil", string.IsNullOrEmpty(candidate.CoilId) ? "None" : candidate.CoilId);
				} else {
					EditorGUILayout.LabelField("Motor Coil", string.IsNullOrEmpty(candidate.MotorCoilId) ? "None" : candidate.MotorCoilId);
					EditorGUILayout.LabelField("Direction Coil", string.IsNullOrEmpty(candidate.DirectionCoilId) ? "None" : candidate.DirectionCoilId);
				}

				foreach (var note in candidate.Notes) {
					EditorGUILayout.HelpBox(note, MessageType.Warning);
				}
				if (!candidate.CanCreate) {
					EditorGUILayout.HelpBox(candidate.BlockReason, MessageType.Info);
				}
			}
		}

		private void Scan()
		{
			_candidates.Clear();
			_status = string.Empty;

			if (!_tableComponent) {
				_status = "Select a table before scanning.";
				return;
			}

			if (!TryReadScript(out var script, out var error)) {
				_status = error;
				return;
			}

			_candidates.AddRange(Parse(script));
			foreach (var candidate in _candidates) {
				ResolveCandidate(candidate);
			}
			_status = _candidates.Count == 0
				? "No cvpmMagnet or cvpmTurntable instances found."
				: $"Found {_candidates.Count} magnet-related script candidate(s).";
		}

		private void ResolveCandidate(DetectionCandidate candidate)
		{
			if (candidate.Kind == DetectionKind.Turntable) {
				candidate.Trigger = FindTrigger(candidate.TriggerName);
				if (!candidate.Trigger) {
					candidate.Selected = false;
					candidate.BlockReason = $"Trigger \"{candidate.TriggerName}\" was not found in the selected table.";
				}
				return;
			}

			candidate.Trigger = FindTrigger(candidate.TriggerName);
			if (!candidate.Trigger) {
				candidate.Selected = false;
				candidate.BlockReason = $"Trigger \"{candidate.TriggerName}\" was not found in the selected table.";
			}
		}

		private void CreateSelected()
		{
			var created = new List<Object>();
			Undo.IncrementCurrentGroup();
			Undo.SetCurrentGroupName("Create detected magnets");
			var undoGroup = Undo.GetCurrentGroup();
			Undo.RecordObject(_tableComponent, "Create detected magnets");

			foreach (var candidate in _candidates.Where(c => c.Selected && c.CanCreate)) {
				var createdObject = candidate.Kind == DetectionKind.Turntable
					? CreateTurntable(candidate)?.gameObject
					: CreateMagnet(candidate)?.gameObject;
				if (createdObject) {
					created.Add(createdObject);
				}
			}

			EditorUtility.SetDirty(_tableComponent);
			EditorSceneManager.MarkSceneDirty(_tableComponent.gameObject.scene);
			Undo.CollapseUndoOperations(undoGroup);

			if (created.Count > 0) {
				Selection.objects = created.ToArray();
			}
			_status = $"Created {created.Count} magnet(s).";
		}

		private MagnetComponent CreateMagnet(DetectionCandidate candidate)
		{
			var trigger = candidate.Trigger;
			if (!trigger) {
				return null;
			}

			var parent = trigger.transform.parent;
			var name = GameObjectUtility.GetUniqueNameForSibling(parent, candidate.Name);
			var go = new GameObject(name);
			Undo.RegisterCreatedObjectUndo(go, "Create detected magnet");
			go.transform.SetParent(parent, false);
			go.transform.localPosition = trigger.transform.localPosition;
			go.transform.localRotation = trigger.transform.localRotation;
			go.transform.localScale = Vector3.one;

			var magnet = Undo.AddComponent<MagnetComponent>(go);
			Undo.RecordObject(magnet, "Configure detected magnet");
			magnet.Radius = VpxRadiusToMillimeters(GetTriggerRadius(trigger));
			magnet.Strength = candidate.Strength;
			magnet.ForceProfile = MagnetForceProfile.VpxCompatible;
			magnet.GrabBall = candidate.GrabBall;
			magnet.IsEnabledOnStart = false;

			if (!string.IsNullOrEmpty(candidate.CoilId) && !HasCoilMapping(candidate.CoilId, magnet)) {
				_tableComponent.MappingConfig.AddCoil(new CoilMapping {
					Id = candidate.CoilId,
					Description = $"{candidate.Name} magnet",
					Destination = CoilDestination.Playfield,
					Device = magnet,
					DeviceItem = MagnetComponent.MagnetCoilItem
				});
			}

			EditorUtility.SetDirty(magnet);
			return magnet;
		}

		private TurntableComponent CreateTurntable(DetectionCandidate candidate)
		{
			var trigger = candidate.Trigger;
			if (!trigger) {
				return null;
			}

			var parent = trigger.transform.parent;
			var name = GameObjectUtility.GetUniqueNameForSibling(parent, candidate.Name);
			var go = new GameObject(name);
			Undo.RegisterCreatedObjectUndo(go, "Create detected turntable");
			go.transform.SetParent(parent, false);
			go.transform.localPosition = trigger.transform.localPosition;
			go.transform.localRotation = trigger.transform.localRotation;
			go.transform.localScale = Vector3.one;

			var turntable = Undo.AddComponent<TurntableComponent>(go);
			Undo.RecordObject(turntable, "Configure detected turntable");
			turntable.Radius = VpxRadiusToMillimeters(GetTriggerRadius(trigger));
			turntable.MaxSpeed = candidate.Strength;
			turntable.MotorOnStart = false;
			turntable.SpinClockwise = true;

			AddTurntableCoilMapping(candidate.MotorCoilId, turntable, TurntableComponent.MotorCoilItem, $"{candidate.Name} motor");
			AddTurntableCoilMapping(candidate.DirectionCoilId, turntable, TurntableComponent.DirectionCoilItem, $"{candidate.Name} direction");

			EditorUtility.SetDirty(turntable);
			return turntable;
		}

		private void AddTurntableCoilMapping(string coilId, TurntableComponent turntable, string deviceItem, string description)
		{
			if (string.IsNullOrEmpty(coilId) || HasCoilMapping(coilId, turntable, deviceItem)) {
				return;
			}
			_tableComponent.MappingConfig.AddCoil(new CoilMapping {
				Id = coilId,
				Description = description,
				Destination = CoilDestination.Playfield,
				Device = turntable,
				DeviceItem = deviceItem
			});
		}

		private bool HasCoilMapping(string coilId, MagnetComponent magnet)
		{
			return _tableComponent.MappingConfig.Coils.Any(mapping =>
				string.Equals(mapping.Id, coilId, StringComparison.OrdinalIgnoreCase) &&
				mapping.Device is MagnetComponent mappedMagnet &&
				mappedMagnet == magnet &&
				mapping.DeviceItem == MagnetComponent.MagnetCoilItem);
		}

		private bool HasCoilMapping(string coilId, TurntableComponent turntable, string deviceItem)
		{
			return _tableComponent.MappingConfig.Coils.Any(mapping =>
				string.Equals(mapping.Id, coilId, StringComparison.OrdinalIgnoreCase) &&
				mapping.Device is TurntableComponent mappedTurntable &&
				mappedTurntable == turntable &&
				mapping.DeviceItem == deviceItem);
		}

		private TriggerComponent FindTrigger(string triggerName)
		{
			return _tableComponent.GetComponentsInChildren<TriggerComponent>(true)
				.FirstOrDefault(trigger => string.Equals(trigger.name, triggerName, StringComparison.OrdinalIgnoreCase));
		}

		private static float GetTriggerRadius(TriggerComponent trigger)
		{
			var collider = trigger.GetComponentInChildren<TriggerColliderComponent>(true);
			if (collider && collider.HitCircleRadius > 0f) {
				return collider.HitCircleRadius;
			}

			var maxRadius = 25f;
			foreach (var dragPoint in trigger.DragPoints ?? Array.Empty<DragPointData>()) {
				maxRadius = math.max(maxRadius, math.length(new float2(dragPoint.Center.X, dragPoint.Center.Y)));
			}
			return maxRadius;
		}

		private static float VpxRadiusToMillimeters(float radiusVpx)
			=> VisualPinball.Unity.Physics.ScaleToWorld(radiusVpx) / MagnetComponent.MillimetersToWorld;

		private bool TryReadScript(out string script, out string error)
		{
			var path = ResolveScriptPath();
			if (string.IsNullOrEmpty(path)) {
				script = string.Empty;
				error = "Select or browse to a VPX table script.";
				return false;
			}
			if (!File.Exists(path)) {
				script = string.Empty;
				error = $"Script file does not exist: {path}";
				return false;
			}
			script = File.ReadAllText(path);
			error = string.Empty;
			return true;
		}

		private string ResolveScriptPath()
		{
			if (_scriptAsset) {
				var assetPath = AssetDatabase.GetAssetPath(_scriptAsset);
				if (!string.IsNullOrEmpty(assetPath)) {
					_scriptPath = Path.GetFullPath(assetPath);
				}
			}
			return _scriptPath;
		}

		private void TryUseSelectedTable()
		{
			if (!Selection.activeGameObject) {
				return;
			}
			_tableComponent = Selection.activeGameObject.GetComponentInParent<TableComponent>();
		}

		private static IEnumerable<DetectionCandidate> Parse(string script)
		{
			var devices = new Dictionary<string, ParsedDevice>(StringComparer.OrdinalIgnoreCase);
			string withTarget = null;
			var lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

			for (var i = 0; i < lines.Length; i++) {
				var lineNumber = i + 1;
				var line = StripComment(lines[i]).Trim();
				if (line.Length == 0) {
					continue;
				}

				var withMatch = WithRegex.Match(line);
				if (withMatch.Success) {
					withTarget = withMatch.Groups["name"].Value;
					continue;
				}
				if (EndWithRegex.IsMatch(line)) {
					withTarget = null;
					continue;
				}
				if (withTarget != null && line.StartsWith(".", StringComparison.Ordinal)) {
					line = withTarget + line;
				}

				ReadNewDevice(line, lineNumber, devices);
				ReadInit(line, lineNumber, devices);
				ReadGrab(line, devices);
				ReadSolenoid(line, lineNumber, devices);
				ReadCreateEvents(line, devices);
				ReadBallTracking(line, lineNumber, devices);
				ReadStrengthMutation(line, lineNumber, devices);
			}

			return devices.Values
				.Where(device => device.HasInit)
				.Select(device => device.ToCandidate())
				.OrderBy(candidate => candidate.Line)
				.ToList();
		}

		private static void ReadNewDevice(string line, int lineNumber, Dictionary<string, ParsedDevice> devices)
		{
			var match = NewDeviceRegex.Match(line);
			if (!match.Success) {
				return;
			}
			var device = GetDevice(devices, match.Groups["name"].Value);
			device.Kind = ParseKind(match.Groups["type"].Value);
			device.Line = lineNumber;
		}

		private static void ReadInit(string line, int lineNumber, Dictionary<string, ParsedDevice> devices)
		{
			var magnet = InitMagnetRegex.Match(line);
			if (magnet.Success) {
				var device = GetDevice(devices, magnet.Groups["name"].Value);
				device.Kind = DetectionKind.Magnet;
				device.TriggerName = magnet.Groups["trigger"].Value;
				device.Strength = ParseStrength(device, magnet.Groups["strength"].Value, 10f, lineNumber);
				device.Line = device.Line == 0 ? lineNumber : device.Line;
				device.HasInit = true;
				return;
			}

			var turntable = InitTurntableRegex.Match(line);
			if (turntable.Success) {
				var device = GetDevice(devices, turntable.Groups["name"].Value);
				device.Kind = DetectionKind.Turntable;
				device.TriggerName = turntable.Groups["trigger"].Value;
				device.Strength = ParseStrength(device, turntable.Groups["strength"].Value, 100f, lineNumber);
				device.Line = device.Line == 0 ? lineNumber : device.Line;
				device.HasInit = true;
			}
		}

		private static void ReadGrab(string line, Dictionary<string, ParsedDevice> devices)
		{
			var match = GrabCenterRegex.Match(line);
			if (match.Success) {
				var device = GetDevice(devices, match.Groups["name"].Value);
				device.GrabCenter = IsTruthy(match.Groups["value"].Value);
				device.HasExplicitGrab = true;
			}
		}

		private static void ReadSolenoid(string line, int lineNumber, Dictionary<string, ParsedDevice> devices)
		{
			var solenoid = SolenoidRegex.Match(line);
			if (solenoid.Success) {
				var device = GetDevice(devices, solenoid.Groups["name"].Value);
				device.CoilId = solenoid.Groups["coil"].Value;
				return;
			}

			var callback = SolCallbackRegex.Match(line);
			if (!callback.Success) {
				return;
			}

			var callbackDevice = GetDevice(devices, callback.Groups["name"].Value);
			var coil = callback.Groups["coil"].Value.Trim();
			if (int.TryParse(coil, NumberStyles.Integer, CultureInfo.InvariantCulture, out var coilId)) {
				callbackDevice.AssignCallbackCoil(callback.Groups["member"].Value, coilId.ToString(CultureInfo.InvariantCulture));
			} else {
				callbackDevice.Notes.Add($"Line {lineNumber}: solenoid callback uses expression \"{coil}\"; map the coil manually.");
			}
		}

		private static void ReadCreateEvents(string line, Dictionary<string, ParsedDevice> devices)
		{
			var match = CreateEventsRegex.Match(line);
			if (match.Success) {
				GetDevice(devices, match.Groups["name"].Value).HasCreateEvents = true;
			}
		}

		private static void ReadBallTracking(string line, int lineNumber, Dictionary<string, ParsedDevice> devices)
		{
			var match = BallTrackingRegex.Match(line);
			if (match.Success) {
				GetDevice(devices, match.Groups["name"].Value).ManualBallLines.Add(lineNumber);
			}
		}

		private static void ReadStrengthMutation(string line, int lineNumber, Dictionary<string, ParsedDevice> devices)
		{
			var match = StrengthAssignmentRegex.Match(line);
			if (match.Success && line.IndexOf("InitMagnet", StringComparison.OrdinalIgnoreCase) < 0) {
				GetDevice(devices, match.Groups["name"].Value).StrengthMutationLines.Add(lineNumber);
			}
		}

		private static ParsedDevice GetDevice(Dictionary<string, ParsedDevice> devices, string name)
		{
			if (!devices.TryGetValue(name, out var device)) {
				device = new ParsedDevice { Name = name };
				devices.Add(name, device);
			}
			return device;
		}

		private static DetectionKind ParseKind(string type)
			=> type.Equals("cvpmTurntable", StringComparison.OrdinalIgnoreCase) ? DetectionKind.Turntable : DetectionKind.Magnet;

		/// <summary>
		/// Parses a numeric init value; scripts often pass a named constant instead
		/// (e.g. `.InitMagnet MagnaSave, kOrbMagnetPower`), which cannot be resolved
		/// here — those get the fallback plus a manual-review note.
		/// </summary>
		private static float ParseStrength(ParsedDevice device, string value, float fallback, int lineNumber)
		{
			if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) {
				return result;
			}
			device.Notes.Add($"Line {lineNumber}: init value is the script expression \"{value}\"; created with default {fallback.ToString(CultureInfo.InvariantCulture)}, adjust manually.");
			return fallback;
		}

		private static bool IsTruthy(string value)
			=> value.Equals("True", StringComparison.OrdinalIgnoreCase) || value == "1";

		private static string StripComment(string line)
		{
			var inString = false;
			for (var i = 0; i < line.Length; i++) {
				if (line[i] == '"') {
					inString = !inString;
				} else if (line[i] == '\'' && !inString) {
					return line.Substring(0, i);
				}
			}
			return line;
		}

		private enum DetectionKind
		{
			Magnet,
			Turntable
		}

		private sealed class ParsedDevice
		{
			public string Name;
			public DetectionKind Kind;
			public string TriggerName = string.Empty;
			public float Strength;
			public int Line;
			public bool HasInit;
			public bool HasCreateEvents;
			public bool HasExplicitGrab;
			public bool GrabCenter;
			public string CoilId = string.Empty;
			public string MotorCoilId = string.Empty;
			public string DirectionCoilId = string.Empty;
			public readonly List<int> ManualBallLines = new();
			public readonly List<int> StrengthMutationLines = new();
			public readonly List<string> Notes = new();

			public void AssignCallbackCoil(string member, string coilId)
			{
				if (member.Equals("MotorOn", StringComparison.OrdinalIgnoreCase)) {
					MotorCoilId = coilId;
				} else if (member.Equals("SpinCW", StringComparison.OrdinalIgnoreCase)) {
					DirectionCoilId = coilId;
				} else {
					CoilId = coilId;
				}
			}

			public DetectionCandidate ToCandidate()
			{
				var candidate = new DetectionCandidate {
					Name = Name,
					Kind = Kind,
					TriggerName = TriggerName,
					Strength = Strength,
					Line = Line,
					GrabBall = Kind == DetectionKind.Magnet && (HasExplicitGrab ? GrabCenter : Strength > 14f),
					CoilId = CoilId,
					MotorCoilId = MotorCoilId,
					DirectionCoilId = DirectionCoilId
				};
				candidate.Notes.AddRange(Notes);
				if (ManualBallLines.Count > 0 && !HasCreateEvents) {
					candidate.Notes.Add($"Manual AddBall/RemoveBall calls at line(s) {string.Join(", ", ManualBallLines)}; verify membership behavior.");
				}
				if (StrengthMutationLines.Count > 0) {
					candidate.Notes.Add($"Runtime Strength assignment at line(s) {string.Join(", ", StrengthMutationLines)}; port the script-side modulation.");
				}
				if (Kind == DetectionKind.Magnet && string.IsNullOrEmpty(CoilId)) {
					candidate.Notes.Add("No solenoid mapping found.");
				} else if (Kind == DetectionKind.Turntable) {
					if (string.IsNullOrEmpty(MotorCoilId)) {
						candidate.Notes.Add("No motor solenoid mapping found.");
					}
					if (string.IsNullOrEmpty(DirectionCoilId)) {
						candidate.Notes.Add("No direction solenoid mapping found.");
					}
				}
				return candidate;
			}
		}

		private sealed class DetectionCandidate
		{
			public bool Selected = true;
			public string Name;
			public DetectionKind Kind;
			public string TriggerName;
			public float Strength;
			public bool GrabBall;
			public string CoilId;
			public string MotorCoilId;
			public string DirectionCoilId;
			public int Line;
			public TriggerComponent Trigger;
			public string BlockReason = string.Empty;
			public readonly List<string> Notes = new();

			public bool CanCreate => Trigger && (Kind == DetectionKind.Magnet || Kind == DetectionKind.Turntable);
			public string TypeLabel => Kind == DetectionKind.Magnet ? "Magnet" : "Turntable";
		}
	}
}
