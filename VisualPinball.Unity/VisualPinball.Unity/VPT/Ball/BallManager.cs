using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public class BallManager
	{
		private int _id;

		private readonly Engine.VPT.Table.Table _table;

		private static Mesh _unitySphereMesh; // used to cache ball mesh from GameObject

		public BallManager(Engine.VPT.Table.Table table)
		{
			_table = table;
		}

		public BallApi CreateBall(Player player, IBallCreationPosition ballCreator, float radius, float mass)
		{
			// calculate mass and scale
			var m = player.TableToWorld;

			var localPos = ballCreator.GetBallCreationPosition(_table).ToUnityFloat3();
			var localVel = ballCreator.GetBallCreationVelocity(_table).ToUnityFloat3();
			localPos.z += radius;
			//float4x4 model = player.TableToWorld * Matrix4x4.TRS(localPos, Quaternion.identity, new float3(radius));

			var worldPos = m.MultiplyPoint(localPos);
			var scale3 = new Vector3(
				m.GetColumn(0).magnitude,
				m.GetColumn(1).magnitude,
				m.GetColumn(2).magnitude
			);
			var scale = (scale3.x + scale3.y + scale3.z) / 3.0f; // scale is only scale (without radiusfloat now, not vector.
			var material = BallMaterial.CreateMaterial();
			var mesh = GetSphereMesh();

			// create ball entity
			EngineProvider<IPhysicsEngine>.Get()
				.BallCreate(mesh, material, worldPos, localPos, localVel, scale, mass, radius);

			return null;
		}

		public static Entity CreatePureEntity(Mesh mesh, Material material, float3 position, float scale)
		{
			var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

			Entity entity = entityManager.CreateEntity(
				typeof(Translation),
				typeof(Scale),
				typeof(Rotation),
				typeof(RenderMesh),
				typeof(LocalToWorld),
				typeof(RenderBounds));

			entityManager.SetSharedComponentData(entity, new RenderMesh
			{
				mesh = mesh,
				material = material
			});

			entityManager.SetComponentData(entity, new RenderBounds
			{
				Value = mesh.bounds.ToAABB()
			});

			entityManager.SetComponentData(entity, new Translation
			{
				Value = position
			});

			entityManager.SetComponentData(entity, new Scale
			{
				Value = scale
			});

			return entity;
		}

		public static void CreateEntity(Mesh mesh, Material material, in float3 worldPos, in float3 localPos,
			in float3 localVel, in float scale, in float mass, in float radius)
		{
			var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			BallAuthoring.CreateEntity(entityManager,
				mesh, material, worldPos, scale, localPos,
					localVel, radius, mass);
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
				Object.Destroy(go);
			}

			return _unitySphereMesh;
		}

		private GameObject CreateSphere(Material material, float3 pos, float3 scale)
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

			return go;
		}

		#region Material

		/// <summary>
		/// Ball material creator instance for the current graphics pipeline
		/// </summary>
		public static IBallMaterial BallMaterial => CreateBallMaterial();

		/// <summary>
		/// Create a material converter depending on the graphics pipeline
		/// </summary>
		/// <returns></returns>
		private static IBallMaterial CreateBallMaterial()
		{
			if (GraphicsSettings.renderPipelineAsset != null)
			{
				if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("UniversalRenderPipelineAsset"))
				{
					return new UrpBallMaterial();
				}
				else if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset"))
				{
					return new HdrpBallMaterial();
				}
			}

			return new StandardBallMaterial();
		}

		#endregion

	}
}
