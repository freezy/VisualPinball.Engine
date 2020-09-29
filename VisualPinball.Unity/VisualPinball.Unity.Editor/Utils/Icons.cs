using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
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
			private readonly string _name;
			private readonly IconSize _size;
			private readonly IconColor _color;

			public IconVariant(string name, IconSize size, IconColor color)
			{
				_name = name;
				_size = size;
				_color = color;
			}
		}

		private const string BumperName = "bumper";
		private const string FlipperName = "flipper";
		private const string GateName = "gate";
		private const string KickerName = "kicker";
		private const string LightName = "light";
		private const string PlungerName = "plunger";
		private const string PrimitiveName = "primitive";
		private const string RampName = "ramp";
		private const string RubberName = "rubber";
		private const string SpinnerName = "spinner";
		private const string SurfaceName = "surface";
		private const string HitTargetName = "target";
		private const string TriggerName = "trigger";

		private static readonly string[] Names = {
			BumperName, FlipperName, GateName, KickerName, LightName, PlungerName, PrimitiveName, RampName, RubberName,
			SpinnerName, SurfaceName, HitTargetName, TriggerName
		};

		private readonly Dictionary<IconVariant, Texture2D> _icons = new Dictionary<IconVariant,Texture2D>();
		private static readonly MethodInfo CopyMonoScriptIconToImporters = typeof(MonoImporter).GetMethod("CopyMonoScriptIconToImporters", BindingFlags.Static|BindingFlags.NonPublic);
		private static readonly MethodInfo SetIconForObject = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static|BindingFlags.NonPublic);
		private static readonly MethodInfo SetGizmoEnabled = Assembly.GetAssembly(typeof(UnityEditor.Editor))?.GetType("UnityEditor.AnnotationUtility")?.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);

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
		public static Texture2D Plunger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PlungerName, size, color);
		public static Texture2D Primitive(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(PrimitiveName, size, color);
		public static Texture2D Ramp(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RampName, size, color);
		public static Texture2D Rubber(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(RubberName, size, color);
		public static Texture2D Spinner(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SpinnerName, size, color);
		public static Texture2D Surface(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(SurfaceName, size, color);
		public static Texture2D Target(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(HitTargetName, size, color);
		public static Texture2D Trigger(IconSize size = IconSize.Large, IconColor color = IconColor.Gray) => Instance.GetItem(TriggerName, size, color);

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

			return _icons[variant];
		}

		private static void DisableGizmo<T>() where T : MonoBehaviour
		{
			// var asm = Assembly
			// 	.GetAssembly(typeof(UnityEditor.Editor))
			// 	.GetType("UnityEditor.AnnotationUtility")
			// 	.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);
			// Type type = asm.GetType("UnityEditor.AnnotationUtility");
			// MethodInfo setGizmoEnabled = type.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);

			var className = typeof(T).Name;
			SetGizmoEnabled?.Invoke(null, new object[] { MonoBehaviourClassID, className, 0, false });
		}

		public static void ToggleGizmos(bool gizmosOn)
		{
			int val = gizmosOn ? 1 : 0;
			Assembly asm = Assembly.GetAssembly(typeof(UnityEditor.Editor));
			Type type = asm.GetType("UnityEditor.AnnotationUtility");
			if (type != null)
			{
				MethodInfo getAnnotations = type.GetMethod("GetAnnotations", BindingFlags.Static | BindingFlags.NonPublic);
				MethodInfo setGizmoEnabled = type.GetMethod("SetGizmoEnabled", BindingFlags.Static | BindingFlags.NonPublic);
				MethodInfo setIconEnabled = type.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);
				var annotations = getAnnotations.Invoke(null, null);
				var classIDs = new List<int>();
				var scriptClasses = new List<string>();
				foreach (var annotation in (IEnumerable) annotations) {
					Type annotationType = annotation.GetType();
					FieldInfo classIdField = annotationType.GetField("classID", BindingFlags.Public | BindingFlags.Instance);
					FieldInfo scriptClassField = annotationType.GetField("scriptClass", BindingFlags.Public | BindingFlags.Instance);
					classIDs.Add((int)classIdField.GetValue(annotation));
					scriptClasses.Add((string)scriptClassField.GetValue(annotation));
				}

				foreach (object annotation in (IEnumerable)annotations)
				{
					Type annotationType = annotation.GetType();
					FieldInfo classIdField = annotationType.GetField("classID", BindingFlags.Public | BindingFlags.Instance);
					FieldInfo scriptClassField = annotationType.GetField("scriptClass", BindingFlags.Public | BindingFlags.Instance);
					if (classIdField != null && scriptClassField != null)
					{
						int classId = (int)classIdField.GetValue(annotation);
						string scriptClass = (string)scriptClassField.GetValue(annotation);
						//setGizmoEnabled.Invoke(null, new object[] { classId, scriptClass, val });
						//setIconEnabled.Invoke(null, new object[] { classId, scriptClass, val });
					}
				}
			}
		}
	}
}
