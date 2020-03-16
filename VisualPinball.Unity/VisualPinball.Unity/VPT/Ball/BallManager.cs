using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Resources;
using VisualPinball.Unity.Extensions;
using Material = UnityEngine.Material;
using Player = VisualPinball.Unity.Game.Player;

namespace VisualPinball.Unity.VPT.Ball
{
	public class BallManager
	{
		private int _id;

		private readonly Engine.VPT.Table.Table _table;

		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

		public BallManager(Engine.VPT.Table.Table table)
		{
			_table = table;
		}

		public BallApi CreateBall(Player player, IBallCreationPosition ballCreator, float radius, float mass)
		{
			// calculate mass and scale
			var m = player.TableToWorld;
			var localPos = ballCreator.GetBallCreationPosition(_table).ToUnityFloat3();
			var worldPos = m.MultiplyPoint(localPos);
			var scale = new Vector3(
				m.GetColumn(0).magnitude,
				m.GetColumn(1).magnitude,
				m.GetColumn(2).magnitude
			) * (radius * 2);
			var material = CreateMaterial();

			// go will be converted automatically to entity
			var go = CreateSphere(material, worldPos, scale, mass);
			return new BallApi(go.GetComponent<GameObjectEntity>().Entity, player);
		}

		private GameObject CreateSphere(Material material, float3 pos, float3 scale, float mass)
		{
			// create go
			var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			go.name = $"Ball{++_id}";

			// set material
			go.GetComponent<Renderer>().material = material;

			// set position and scale
			go.transform.localPosition = pos;
			go.transform.localScale = scale;

			// mark to convert
			go.AddComponent<ConvertToEntity>();

			// physics shape
			var shape = go.AddComponent<PhysicsShapeAuthoring>();
			shape.SetSphere(new SphereGeometry { Radius = 0.5f, Center = float3.zero }, quaternion.identity);
			shape.Friction = new PhysicsMaterialCoefficient
				{Value = 0.1f, CombineMode = global::Unity.Physics.Material.CombinePolicy.Maximum};
			shape.Restitution = new PhysicsMaterialCoefficient
				{Value = 0.2f, CombineMode = global::Unity.Physics.Material.CombinePolicy.Maximum};

			// physics body
			var body = go.AddComponent<PhysicsBodyAuthoring>();
			body.MotionType = BodyMotionType.Dynamic;
			body.Mass = mass * 10f; // TODO tweak
			body.LinearDamping = 0.01f;
			body.AngularDamping = 0.05f;

			return go;
		}

		private static Material CreateMaterial()
		{
			if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("UniversalRenderPipelineAsset")) {
				return CreateUniversalMaterial();
			}

			return GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset")
				? CreateHDMaterial()
				: CreateStandardMaterial();
		}

		private static Material CreateStandardMaterial()
		{
			var material = new Material(Shader.Find("Standard"));
			var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true) {name = "BallDebugTexture"};
			texture.LoadImage(Resource.BallDebug.Data);
			material.SetTexture(MainTex, texture);
			material.SetFloat(Metallic, 0.9f);
			material.SetFloat(Glossiness, 0.75f);
			return material;
		}

		private static Material CreateHDMaterial()
		{
			return CreateScriptableMaterial("High Definition Render Pipeline/Lit");
		}

		private static Material CreateUniversalMaterial()
		{
			return CreateScriptableMaterial("Universal Render Pipeline/Lit");
		}

		private static Material CreateScriptableMaterial(string shaderName)
		{
			var material = new Material(Shader.Find(shaderName));
			var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true) {name = "BallDebugTexture"};
			texture.LoadImage(Resource.BallDebug.Data);
			material.SetTexture(BaseMap, texture);
			material.SetColor(BaseColor, Color.white);
			material.SetFloat(Metallic, 0.85f);
			material.SetFloat(Glossiness, 0.75f);
			return material;
		}
	}
}
