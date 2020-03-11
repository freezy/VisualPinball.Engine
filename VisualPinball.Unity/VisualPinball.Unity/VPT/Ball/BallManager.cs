using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Extensions;
using Player = VisualPinball.Unity.Game.Player;

namespace VisualPinball.Unity.VPT.Ball
{
	public class BallManager
	{
		//private int _id = 0;

		private readonly Engine.VPT.Table.Table _table;
		private readonly Entity _rootEntity;
		private readonly EntityManager _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
		private readonly GameObject _spherePrefab;

		private readonly Material _material;

		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

		public BallManager(Engine.VPT.Table.Table table, Entity rootEntity)
		{
			_table = table;
			_rootEntity = rootEntity;

			// create a ball "prefab" (it's actually not a prefab, but we'll use it instantiate ball entities)
			_material = new Material(Shader.Find("Standard"));
			var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true) {name = "BallDebugTexture"};
			texture.LoadImage(File.ReadAllBytes(@"Assets\Scenes\check512.png"));
			_material.SetTexture(MainTex, texture);
			_material.SetFloat(Metallic, 0.85f);
			_material.SetFloat(Glossiness, 0.75f);

			_spherePrefab = CreateSphere();
			_spherePrefab.SetActive(false);
		}

		public BallApi CreateBall(Player player, IBallCreationPosition ballCreator, float radius, float mass)
		{
			var settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, null);

			_spherePrefab.SetActive(true);
			var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(_spherePrefab, settings);
			_spherePrefab.SetActive(false);

			var pos = ballCreator.GetBallCreationPosition(_table).ToUnityFloat3();
			var scale = new float3(radius, radius, radius);

			// parenting
			_entityManager.AddComponentData(entity, new Parent {Value = _rootEntity});
			_entityManager.AddComponentData(entity, new LocalToParent());

			// local position
			_entityManager.SetComponentData(entity, new Translation {Value = pos});
			_entityManager.AddComponentData(entity, new NonUniformScale {Value = scale});
			_entityManager.AddComponentData(entity, new BallData {Mass = mass});

			return new BallApi(entity, player);
		}

		private GameObject CreateSphere()
		{
			var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphere.GetComponent<Renderer>().material = _material;
			return sphere;
		}
	}
}
