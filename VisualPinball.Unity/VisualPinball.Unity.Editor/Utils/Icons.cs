using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using VisualPinball.Engine.VPT;
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
		private const string CoilName = "coil";
		private const string FlipperName = "flipper";
		private const string GateName = "gate";
		private const string KeyName = "keyboard";
		private const string KickerName = "kicker";
		private const string LightName = "light";
		private const string PlayfieldName = "playfield";
		private const string PlungerName = "plunger";
		private const string PrimitiveName = "primitive";
		private const string RampName = "ramp";
		private const string RubberName = "rubber";
		private const string SpinnerName = "spinner";
		private const string SurfaceName = "surface";
		private const string TableName = "table";
		private const string HitTargetName = "target";
		private const string TriggerName = "trigger";
		private const string TroughName = "trough";
		private const string SwitchNcName = "switch_nc";
		private const string SwitchNoName = "switch_no";

		private static readonly string[] Names = {
			BumperName, CoilName, FlipperName, GateName, KeyName, KickerName, LightName, PlayfieldName, PlungerName,
			PrimitiveName, RampName, RubberName, SpinnerName, SurfaceName, HitTargetName, TableName, TriggerName,
			TroughName, SwitchNcName, SwitchNoName
		};

		private readonly Dictionary<IconVariant, Texture2D> _icons = new Dictionary<IconVariant,Texture2D>();
		private static readonly MethodInfo CopyMonoScriptIconToImporters = typeof(MonoImporter).GetMethod("CopyMonoScriptIconToImporters", BindingFlags.Static|BindingFlags.NonPublic);
		private static readonly MethodInfo SetIconForObject = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static|BindingFlags.NonPublic);
		private static readonly MethodInfo SetGizmoEnabled = Assembly.GetAssembly(typeof(UnityEditor.Editor))?.GetType("UnityEditor.AnnotationUtility")?.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly MethodInfo SetIconEnabled = Assembly.GetAssembly(typeof(UnityEditor.Editor))?.GetType("UnityEditor.AnnotationUtility")?.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

		// see https://docs.unity3d.com/Manual/ClassIDReference.html
		private static readonly int MonoBehaviourClassID = 114;

		private static Icons _instance;
		private static Icons Instance => _instance ?? (_instance = new Icons());


		private Icons()
		{
			const string iconPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Resources/Icons";
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

		public static Texture2D Bumper(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(BumperName, size, color);
		public static Texture2D Flipper(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(FlipperName, size, color);
		public static Texture2D Gate(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(GateName, size, color);
		public static Texture2D Kicker(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(KickerName, size, color);
		public static Texture2D Light(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(LightName, size, color);
		public static Texture2D Playfield(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlayfieldName, size, color);
		public static Texture2D Plunger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlungerName, size, color);
		public static Texture2D Primitive(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PrimitiveName, size, color);
		public static Texture2D Ramp(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RampName, size, color);
		public static Texture2D Rubber(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RubberName, size, color);
		public static Texture2D Spinner(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SpinnerName, size, color);
		public static Texture2D Surface(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SurfaceName, size, color);
		public static Texture2D Table(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TableName, size, color);
		public static Texture2D Target(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(HitTargetName, size, color);
		public static Texture2D Trigger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TriggerName, size, color);
		public static Texture2D Trough(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TroughName, size, color);
		public static Texture2D Switch(bool normallyClosed, IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(normallyClosed ? SwitchNcName : SwitchNoName, size, color);
		public static Texture2D Coil(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(CoilName, size, color);
		public static Texture2D Key(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(KeyName, size, color);

		public static Texture2D ByComponent<T>(T mb, IconSize size = IconSize.Large, IconColor color = IconColor.Gray)
			where T : class
		{
			switch (mb) {
				case BumperAuthoring _: return Bumper(size, color);
				case FlipperAuthoring _: return Flipper(size, color);
				case GateAuthoring _: return Gate(size, color);
				case KickerAuthoring _: return Kicker(size, color);
				case LightAuthoring _: return Light(size, color);
				case PlungerAuthoring _: return Plunger(size, color);
				case PlayfieldAuthoring _: return Playfield(size, color);
				case PrimitiveAuthoring _: return Primitive(size, color);
				case RampAuthoring _: return Ramp(size, color);
				case RubberAuthoring _: return Rubber(size, color);
				case SpinnerAuthoring _: return Spinner(size, color);
				case SurfaceAuthoring _: return Surface(size, color);
				case HitTargetAuthoring _: return Target(size, color);
				case TriggerAuthoring _: return Trigger(size, color);
				case TroughAuthoring _: return Trough(size, color);
				default: return null;
			}
		}

		[DidReloadScripts]
		public static void OnScriptsReloaded()
		{
			DisableGizmo<BumperAuthoring>();
			DisableGizmo<BumperBaseMeshAuthoring>();
			DisableGizmo<BumperCapMeshAuthoring>();
			DisableGizmo<BumperColliderAuthoring>();
			DisableGizmo<BumperRingMeshAuthoring>();
			DisableGizmo<BumperRingAnimationAuthoring>();
			DisableGizmo<BumperSkirtMeshAuthoring>();
			DisableGizmo<BumperSkirtAnimationAuthoring>();
			DisableGizmo<DefaultGameEngineAuthoring>();
			DisableGizmo<FlipperAuthoring>();
			DisableGizmo<FlipperColliderAuthoring>();
			DisableGizmo<FlipperBaseMeshAuthoring>();
			DisableGizmo<FlipperRubberMeshAuthoring>();
			DisableGizmo<GateAuthoring>();
			DisableGizmo<GateColliderAuthoring>();
			DisableGizmo<GateBracketMeshAuthoring>();
			DisableGizmo<GateWireAnimationAuthoring>();
			DisableGizmo<GateWireMeshAuthoring>();
			DisableGizmo<HitTargetAuthoring>();
			DisableGizmo<HitTargetColliderAuthoring>();
			DisableGizmo<HitTargetMeshAuthoring>();
			DisableGizmo<KickerAuthoring>();
			DisableGizmo<KickerColliderAuthoring>();
			DisableGizmo<KickerMeshAuthoring>();
			DisableGizmo<LightAuthoring>();
			DisableGizmo<LightBulbMeshAuthoring>();
			DisableGizmo<LightSocketMeshAuthoring>();
			DisableGizmo<PlayfieldAuthoring>();
			DisableGizmo<PlayfieldColliderAuthoring>();
			DisableGizmo<PlayfieldMeshAuthoring>();
			DisableGizmo<PlungerAuthoring>();
			DisableGizmo<PlungerColliderAuthoring>();
			DisableGizmo<PlungerFlatMeshAuthoring>();
			DisableGizmo<PlungerRodMeshAuthoring>();
			DisableGizmo<PlungerSpringMeshAuthoring>();
			DisableGizmo<PrimitiveAuthoring>();
			DisableGizmo<PrimitiveColliderAuthoring>();
			DisableGizmo<PrimitiveMeshAuthoring>();
			DisableGizmo<RampAuthoring>();
			DisableGizmo<RampColliderAuthoring>();
			DisableGizmo<RampFloorMeshAuthoring>();
			DisableGizmo<RampWallMeshAuthoring>();
			DisableGizmo<RampWireMeshAuthoring>();
			DisableGizmo<RubberAuthoring>();
			DisableGizmo<RubberMeshAuthoring>();
			DisableGizmo<RubberColliderAuthoring>();
			DisableGizmo<SpinnerAuthoring>();
			DisableGizmo<SpinnerBracketMeshAuthoring>();
			DisableGizmo<SpinnerColliderAuthoring>();
			DisableGizmo<SpinnerPlateMeshAuthoring>();
			DisableGizmo<SpinnerPlateAnimationAuthoring>();
			DisableGizmo<SurfaceAuthoring>();
			DisableGizmo<SurfaceColliderAuthoring>();
			DisableGizmo<SurfaceSideMeshAuthoring>();
			DisableGizmo<SurfaceTopMeshAuthoring>();
			DisableGizmo<TableAuthoring>();
			DisableGizmo<TriggerAuthoring>();
			DisableGizmo<TriggerColliderAuthoring>();
			DisableGizmo<TriggerMeshAuthoring>();
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

		private static void DisableGizmo<T>() where T : MonoBehaviour
		{
			var className = typeof(T).Name;
			//SetGizmoEnabled?.Invoke(null, new object[] { MonoBehaviourClassID, className, 0, false });
			SetIconEnabled?.Invoke(null, new object[] { MonoBehaviourClassID, className, 0 });
		}
	}
}
