using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Editor.Utils
{
	internal class DataFieldUtils
	{
		public delegate bool OnEditionChangeDelegate(string label, out string message, List<UnityEngine.Object> recordObjs, params (string, object)[] pList);

		public static bool ItemDataField(string label, ref float field, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.FloatField(label, field);
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataSlider(string label, ref float field, float leftVal, float rightVal, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.Slider(label, field, leftVal, rightVal);
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataField(string label, ref int field, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			int val = EditorGUILayout.IntField(label, field);
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataSlider(string label, ref int field, int leftVal, int rightVal, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			int val = EditorGUILayout.IntSlider(label, field, leftVal, rightVal);
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataField(string label, ref string field, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			string val = EditorGUILayout.TextField(label, field);
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataField(string label, ref bool field, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			bool val = EditorGUILayout.Toggle(label, field);
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataField(string label, ref Vertex2D field, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			Vertex2D val = EditorGUILayout.Vector2Field(label, field.ToUnityVector2()).ToVertex2D();
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataField(string label, ref Vertex3D field, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			Vertex3D val = EditorGUILayout.Vector3Field(label, field.ToUnityVector3()).ToVertex3D();
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static bool ItemDataField(string label, ref Engine.Math.Color field, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			EditorGUI.BeginChangeCheck();
			Engine.Math.Color val = EditorGUILayout.ColorField(label, field.ToUnityColor()).ToEngineColor();
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = val;
				return true;
			}
			return false;
		}

		public static UnityEngine.Object ItemObjectField(string label, UnityEngine.Object field, bool allowSceneObject, OnEditionChangeDelegate onChange, params (string, object)[] pList)
		{
			if (field != null) {
				EditorGUI.BeginChangeCheck();
				var val = EditorGUILayout.ObjectField(label, field, field.GetType(), allowSceneObject);
				if (EditorGUI.EndChangeCheck()) {
					string message = "";
					List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
					if (onChange(label, out message, recordObjs, pList)) {
						Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
					}
					return val;
				}
			}
			return null;
		}

		public static bool DropDownField<T>(string label, ref T field, string[] optionStrings, T[] optionValues, OnEditionChangeDelegate onChange, params (string, object)[] pList) where T : IEquatable<T>
		{
			if (optionStrings == null || optionValues == null || optionStrings.Length != optionValues.Length)
			{
				return false;
			}

			int selectedIndex = 0;
			for (int i = 0; i < optionValues.Length; i++)
			{
				if (optionValues[i].Equals(field))
				{
					selectedIndex = i;
					break;
				}
			}
			EditorGUI.BeginChangeCheck();
			selectedIndex = EditorGUILayout.Popup(label, selectedIndex, optionStrings);
			if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < optionValues.Length)
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (onChange(label, out message, recordObjs, pList))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				field = optionValues[selectedIndex];
				return true;
			}
			return false;
		}

	}
}
