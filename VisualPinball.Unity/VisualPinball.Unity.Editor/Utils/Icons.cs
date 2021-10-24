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
		Gray, Green, Orange, Blue
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

		private const string BumperName = "bumper";
		private const string BoltName = "bolt";
		private const string CoilName = "coil";
		private const string DropTargetName = "drop_target";
		private const string DropTargetBankName = "drop_target_bank";
		private const string FlasherName = "light_flasher";
		private const string FlipperName = "flipper";
		private const string GateName = "gate";
		private const string HitTargetName = "hit_target";
		private const string KeyName = "keyboard";
		private const string KickerName = "kicker";
		private const string LightGroupName = "light_group";
		private const string LightName = "light";
		private const string PlayfieldName = "playfield";
		private const string PlungerName = "plunger";
		private const string PlugName = "plug";
		private const string PrimitiveName = "primitive";
		private const string RampName = "ramp";
		private const string RubberName = "rubber";
		private const string SpinnerName = "spinner";
		private const string SurfaceName = "surface";
		private const string SlingshotName = "slingshot";
		private const string TableName = "table";
		private const string TriggerName = "trigger";
		private const string TroughName = "trough";
		private const string SwitchNcName = "switch_nc";
		private const string SwitchNoName = "switch_no";

		private static readonly string[] Names = {
			BumperName, BoltName, CoilName, DropTargetName, DropTargetBankName, FlasherName, FlipperName, HitTargetName, GateName, KeyName, KickerName,
			LightGroupName, LightName, PlayfieldName, PlungerName, PlugName, PrimitiveName, RampName, RubberName, SpinnerName, SurfaceName, TableName,
			TriggerName, TroughName, SlingshotName, SwitchNcName, SwitchNoName
		};

		private readonly Dictionary<IconVariant, Texture2D> _icons = new Dictionary<IconVariant, Texture2D>();
		private static readonly MethodInfo CopyMonoScriptIconToImporters = typeof(MonoImporter).GetMethod("CopyMonoScriptIconToImporters", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly MethodInfo SetIconForObject = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly MethodInfo SetGizmoEnabled = Assembly.GetAssembly(typeof(UnityEditor.Editor))?.GetType("UnityEditor.AnnotationUtility")?.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly MethodInfo SetIconEnabled = Assembly.GetAssembly(typeof(UnityEditor.Editor))?.GetType("UnityEditor.AnnotationUtility")?.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

		// see https://docs.unity3d.com/Manual/ClassIDReference.html
		private static readonly int MonoBehaviourClassID = 114;

		private static Icons _instance;
		private static Icons Instance => _instance ??= new Icons();

		private Icons()
		{
			const string iconPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Editor/Icons";
			foreach (var name in Names)
			{
				foreach (var size in Enum.GetValues(typeof(IconSize)).Cast<IconSize>())
				{
					foreach (var color in Enum.GetValues(typeof(IconColor)).Cast<IconColor>())
					{
						var variant = new IconVariant(name, size, color);
						var path = $"{iconPath}/{size.ToString().ToLower()}_{color.ToString().ToLower()}/{name}.png";
						if (File.Exists(path))
						{
							_icons[variant] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
						}
					}
				}
			}
		}

		public static Texture2D Bumper(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(BumperName, size, color);
		public static Texture2D DropTarget(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(DropTargetName, size, color);
		public static Texture2D DropTargetBank(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(DropTargetBankName, size, color);
		public static Texture2D Flasher(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(FlasherName, size, color);
		public static Texture2D Flipper(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(FlipperName, size, color);
		public static Texture2D Gate(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(GateName, size, color);
		public static Texture2D HitTarget(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(HitTargetName, size, color);
		public static Texture2D Kicker(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(KickerName, size, color);
		public static Texture2D Light(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(LightName, size, color);
		public static Texture2D LightGroup(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(LightGroupName, size, color);
		public static Texture2D Playfield(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlayfieldName, size, color);
		public static Texture2D Plunger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlungerName, size, color);
		public static Texture2D Plug(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlugName, size, color);
		public static Texture2D Primitive(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PrimitiveName, size, color);
		public static Texture2D Ramp(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RampName, size, color);
		public static Texture2D Rubber(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RubberName, size, color);
		public static Texture2D Spinner(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SpinnerName, size, color);
		public static Texture2D Surface(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SurfaceName, size, color);
		public static Texture2D Table(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TableName, size, color);
		public static Texture2D Trigger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TriggerName, size, color);
		public static Texture2D Trough(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TroughName, size, color);
		public static Texture2D Slingshot(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SlingshotName, size, color);
		public static Texture2D Switch(bool isClosed, IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(isClosed ? SwitchNcName : SwitchNoName, size, color);
		public static Texture2D Coil(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(CoilName, size, color);
		public static Texture2D Key(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(KeyName, size, color);
		public static Texture2D Bolt(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(BoltName, size, color);

		public static Texture2D ByComponent<T>(T mb, IconSize size = IconSize.Large, IconColor color = IconColor.Gray)
			where T : class
		{
			switch (mb)
			{
				case BumperComponent _: return Bumper(size, color);
				case DropTargetComponent _: return DropTarget(size, color);
				case DropTargetBankComponent _: return DropTargetBank(size, color);
				//case FlasherComponent _: return Flasher(size, color);
				case FlipperComponent _: return Flipper(size, color);
				case GateComponent _: return Gate(size, color);
				case HitTargetComponent _: return HitTarget(size, color);
				case KickerComponent _: return Kicker(size, color);
				case LightComponent _: return Light(size, color);
				case LightGroupComponent _: return LightGroup(size, color);
				case PlungerComponent _: return Plunger(size, color);
				case PlayfieldComponent _: return Playfield(size, color);
				case PrimitiveComponent _: return Primitive(size, color);
				case RampComponent _: return Ramp(size, color);
				case RubberComponent _: return Rubber(size, color);
				case SpinnerComponent _: return Spinner(size, color);
				case SlingshotComponent _: return Slingshot(size, color);
				case SurfaceComponent _: return Surface(size, color);
				case TriggerComponent _: return Trigger(size, color);
				case TroughComponent _: return Trough(size, color);
				default: return null;
			}
		}

		[MenuItem("Visual Pinball/Editor/Disable Gizmo Icons", false, 510)]
		public static void DisableGizmoIcons()
		{
			DisableGizmo<BumperComponent>();
			DisableGizmo<BumperColliderComponent>();
			DisableGizmo<BumperRingAnimationComponent>();
			DisableGizmo<BumperSkirtAnimationComponent>();
			DisableGizmo<DefaultGamelogicEngine>();
			DisableGizmo<DotMatrixDisplayComponent>();
			DisableGizmo<DropTargetComponent>();
			DisableGizmo<DropTargetBankComponent>();
			DisableGizmo<DropTargetColliderComponent>();
			DisableGizmo<DropTargetAnimationComponent>();
			//DisableGizmo<FlasherComponent>();
			DisableGizmo<FlipperComponent>();
			DisableGizmo<FlipperColliderComponent>();
			DisableGizmo<FlipperBaseMeshComponent>();
			DisableGizmo<FlipperRubberMeshComponent>();
			DisableGizmo<GateComponent>();
			DisableGizmo<GateColliderComponent>();
			DisableGizmo<GateWireAnimationComponent>();
			DisableGizmo<HitTargetComponent>();
			DisableGizmo<HitTargetColliderComponent>();
			DisableGizmo<HitTargetAnimationComponent>();
			DisableGizmo<KickerComponent>();
			DisableGizmo<KickerColliderComponent>();
			DisableGizmo<LightComponent>();
			DisableGizmo<LightGroupComponent>();
			DisableGizmo<PlayfieldComponent>();
			DisableGizmo<PlayfieldColliderComponent>();
			DisableGizmo<PlayfieldMeshComponent>();
			DisableGizmo<PlungerComponent>();
			DisableGizmo<PlungerColliderComponent>();
			DisableGizmo<PlungerFlatMeshComponent>();
			DisableGizmo<PlungerRodMeshComponent>();
			DisableGizmo<PlungerSpringMeshComponent>();
			DisableGizmo<PrimitiveComponent>();
			DisableGizmo<PrimitiveColliderComponent>();
			DisableGizmo<PrimitiveMeshComponent>();
			DisableGizmo<RampComponent>();
			DisableGizmo<RampColliderComponent>();
			DisableGizmo<RampFloorMeshComponent>();
			DisableGizmo<RampWallMeshComponent>();
			DisableGizmo<RampWireMeshComponent>();
			DisableGizmo<RubberComponent>();
			DisableGizmo<RubberMeshComponent>();
			DisableGizmo<RubberColliderComponent>();
			DisableGizmo<SegmentDisplayComponent>();
			DisableGizmo<SlingshotComponent>();
			DisableGizmo<SpinnerComponent>();
			DisableGizmo<SpinnerColliderComponent>();
			DisableGizmo<SpinnerPlateAnimationComponent>();
			DisableGizmo<SurfaceComponent>();
			DisableGizmo<SurfaceColliderComponent>();
			DisableGizmo<SurfaceSideMeshComponent>();
			DisableGizmo<SurfaceTopMeshComponent>();
			DisableGizmo<TableComponent>();
			DisableGizmo<TriggerComponent>();
			DisableGizmo<TriggerAnimationComponent>();
			DisableGizmo<TriggerColliderComponent>();
			DisableGizmo<TriggerMeshComponent>();
		}

		public static void ApplyToComponent<T>(Object target, Texture2D tex) where T : MonoBehaviour
		{
			if (target == null || tex == null)
			{
				throw new ArgumentNullException();
			}
			SetIconForObject.Invoke(null, new object[] { target, tex });
			DisableGizmo<T>();

			var monoScript = target as MonoScript;
			if (monoScript)
			{
				CopyMonoScriptIconToImporters.Invoke(null, new object[] { monoScript });
			}
		}

		private Texture2D GetItem(string name, IconSize size, IconColor color)
		{
			var variant = new IconVariant(name, size, color);
			if (!_icons.ContainsKey(variant))
			{
				variant = new IconVariant(name, IconSize.Large, color);
			}

			if (!_icons.ContainsKey(variant))
			{
				variant = new IconVariant(name, IconSize.Large, IconColor.Gray);
			}

			if (!_icons.ContainsKey(variant))
			{
				throw new InvalidOperationException($"Cannot find {variant.Size} {variant.Name} icon of color {variant.Color}.");
			}

			return _icons[variant];
		}

		private static void DisableGizmo<T>() where T : MonoBehaviour
		{
			var className = typeof(T).Name;
			//SetGizmoEnabled?.Invoke(null, new object[] { MonoBehaviourClassID, className, 0, false });
			SetIconEnabled?.Invoke(null, new object[] { MonoBehaviourClassID, className, 0 });
		}
	}
}
