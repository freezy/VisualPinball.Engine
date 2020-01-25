using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer
{
	public class TextureImporter
	{
		private readonly RenderObjectGroup[] _renderObjects;
		private readonly Texture[] _textures;

		public TextureImporter(Table table)
		{
			// takes 120ms for batman, might be faster on threads
			_renderObjects = table.Renderables.Select(r => r.GetRenderObjects(table, Origin.Original, false)).ToArray();
			_textures = table.Textures.Values.Concat(Texture.LocalTextures).ToArray();
		}

		public void ImportTextures(string textureFolder)
		{
			Profiler.Start("Mark to analyze");
			MarkTexturesToAnalyze();
			Profiler.Stop("Mark to analyze");

			Profiler.Start("Run job");
			using (var job = new TextureJob(_textures, textureFolder)) {
				var handle = job.Schedule(_textures.Length, 64);
				handle.Complete();
			}
			Profiler.Stop("Run job");

			// now the assets are written to disk, explicitly import them
			Profiler.Start("Unity asset import");
			AssetDatabase.ImportAsset(textureFolder, ImportAssetOptions.ImportRecursive);
			Profiler.Stop("Unity asset import");
		}

		private void MarkTexturesToAnalyze()
		{
			foreach (var rog in _renderObjects) {
				foreach (var ro in rog.RenderObjects) {
					if (ro.Material != null && ro.Map != null) {
						if (!ro.Material.IsOpacityActive && ro.Material.Edge >= 1 && ro.Map.HasTransparentFormat) {
							ro.Map.MarkAnalyze();
						}
					}
				}
			}
		}
	}

	internal struct TextureJob : IJobParallelFor, IDisposable
	{
		[ReadOnly]
		private NativeArray<IntPtr> _textures;

		[ReadOnly]
		[NativeDisableUnsafePtrRestriction]
		private readonly IntPtr _textureFolder;

		public TextureJob(IEnumerable<Texture> textures, string textureFolder)
		{
			_textures = new NativeArray<IntPtr>(textures.Select(MemHelper.ToIntPtr).ToArray(), Allocator.Persistent);
			_textureFolder = MemHelper.ToIntPtr(textureFolder);
		}

		public void Execute(int index)
		{
			// unpack pointers
			var texture = MemHelper.ToObj<Texture>(_textures[index]);
			var textureFolder =  MemHelper.ToObj<string>(_textureFolder);

			// get stats if marked
			if (texture.IsMarkedToAnalyze) {
				texture.GetStats();
			}

			// write to disk
			var path = texture.GetUnityFilename(textureFolder);
			File.WriteAllBytes(path, texture.FileContent);
		}

		public void Dispose()
		{
			_textures.Dispose();
		}
	}
}
