using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using VisualPinball.Unity.Editor.Import;

[Serializable]
public class VpxImportWizard : EditorWindow
{
	#region Menu
	public static VpxImportWizard Window;

	[MenuItem("Visual Pinball/Import Wizard")]
	public static void Init()
	{
		Window = (VpxImportWizard)GetWindow(typeof(VpxImportWizard));
		Window.autoRepaintOnSceneChange = true;
		Window.minSize = new Vector2(800, 300);
		Window.titleContent = new GUIContent("Visual Pinball Import Wizard"); // here we could attach an icon
		Window.Show();
	}
	#endregion Menu

	public void OnGUI()
	{
		GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
		boxStyle.alignment = TextAnchor.MiddleLeft;
		boxStyle.stretchWidth = true;

		GUIStyle labelInfoStyle = new GUIStyle(GUI.skin.label);
		labelInfoStyle.fontStyle = FontStyle.Italic;

		float boxMargin = 6;
		float settingsMargin = 12;

		#region top toolbar
		EditorGUILayout.BeginVertical(boxStyle);
		{
			GUILayout.Space(boxMargin);

			EditorGUILayout.BeginHorizontal();

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Reset Settings", GUILayout.Width(100)))
			{
				VpxImportWizardSettings.Reset();
			}
			EditorGUILayout.EndHorizontal();

			GUILayout.Space(boxMargin);

		}
		EditorGUILayout.EndVertical();
		#endregion top toolbar

		// settings
		EditorGUILayout.BeginVertical(boxStyle);
		{
			EditorGUILayout.BeginHorizontal();
			VpxImportWizardSettings.VpxPath = EditorGUILayout.TextField("VPX File", VpxImportWizardSettings.VpxPath);
			if (GUILayout.Button("Select File", GUILayout.Width(100)))
			{
				// open file dialog
				var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", null, new[] { "Visual Pinball Table Files", "vpx" });
				if (vpxPath.Length != 0 && File.Exists(vpxPath))
				{
					VpxImportWizardSettings.VpxPath = vpxPath;
				}
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.LabelField("The vpx file path which will be used for the import", labelInfoStyle);

			GUILayout.Space(settingsMargin);

			VpxImportWizardSettings.ApplyPatch = EditorGUILayout.Toggle("Apply Patch", VpxImportWizardSettings.ApplyPatch);

			EditorGUILayout.LabelField("Allows you to disable the automatic patching of a table during the import", labelInfoStyle);

			GUILayout.Space(settingsMargin);

			VpxImportWizardSettings.TableName = EditorGUILayout.TextField("Table Name", VpxImportWizardSettings.TableName);

			EditorGUILayout.LabelField("The name of the gameobject. Empty = default. Tags: %TABLENAME% = table name, %INFONAME% = Table's Info Name", labelInfoStyle);


			GUILayout.FlexibleSpace();
		}
		EditorGUILayout.EndVertical();

		#region bottom toolbar
		EditorGUILayout.BeginVertical(boxStyle);
		{
			EditorGUILayout.BeginHorizontal();

			// align right
			GUILayout.FlexibleSpace();

			//GUI.backgroundColor = Color.red;
			if (GUILayout.Button("Import", GUILayout.Width(100), GUILayout.Height(30)))
			{
				if (File.Exists(VpxImportWizardSettings.VpxPath))
				{
					VpxImportEngine.Import(VpxImportWizardSettings.VpxPath, null, VpxImportWizardSettings.ApplyPatch, VpxImportWizardSettings.TableName);
				}
				else
				{
					Debug.LogError(String.Format("VPX file not found: {0}", VpxImportWizardSettings.VpxPath));
				}
			}
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndVertical();
		#endregion bottom toolbar
	}
}
