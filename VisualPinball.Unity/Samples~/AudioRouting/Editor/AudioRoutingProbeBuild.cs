// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace VisualPinball.Unity.Editor
{
	public static class AudioRoutingProbeBuild
	{
		/// <summary>Builds only the isolated project prepared by BuildProbe.ps1.</summary>
		public static void Build()
		{
			if (!File.Exists(Path.Combine(Application.dataPath, "../.vpe-audio-routing-probe")))
				throw new InvalidOperationException("Build this probe in its isolated project; do not modify a table or player project.");
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			if (!AudioSettings.GetSpatializerPluginNames().Contains(AudioRoutingProbe.SpatializerName))
				throw new InvalidOperationException("The native routing plugin was not discovered by Unity.");
			AudioSettings.SetSpatializerPluginName(AudioRoutingProbe.SpatializerName);
			var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
			settings.FindProperty("Default Speaker Mode").intValue = (int)AudioSpeakerMode.Mode7point1;
			settings.ApplyModifiedPropertiesWithoutUndo();
			Directory.CreateDirectory("Assets/Resources");
			const string mixerPath = "Assets/Resources/RoutingProbeMixer.mixer";
			AssetDatabase.DeleteAsset(mixerPath);
			CreateMixer(mixerPath);
			const string generatorPath = "Assets/Resources/GeneratorProbeMixer.mixer";
			AssetDatabase.DeleteAsset(generatorPath);
			CreateMixer(generatorPath, true);
			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			var camera = new GameObject("Camera").AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.05f, .08f, .065f);
			new GameObject("Light").AddComponent<Light>().type = LightType.Directional;
			new GameObject("Routing probe").AddComponent<AudioRoutingProbe>();
			EditorSceneManager.SaveScene(scene, "Assets/RoutingProbe.unity");
			AssetDatabase.SaveAssets();
			var args = Environment.GetCommandLineArgs();
			int index = Array.IndexOf(args, "-probeOutput");
			if (index < 0 || index + 1 == args.Length) throw new ArgumentException("Supply -probeOutput with the executable path.");
			PlayerSettings.defaultIsNativeResolution = false;
			PlayerSettings.defaultScreenWidth = 960; PlayerSettings.defaultScreenHeight = 540;
			PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
			PlayerSettings.runInBackground = true;
			var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/RoutingProbe.unity" }, locationPathName = args[index+1], target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
			if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Audio routing probe build failed: " + report.summary.result);
		}

		private static void CreateMixer(string path, bool generator = false)
		{
			// Unity has no public mixer-construction API. Keep the reflected API in this
			// version-specific developer sample, outside the production runtime.
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			var assembly = typeof(UnityEditor.Editor).Assembly;
			var mixerType = assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
			var effectType = assembly.GetType("UnityEditor.Audio.AudioMixerEffectController", true);
			var mixer = (AudioMixer)mixerType.GetMethod("CreateMixerControllerAtPath", flags).Invoke(null, new object[] { path });
			var master = mixerType.GetProperty("masterGroup", flags).GetValue(mixer);
			var effectsProperty = master.GetType().GetProperty("effects", flags);
			var existing = (Array)effectsProperty.GetValue(master);
			var effect = (UnityEngine.Object)Activator.CreateInstance(effectType, flags, null, new object[] { "VPE Routing Meter" }, null);
			AssetDatabase.AddObjectToAsset(effect, mixer);
			var all = Array.CreateInstance(effectType, existing.Length + (generator ? 2 : 1));
			int offset = generator ? 1 : 0;
			if (generator) {
				var source = (UnityEngine.Object)Activator.CreateInstance(effectType, flags, null, new object[] { "VPE Routing Generator" }, null);
				AssetDatabase.AddObjectToAsset(source, mixer); all.SetValue(source, 0);
			}
			Array.Copy(existing, 0, all, offset, existing.Length); all.SetValue(effect, all.Length-1);
			effectsProperty.SetValue(master, all);
			var serialized = new SerializedObject(mixer);
			serialized.FindProperty("m_EnableSuspend").boolValue = false;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(mixer); EditorUtility.SetDirty((UnityEngine.Object)master);
		}
	}
}
