using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Resources;
using Player = VisualPinball.Unity.Game.Player;

namespace VisualPinball.Unity.VPT.Ball
{
	public class BallManager
	{
		//private int _id = 0;

		private readonly Engine.VPT.Table.Table _table;
		private readonly Entity _spherePrefab;
		private readonly EntityManager _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

		public BallManager(Engine.VPT.Table.Table table)
		{
			_table = table;

			// // create a ball "prefab" (it's actually not a prefab, but we'll use it instantiate ball entities)
			// var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			// var renderer = sphere.GetComponent<Renderer>();
			// var material = new Material(Shader.Find("Standard"));
			// var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true) {name = "BallDebugTexture"};
			// texture.LoadImage(Resource.BallDebug.Data);
			// material.SetTexture(MainTex, texture);
			// material.SetFloat(Metallic, 0.85f);
			// material.SetFloat(Glossiness, 0.75f);
			// renderer.material = material;
			//
			// var settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, null);
			// _spherePrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(sphere, settings);
		}

		public BallApi CreateBall(Player player, IBallCreationPosition ballCreator, float radius, float mass)
		{
			var entity = _entityManager.Instantiate(_spherePrefab);

			var pos = ballCreator.GetBallCreationPosition(_table);
			var position = new float3(pos.X, pos.Y, pos.Z + radius);
			_entityManager.SetComponentData(entity, new Translation {Value = position});
			_entityManager.AddComponentData(entity, new NonUniformScale { Value = radius });
			_entityManager.AddComponentData(entity, new BallData { Mass = mass });

			return new BallApi(entity, player);
		}
	}
}
