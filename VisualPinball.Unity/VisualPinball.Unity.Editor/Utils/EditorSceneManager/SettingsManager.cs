using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor; 
using VisualPinball.Unity.Editor;

/// <summary>
/// Used to manage persistent settings and preference for editor tools.  
/// Menu is provided in the Edit>Preferences window under Visual Pinball. 
/// </summary>


//TODO: Make this dynamic if possible. 

namespace VisualPinball.Unity.Editor.Utils
{
	public class SettingsManager : ScriptableObject
	{

		public const string _SettingsPath = AssetPaths.settingsPath + "VPESettings.asset";

		[SerializeField]
		private int m_cameraPreset; 

		internal static SettingsManager GetOrCreateSettings()
		{
			var settings = AssetDatabase.LoadAssetAtPath<SettingsManager>(_SettingsPath); 
			if(settings == null)
			{
				settings = ScriptableObject.CreateInstance<SettingsManager>();
				settings.m_cameraPreset = 2;
				AssetDatabase.CreateAsset(settings, _SettingsPath);
				AssetDatabase.SaveAssets(); 
			}
			return settings; 
		}

		internal static SerializedObject GetSerializedSettings()
		{
			return new SerializedObject(GetOrCreateSettings()); 
		}
	
	}	

	struct Setting
	{
		string Label;
		string Property;
		string Keyword; 
	}

	//Register the SettingsProvider
	static class SetttingsManagerRegister
	{

		static string providerPath = "Visual Pinball/Settings";
		//TODO: Make loop of settings. 
		//static List<Setting> settings = new List<Setting>(); 

		[SettingsProvider]
		public static SettingsProvider CreateSettingsProvider()
		{
			var provider = new SettingsProvider(providerPath, SettingsScope.User)
			{
				label = "Camera",
				guiHandler = (searchContext) =>
				{
					var settings = SettingsManager.GetSerializedSettings();
					EditorGUILayout.PropertyField(settings.FindProperty("m_cameraPreset"), new GUIContent("Camera Preset"));
				},
				keywords = new HashSet<string>(new[] { "Camera Preset" })
			};
			return provider;
		}
			
		
	}

}
