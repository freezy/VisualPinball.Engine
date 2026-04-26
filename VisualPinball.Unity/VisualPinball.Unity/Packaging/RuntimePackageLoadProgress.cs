using UnityEngine;

namespace VisualPinball.Unity
{
	public enum RuntimePackageLoadStage
	{
		OpeningPackage,
		ImportingScene,
		InstantiatingScene,
		LoadingSounds,
		LoadingAssets,
		LoadingColliderMeshes,
		RestoringPackables,
		RestoringReferences,
		RestoringGlobals,
		RestoringTableMetadata,
		RestoringMaterials,
		Finalizing,
	}

	public readonly struct RuntimePackageLoadProgress
	{
		public RuntimePackageLoadProgress(RuntimePackageLoadStage stage, float value01, string message)
		{
			Stage = stage;
			Value01 = Mathf.Clamp01(value01);
			Message = message ?? string.Empty;
		}

		public RuntimePackageLoadStage Stage { get; }
		public float Value01 { get; }
		public string Message { get; }
		public int Percent => Mathf.RoundToInt(Value01 * 100f);
	}
}
