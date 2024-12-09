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


// ReSharper disable UnusedType.Global

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Playfield;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public enum IconSize
	{
		Large, Small
	}

	public enum IconColor
	{
		Gray, Green, Orange, Blue, Colored
	}

	public interface IIconLookup
	{
		Texture2D Lookup<T>(T mb, IconSize size = IconSize.Large, IconColor color = IconColor.Gray) where T : class;
		void DisableGizmoIcons();
	}

	public class Icons
	{
		private readonly struct IconVariant
		{
			public readonly string Name;
			public readonly IconSize Size;
			public readonly IconColor Color;

			public IconVariant(string name, IconSize size, IconColor color)
			{
				Name = name;
				Size = size;
				Color = color;
			}
		}

		private const string AssetLibraryName = "asset_library";
		private const string BallName = "ball";
		private const string BallRollerName = "ball_roller";
		private const string BoltName = "bolt";
		private const string BumperName = "bumper";
		private const string CalendarName = "calendar";
		private const string CannonName = "cannon";
		private const string CoilName = "coil";
		private const string DropTargetBankName = "drop_target_bank";
		private const string DropTargetName = "drop_target";
		private const string FlasherName = "light_flasher";
		private const string FlipperName = "flipper";
		private const string GateName = "gate";
		private const string GateBracketName = "gate_bracket";
		private const string GateLifterName = "gate_lifter";
		private const string HitTargetName = "hit_target";
		private const string KeyName = "keyboard";
		private const string KickerName = "kicker";
		private const string LightGroupName = "light_group";
		private const string LightName = "light";
		private const string MechName = "mech";
		private const string MechPinMameName = "mech_pinmame";
		private const string PlayfieldName = "playfield";
		private const string PlugName = "plug";
		private const string PlungerName = "plunger";
		private const string PrimitiveName = "primitive";
		private const string PhysicsName = "physics";
		private const string RampName = "ramp";
		private const string RotatorName = "rotator";
		private const string RubberName = "rubber";
		private const string ScoreReelName = "score_reel";
		private const string ScoreReelSingleName = "score_reel_single";
		private const string SlingshotName = "slingshot";
		private const string SpinnerName = "spinner";
		private const string SurfaceName = "surface";
		private const string SwitchNcName = "switch_nc";
		private const string SwitchNoName = "switch_no";
		private const string TableName = "table";
		private const string TeleporterName = "teleporter";
		private const string TriggerName = "trigger";
		private const string TroughName = "trough";
		private const string MetalWireGuideName = "metal_wire_guides";
		private const string LockedName = "locked";
		private const string UnlockedName = "unlocked";

		// colored
		private const string CoilEventName = "coil_event";
		private const string SwitchEventName = "switch_event";
		private const string LampEventName = "lamp_event";
		private const string LampSeqName = "lamp_seq";
		private const string PlayerVariableName = "player_variable";
		private const string PlayerVariableEventName = "player_variable_event";
		private const string TableVariableName = "table_variable";
		private const string TableVariableEventName = "table_variable_event";
		private const string UpdateDisplayName = "update_display";
		private const string DisplayEventName = "display_event";

		private static readonly string[] Names = {
			AssetLibraryName, BallRollerName, BallName, BoltName, BumperName, CalendarName, CannonName, CoilName, DropTargetBankName, DropTargetName, FlasherName,
			FlipperName, GateName, GateLifterName, HitTargetName, KeyName, KickerName, LightGroupName, LightName, MechName, MechPinMameName, PlayfieldName, PlugName,
			PhysicsName, PlungerName, PrimitiveName, RampName, RotatorName, RubberName, ScoreReelName, ScoreReelSingleName, SlingshotName, SpinnerName, SurfaceName,
			SwitchNcName, SwitchNoName, TableName, TeleporterName, TriggerName, TroughName,
			CoilEventName, SwitchEventName, LampEventName, LampSeqName, MetalWireGuideName,
			PlayerVariableName, PlayerVariableEventName, TableVariableName, TableVariableEventName, UpdateDisplayName, DisplayEventName,
			LockedName, UnlockedName
		};

		private readonly Dictionary<IconVariant, Texture2D> _icons = new Dictionary<IconVariant, Texture2D>();
		private static readonly MethodInfo CopyMonoScriptIconToImporters = typeof(MonoImporter).GetMethod("CopyMonoScriptIconToImporters", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly MethodInfo SetIconForObject = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly MethodInfo SetGizmoEnabled = Assembly.GetAssembly(typeof(UnityEditor.Editor))?.GetType("UnityEditor.AnnotationUtility")?.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly MethodInfo SetIconEnabled = Assembly.GetAssembly(typeof(UnityEditor.Editor))?.GetType("UnityEditor.AnnotationUtility")?.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

		// see https://docs.unity3d.com/Manual/ClassIDReference.html
		private static readonly int MonoBehaviourClassID = 114;

		private static Icons _instance;
		private static IIconLookup[] _lookups;
		private static Icons Instance => _instance ??= new Icons();
		private static IIconLookup[] Lookups => _lookups ??= GetLookups();

		private Icons()
		{
			const string iconPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Editor/Icons";
			foreach (var name in Names) {
				foreach (var size in Enum.GetValues(typeof(IconSize)).Cast<IconSize>()) {
					foreach (var color in Enum.GetValues(typeof(IconColor)).Cast<IconColor>()) {
						var variant = new IconVariant(name, size, color);
						var path = $"{iconPath}/{size.ToString().ToLower()}_{color.ToString().ToLower()}/{name}.png";
						if (File.Exists(path)) {
							_icons[variant] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
						}
					}
				}
			}
		}

		private static IIconLookup[] GetLookups() {
			var t = typeof(IIconLookup);
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(x => x.FullName.StartsWith("VisualPinball."))
				.SelectMany(x => x.GetTypes())
				.Where(x => x.IsClass && t.IsAssignableFrom(x))
				.Select(x => (IIconLookup) Activator.CreateInstance(x))
				.ToArray();
		}

		public static Texture2D AssetLibrary(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(AssetLibraryName, size, color);
		public static Texture2D Ball(IconSize size = IconSize.Small, IconColor color = IconColor.Gray) => Instance.GetItem(BallName, size, color);
		public static Texture2D BallRoller(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(BallRollerName, size, color);
		public static Texture2D Bolt(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(BoltName, size, color);
		public static Texture2D Bumper(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(BumperName, size, color);
		public static Texture2D Calendar(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(CalendarName, size, color);
		public static Texture2D Cannon(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(CannonName, size, color);
		public static Texture2D Coil(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(CoilName, size, color);
		public static Texture2D DropTarget(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(DropTargetName, size, color);
		public static Texture2D DropTargetBank(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(DropTargetBankName, size, color);
		public static Texture2D Flasher(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(FlasherName, size, color);
		public static Texture2D Flipper(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(FlipperName, size, color);
		public static Texture2D Gate(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(GateName, size, color);
		public static Texture2D GateBracket(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(GateBracketName, size, color);
		public static Texture2D GateLifter(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(GateLifterName, size, color);
		public static Texture2D HitTarget(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(HitTargetName, size, color);
		public static Texture2D Key(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(KeyName, size, color);
		public static Texture2D Kicker(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(KickerName, size, color);
		public static Texture2D Light(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(LightName, size, color);
		public static Texture2D LightGroup(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(LightGroupName, size, color);
		public static Texture2D Locked(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(LockedName, size, color);
		public static Texture2D Mech(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(MechName, size, color);
		public static Texture2D MechPinMame(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(MechPinMameName, size, color);
		public static Texture2D MetalWireGuide(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(MetalWireGuideName, size, color);
		public static Texture2D Physics(IconSize size = IconSize.Small, IconColor color = IconColor.Gray) => Instance.GetItem(PhysicsName, size, color);
		public static Texture2D Playfield(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlayfieldName, size, color);
		public static Texture2D Plug(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlugName, size, color);
		public static Texture2D Plunger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlungerName, size, color);
		public static Texture2D Primitive(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PrimitiveName, size, color);
		public static Texture2D Ramp(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RampName, size, color);
		public static Texture2D Rotator(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RotatorName, size, color);
		public static Texture2D Rubber(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RubberName, size, color);
		public static Texture2D ScoreReel(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(ScoreReelName, size, color);
		public static Texture2D ScoreReelSingle(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(ScoreReelSingleName, size, color);
		public static Texture2D Slingshot(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SlingshotName, size, color);
		public static Texture2D Spinner(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SpinnerName, size, color);
		public static Texture2D Surface(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SurfaceName, size, color);
		public static Texture2D Switch(bool isClosed, IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(isClosed ? SwitchNcName : SwitchNoName, size, color);
		public static Texture2D Table(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TableName, size, color);
		public static Texture2D Teleporter(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TeleporterName, size, color);
		public static Texture2D Trigger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TriggerName, size, color);
		public static Texture2D Trough(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TroughName, size, color);
		public static Texture2D Unlocked(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(UnlockedName, size, color);


		public static Texture2D CoilEvent => Instance.GetItem(CoilEventName, IconSize.Large, IconColor.Colored);
		public static Texture2D SwitchEvent => Instance.GetItem(SwitchEventName, IconSize.Large, IconColor.Colored);
		public static Texture2D LampEvent => Instance.GetItem(LampEventName, IconSize.Large, IconColor.Colored);
		public static Texture2D LampSequence => Instance.GetItem(LampSeqName, IconSize.Large, IconColor.Colored);
		public static Texture2D PlayerVariable => Instance.GetItem(PlayerVariableName, IconSize.Large, IconColor.Colored);
		public static Texture2D PlayerVariableEvent => Instance.GetItem(PlayerVariableEventName, IconSize.Large, IconColor.Colored);
		public static Texture2D TableVariable => Instance.GetItem(TableVariableName, IconSize.Large, IconColor.Colored);
		public static Texture2D TableVariableEvent => Instance.GetItem(TableVariableEventName, IconSize.Large, IconColor.Colored);
		public static Texture2D UpdateDisplay => Instance.GetItem(UpdateDisplayName, IconSize.Large, IconColor.Colored);
		public static Texture2D DisplayEvent => Instance.GetItem(DisplayEventName, IconSize.Large, IconColor.Colored);

		public static Texture2D ByComponent<T>(T mb, IconSize size = IconSize.Large, IconColor color = IconColor.Gray)
			where T : class
		{
			return Lookups
				.Select(lookup => lookup.Lookup(mb, size, color))
				.FirstOrDefault(icon => icon != null);
		}

		[MenuItem("Visual Pinball/Editor/Disable Gizmo Icons", false, 510)]
		public static void DisableGizmoIcons()
		{
			foreach (var lookup in Lookups) {
				lookup.DisableGizmoIcons();
			}
		}

		public static void ApplyToComponent<T>(Object target, Texture2D tex) where T : MonoBehaviour
		{
			if (target == null || tex == null) {
				throw new ArgumentNullException();
			}
			SetIconForObject.Invoke(null, new object[]{ target, tex });
			DisableGizmo<T>();

			var monoScript = target as MonoScript;
			if (monoScript) {
				CopyMonoScriptIconToImporters.Invoke(null, new object[]{ monoScript });
			}
		}

		private Texture2D GetItem(string name, IconSize size, IconColor color)
		{
			var variant = new IconVariant(name, size, color);
			if (!_icons.ContainsKey(variant)) {
				variant = new IconVariant(name, IconSize.Large, color);
			}

			if (!_icons.ContainsKey(variant)) {
				variant = new IconVariant(name, IconSize.Large, IconColor.Gray);
			}

			if (!_icons.ContainsKey(variant)) {
				throw new InvalidOperationException($"Cannot find {variant.Size} {variant.Name} icon of color {variant.Color}.");
			}

			return _icons[variant];
		}

		public static void DisableGizmo<T>() where T : MonoBehaviour
		{
			var className = typeof(T).Name;
			//SetGizmoEnabled?.Invoke(null, new object[] { MonoBehaviourClassID, className, 0, false });
			SetIconEnabled?.Invoke(null, new object[] { MonoBehaviourClassID, className, 0 });
		}
	}

	internal class IconLookup : IIconLookup
	{
		public Texture2D Lookup<T>(T mb, IconSize size = IconSize.Large, IconColor color = IconColor.Gray) where T : class
		{
			switch (mb) {
				case BallComponent _: return Icons.Ball(size, color);
				case BallRollerComponent _: return Icons.BallRoller(size, color);
				case BumperComponent _: return Icons.Bumper(size, color);
				case CannonRotatorComponent _: return Icons.Cannon(size, color);
				case DropTargetComponent _: return Icons.DropTarget(size, color);
				case DropTargetBankComponent _: return Icons.DropTargetBank(size, color);
				case FlipperComponent _: return Icons.Flipper(size, color);
				case GateComponent _: return Icons.Gate(size, color);
				case GateBracketComponent _: return Icons.GateBracket(size, color);
				case GateLifterComponent _: return Icons.GateLifter(size, color);
				case HitTargetComponent _: return Icons.HitTarget(size, color);
				case KickerComponent _: return Icons.Kicker(size, color);
				case LightComponent _: return Icons.Light(size, color);
				case LightGroupComponent _: return Icons.LightGroup(size, color);
				case PhysicsEngine _: return Icons.Physics(size, color);
				case PlungerComponent _: return Icons.Plunger(size, color);
				case PlayfieldComponent _: return Icons.Playfield(size, color);
				case PrimitiveComponent _: return Icons.Primitive(size, color);
				case RampComponent _: return Icons.Ramp(size, color);
				case RotatorComponent _: return Icons.Rotator(size, color);
				case RubberComponent _: return Icons.Rubber(size, color);
  				case ScoreMotorComponent _: return Icons.Mech(size, color);
				case ScoreReelComponent _: return Icons.ScoreReelSingle(size, color);
				case ScoreReelDisplayComponent _: return Icons.ScoreReel(size, color);
				case SpinnerComponent _: return Icons.Spinner(size, color);
				case SlingshotComponent _: return Icons.Slingshot(size, color);
				case SurfaceComponent _: return Icons.Surface(size, color);
				case StepRotatorMechComponent _: return Icons.Mech(size, color);
				case TeleporterComponent _: return Icons.Teleporter(size, color);
				case TriggerComponent _: return Icons.Trigger(size, color);
				case TroughComponent _: return Icons.Trough(size, color);
				case MetalWireGuideComponent _: return Icons.MetalWireGuide(size, color);
				case CollisionSwitchComponent _: return Icons.Switch(false, size, color);
				default: return null;
			}
		}

		public void DisableGizmoIcons()
		{
			Icons.DisableGizmo<BallComponent>();
			Icons.DisableGizmo<BallRollerComponent>();
			Icons.DisableGizmo<BumperComponent>();
			Icons.DisableGizmo<BumperColliderComponent>();
			Icons.DisableGizmo<BumperRingAnimationComponent>();
			Icons.DisableGizmo<BumperSkirtAnimationComponent>();
			Icons.DisableGizmo<CannonRotatorComponent>();
			Icons.DisableGizmo<DefaultGamelogicEngine>();
			Icons.DisableGizmo<DotMatrixDisplayComponent>();
			Icons.DisableGizmo<DropTargetComponent>();
			Icons.DisableGizmo<DropTargetBankComponent>();
			Icons.DisableGizmo<DropTargetColliderComponent>();
			Icons.DisableGizmo<DropTargetAnimationComponent>();
			//Icons.DisableGizmo<FlasherComponent>();
			Icons.DisableGizmo<FlipperComponent>();
			Icons.DisableGizmo<FlipperColliderComponent>();
			Icons.DisableGizmo<FlipperBaseMeshComponent>();
			Icons.DisableGizmo<FlipperRubberMeshComponent>();
			Icons.DisableGizmo<GateComponent>();
			Icons.DisableGizmo<GateBracketComponent>();
			Icons.DisableGizmo<GateLifterComponent>();
			Icons.DisableGizmo<GateColliderComponent>();
			Icons.DisableGizmo<GateWireAnimationComponent>();
			Icons.DisableGizmo<HitTargetComponent>();
			Icons.DisableGizmo<HitTargetColliderComponent>();
			Icons.DisableGizmo<HitTargetAnimationComponent>();
			Icons.DisableGizmo<KickerComponent>();
			Icons.DisableGizmo<KickerColliderComponent>();
			Icons.DisableGizmo<LightComponent>();
			Icons.DisableGizmo<LightGroupComponent>();
			Icons.DisableGizmo<PhysicsEngine>();
			Icons.DisableGizmo<PlayfieldComponent>();
			Icons.DisableGizmo<PlayfieldColliderComponent>();
			Icons.DisableGizmo<PlayfieldMeshComponent>();
			Icons.DisableGizmo<PlungerComponent>();
			Icons.DisableGizmo<PlungerColliderComponent>();
			Icons.DisableGizmo<PlungerFlatMeshComponent>();
			Icons.DisableGizmo<PlungerRodMeshComponent>();
			Icons.DisableGizmo<PlungerSpringMeshComponent>();
			Icons.DisableGizmo<PrimitiveComponent>();
			Icons.DisableGizmo<PrimitiveColliderComponent>();
			Icons.DisableGizmo<PrimitiveMeshComponent>();
			Icons.DisableGizmo<RampComponent>();
			Icons.DisableGizmo<RampColliderComponent>();
			Icons.DisableGizmo<RampFloorMeshComponent>();
			Icons.DisableGizmo<RampWallMeshComponent>();
			Icons.DisableGizmo<RampWireMeshComponent>();
			Icons.DisableGizmo<RotatorComponent>();
			Icons.DisableGizmo<RubberComponent>();
			Icons.DisableGizmo<RubberMeshComponent>();
			Icons.DisableGizmo<RubberColliderComponent>();
			Icons.DisableGizmo<ScoreReelComponent>();
			Icons.DisableGizmo<ScoreReelDisplayComponent>();
			Icons.DisableGizmo<ScoreMotorComponent>();
			Icons.DisableGizmo<SegmentDisplayComponent>();
			Icons.DisableGizmo<SlingshotComponent>();
			Icons.DisableGizmo<SpinnerComponent>();
			Icons.DisableGizmo<SpinnerColliderComponent>();
			Icons.DisableGizmo<SpinnerPlateAnimationComponent>();
			Icons.DisableGizmo<SpinnerBracketColliderComponent>();
			Icons.DisableGizmo<SurfaceComponent>();
			Icons.DisableGizmo<SurfaceColliderComponent>();
			Icons.DisableGizmo<SurfaceSideMeshComponent>();
			Icons.DisableGizmo<SurfaceTopMeshComponent>();
			Icons.DisableGizmo<TableComponent>();
			Icons.DisableGizmo<TeleporterComponent>();
			Icons.DisableGizmo<TriggerComponent>();
			Icons.DisableGizmo<TriggerAnimationComponent>();
			Icons.DisableGizmo<TriggerColliderComponent>();
			Icons.DisableGizmo<TriggerMeshComponent>();
			Icons.DisableGizmo<MetalWireGuideComponent>();
			Icons.DisableGizmo<MetalWireGuideMeshComponent>();
			Icons.DisableGizmo<MetalWireGuideColliderComponent>();
		}
	}
}
