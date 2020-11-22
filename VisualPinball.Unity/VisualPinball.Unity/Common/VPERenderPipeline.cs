// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using System.IO;
using System.Linq;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;


namespace VisualPinball.Unity
{
	public static class VPERenderPipeline
	{

		private const bool LOG_NEW_DEFINE_SYMBOLS = true;

		private const string HDRP_PACKAGE = "render-pipelines.high-definition";
		private const string URP_PACKAGE = "render-pipelines.universal";

		private const string TAG_HDRP = "USING_HDRP";
		private const string TAG_URP = "USING_URP";

		private const string CS_CLASSNAME = "VPEDefinedRenderPipeline";
		private const string CS_FILENAME = CS_CLASSNAME + ".cs";


		[UnityEditor.Callbacks.DidReloadScripts]
		private static void OnScriptsReloaded()
		{

			ListRequest packagesRequest = Client.List(true);

			LoadPackages(packagesRequest);

		}
		private static void LoadPackages(ListRequest request)
		{

			if(request == null)
				return;


			// Wait for request to complete
			for(int i = 0; i < 1000; i++)
			{
				if(request.Result != null)
					break;
				Task.Delay(1).Wait();
			}
			if(request.Result == null)
				throw new TimeoutException();

			// Find out what packages are installed
			var packagesList = request.Result.ToList();

			bool hasHDRP = packagesList.Find(x => x.name.Contains(HDRP_PACKAGE)) != null;
			bool hasURP = packagesList.Find(x => x.name.Contains(URP_PACKAGE)) != null;

			if(hasHDRP && hasURP)
				Debug.LogError("<b>RenderPipeline Packages:</b> Both the HDRP and URP seem to be installed, this may cause problems!");


			DefinePreProcessors(hasHDRP, hasURP);
			SaveToFile(CSharpFileCode(hasHDRP, hasURP));

		}



		private static void DefinePreProcessors(bool defineHDRP, bool defineURP)
		{

			string originalDefineSymbols;
			string newDefineSymbols;

			List<string> defined;
			BuildTargetGroup platform = EditorUserBuildSettings.selectedBuildTargetGroup;

			string log = string.Empty;

			originalDefineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(platform);
			defined = originalDefineSymbols.Split(';').Where(x => !String.IsNullOrWhiteSpace(x)).ToList();

			Action<bool, string> AppendRemoveTag = (stat, tag) =>
			{
				if(stat && !defined.Contains(tag))
					defined.Add(tag);
				else if(!stat && defined.Contains(tag))
					defined.Remove(tag);
			};

			AppendRemoveTag(defineHDRP, TAG_HDRP);
			AppendRemoveTag(defineURP, TAG_URP);

			newDefineSymbols = string.Join(";", defined);
			if(originalDefineSymbols != newDefineSymbols)
			{
				PlayerSettings.SetScriptingDefineSymbolsForGroup(platform, newDefineSymbols);
				log += $"<color=yellow>{platform.ToString()}</color> Old Define Symbols:\n - <color=red>{originalDefineSymbols}</color>\n";
				log += $"<color=yellow>{platform.ToString()}</color> New Define Symbols:\n - <color=green>{newDefineSymbols}</color>\n";
			}
	
			if(LOG_NEW_DEFINE_SYMBOLS && !String.IsNullOrEmpty(log))
				Debug.Log($"<b>{nameof(VPERenderPipeline)}:</b> PlayerSetting Define Symbols have been updated! Check log for further details.\n{log}");

		}

		private static void SaveToFile(string Code)
		{ 

			// Get working directory to save the file to
			var directory = Directory.GetParent(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName());
			if(directory != null && directory.Parent != null)
				directory = directory.Parent;

			File.WriteAllText(directory.FullName + "\\" + CS_FILENAME, Code);

		}
		private static string CSharpFileCode(bool defineHDRP, bool defineURP)
		{

			Func<bool, string> ToString = (b) => b ? "true" : "false";

			return "namespace VisualPinball.Unity \n" +
			"{\n" +
				$"\tpublic static class {CS_CLASSNAME}\n" +
				"\t{\n\n" +

					$"\t\tpublic const bool USING_HDRP = {ToString(defineHDRP)};\n\n" +

					$"\t\tpublic const bool USING_URP = {ToString(defineURP)};\n\n" +

				"\t}\n" +
			"}";

		}


	}
}
