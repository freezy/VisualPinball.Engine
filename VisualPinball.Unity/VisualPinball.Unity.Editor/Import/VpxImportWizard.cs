using System;
using System.IO;
using NLog;
using UnityEditor;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class VpxImportWizard : EditorWindow
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
			#region GUI Layout
			float boxMargin = 6;
			float settingsMargin = 12;

			GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
			{
				alignment = TextAnchor.MiddleLeft,
				stretchWidth = true
			};

			GUIStyle labelInfoStyle = new GUIStyle(GUI.skin.label)
			{
				fontStyle = FontStyle.Italic
			};

			GUIStyle labelDescriptionStyle = new GUIStyle(GUI.skin.label)
			{
				fontStyle = FontStyle.Bold
			};
			#endregion GUI Layout

			#region Description
			EditorGUILayout.BeginVertical(boxStyle);
			{
				GUILayout.Space(boxMargin);

				EditorGUILayout.LabelField("The Import Wizard is used for DEVELOPMENT PURPOSES ONLY!", labelDescriptionStyle);
				EditorGUILayout.LabelField("It will be removed later, use only if you are aware of the ramifications.", labelDescriptionStyle);

				GUILayout.Space(boxMargin);
			}
			EditorGUILayout.EndVertical();
			#endregion Description

			#region Top Toolbar
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
            #endregion Top Toolbar

            #region Settings
            EditorGUILayout.BeginVertical(boxStyle);
			{
				EditorGUILayout.BeginHorizontal();
				VpxImportWizardSettings.VpxPath = EditorGUILayout.TextField("VPX File", VpxImportWizardSettings.VpxPath);
				if (GUILayout.Button("Select File", GUILayout.Width(100)))
				{
					// get the initial directory from the vpx path
					string initialDirectory = null;
					if( !string.IsNullOrEmpty(VpxImportWizardSettings.VpxPath))
					{
						string vpxDirectory = Path.GetDirectoryName(VpxImportWizardSettings.VpxPath);
						if( Directory.Exists( vpxDirectory)) {
							initialDirectory = vpxDirectory;
						}
					}

					// open file dialog
					var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", initialDirectory, new[] { "Visual Pinball Table Files", "vpx" });
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
            #endregion Settings

            #region Bottom Toolbar
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
						Logger.Error("VPX file not found: {0}", VpxImportWizardSettings.VpxPath);
					}
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();
			#endregion Bottom Toolbar
		}
	}
}
