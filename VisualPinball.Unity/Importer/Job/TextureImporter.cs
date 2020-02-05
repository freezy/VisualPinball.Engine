using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Importer.AssetHandler;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer.Job
{
	public class TextureImporter
	{
		private readonly Texture[] _textures;
		private readonly IAssetHandler _assetHandler;

		public TextureImporter(Texture[] textures, IAssetHandler assetHandler)
		{
			_textures = textures;
			_assetHandler = assetHandler;
		}

		public void ImportTextures()
		{
			using (var job = new TextureJob(_textures, _assetHandler)) {
				var handle = job.Schedule(_textures.Length, 64);
				handle.Complete();
			}

			_assetHandler.ImportTextures(_textures);
		}
	}

	internal struct TextureJob : IJobParallelFor, IDisposable
	{
		[ReadOnly]
		private NativeArray<IntPtr> _textures;

		[ReadOnly]
		[NativeDisableUnsafePtrRestriction]
		private readonly IntPtr _assetHandler;

		public TextureJob(IEnumerable<Texture> textures, IAssetHandler assetHandler)
		{
			_textures = new NativeArray<IntPtr>(textures.Select(MemHelper.ToIntPtr).ToArray(), Allocator.Persistent);
			_assetHandler = MemHelper.ToIntPtr(assetHandler);
		}

		public void Execute(int index)
		{
			// unpack pointers
			var texture = MemHelper.ToObj<Texture>(_textures[index]);
			var assetHandler = MemHelper.ToObj<IAssetHandler>(_assetHandler);

			assetHandler.HandleTextureData(texture);
		}

		public void Dispose()
		{
			_textures.Dispose();
		}
	}
}
