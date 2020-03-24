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
using Unity.Transforms;
using Unity.Rendering;
using SphereCollider = Unity.Physics.SphereCollider;
using BoxCollider = Unity.Physics.BoxCollider;
using ECSMaterial = Unity.Physics.Material;

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
		private static Mesh _unitySphereMesh = null; // used to cache ball mesh from GameObject

		public BallManager(Engine.VPT.Table.Table table)
		{
			_table = table;
		}

		public BallApi CreateBall(Player player, IBallCreationPosition ballCreator, float radius, float mass)
		{
			// calculate mass and scale
			var m = player.TableToWorld;

			var localPos = ballCreator.GetBallCreationPosition(_table).ToUnityFloat3();
			localPos.z += 10;
			float4x4 model = player.TableToWorld * Matrix4x4.TRS(localPos, Quaternion.identity, new float3(radius));
			
			var worldPos = m.MultiplyPoint(localPos);
			var scale3 = new Vector3(
				m.GetColumn(0).magnitude,
				m.GetColumn(1).magnitude,
				m.GetColumn(2).magnitude
			);
			var scale = (scale3.x + scale3.y + scale3.z) / 3.0f; // scale is only scale (without radiusfloat now, not vector.
			var material = CreateMaterial();

			// go will be converted automatically to entity
//			var go = CreateSphere(material, worldPos, scale * radius * 2, mass);
//			return new BallApi(go.GetComponent<GameObjectEntity>().Entity, player);

			// ECS way:
			EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

			EntityArchetype archetype = entityManager.CreateArchetype(new ComponentType[]
			{
				typeof(Translation),
				typeof(Rotation),
				typeof(Scale),
				typeof(RenderMesh),
				typeof(RenderBounds),
				typeof(PhysicsCollider),
				typeof(PhysicsMass),
				typeof(PhysicsVelocity),
				typeof(PhysicsDamping),
				typeof(LocalToWorld)
			});


			var colliderComponent = new PhysicsCollider
			{
				Value = SphereCollider.Create(
					new SphereGeometry
					{
						Center = float3.zero,
						Radius = scale * radius
					},
					new CollisionFilter
					{
						BelongsTo = 0xffffffff,
						CollidesWith = 0xffffffff,
						GroupIndex = 0
					},
					new ECSMaterial
					{
						CustomTags  = 0,           // <- can we use it to mark objects type? Like BallTag.
						Friction    = 0.001f,
						Restitution = 0.002f
					})
			};

			Entity entity = entityManager.CreateEntity(archetype);
			entityManager.AddComponentData(entity, new Translation { Value = worldPos });
			entityManager.AddComponentData(entity, new Rotation { Value = Quaternion.identity });
			entityManager.AddComponentData(entity, new Scale { Value = scale * radius * 2 });
			entityManager.AddSharedComponentData(entity, new RenderMesh {
				mesh = GetSphereMesh(),
				material = CreateMaterial()
			});
			entityManager.AddComponentData(entity, colliderComponent);
			entityManager.AddComponentData(entity, PhysicsMass.CreateDynamic(colliderComponent.MassProperties, mass));
			entityManager.AddComponentData(entity, new PhysicsVelocity
			{
				Linear = new float3(0.0001f, 0.0002f, 0.0003f),
				Angular = new float3(0)
			});
			entityManager.AddComponentData(entity, new PhysicsDamping
			{
				Linear = 0.011f,
				Angular = 0.055f
			});

#if UNITY_EDITOR
			entityManager.SetName(entity, $"Ball{++_id}");
#endif
			return new BallApi(entity, player);
		}

		/// <summary>
		/// Dirty way to get SphereMesh from Unity.
		/// ToDo: Get Mesh from our resources
		/// </summary>
		/// <returns>Sphere Mesh</returns>
		private static Mesh GetSphereMesh()
		{
			if (!_unitySphereMesh)
			{
				var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				_unitySphereMesh = go.GetComponent<MeshFilter>().sharedMesh;
				GameObject.Destroy(go);
			}

			return _unitySphereMesh;
		}

		//private GameObject CreateSphere(Material material, float3 pos, float3 scale, float mass)
		//{
		//	// create go
		//	var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		//	go.name = $"Ball{++_id}";

		//	// set material
		//	go.GetComponent<Renderer>().material = material;

		//	// set position and scale
		//	go.transform.localPosition = pos;
		//	go.transform.localScale = scale;

		//	// mark to convert
		//	go.AddComponent<ConvertToEntity>();

		//	// physics shape
		//	var shape = go.AddComponent<PhysicsShapeAuthoring>();
		//	shape.SetSphere(new SphereGeometry { Radius = 0.5f, Center = float3.zero }, quaternion.identity);
		//	shape.Friction = new PhysicsMaterialCoefficient
		//		{Value = 0.1f, CombineMode = global::Unity.Physics.Material.CombinePolicy.Maximum};
		//	shape.Restitution = new PhysicsMaterialCoefficient
		//		{Value = 0.2f, CombineMode = global::Unity.Physics.Material.CombinePolicy.Maximum};

		//	// physics body
		//	var body = go.AddComponent<PhysicsBodyAuthoring>();
		//	body.MotionType = BodyMotionType.Dynamic;
		//	body.Mass = mass * 10f; // TODO tweak
		//	body.LinearDamping = 0.01f;
		//	body.AngularDamping = 0.05f;

		//	return go;
		//}

		private static Material CreateMaterial()
		{
			if (GraphicsSettings.renderPipelineAsset != null) {
				if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("UniversalRenderPipelineAsset")) {
					return CreateUniversalMaterial();
				}

				if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset")) {
					return CreateHDMaterial();
				}
			}

			return CreateStandardMaterial();
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
