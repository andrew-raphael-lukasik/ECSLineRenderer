using System.Runtime.CompilerServices;
using UnityEngine;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;

namespace Segments
{
	public static class Core
	{

		static World world;
		static EntityManager commander;
		static EntityArchetype segmentArchetype = default(EntityArchetype);
		static Entity defaultPrefab;


		public static World GetWorld ()
		{
			if( world!=null )
				return world;
			else
			{
				world = World.DefaultGameObjectInjectionWorld;
				DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups( world , Prototypes.worldSystems );
				commander = world.EntityManager;
				
				defaultPrefab = commander.CreateEntity( GetSegmentArchetype() );
				commander.SetComponentData<Segment>( defaultPrefab , Prototypes.segment );
				commander.SetComponentData<SegmentWidth>( defaultPrefab , Prototypes.segmentWidth );
				commander.SetComponentData<SegmentAspectRatio>( defaultPrefab , new SegmentAspectRatio{ Value = 1f } );
				commander.AddComponentData<RenderBounds>( defaultPrefab , Prototypes.renderBounds );
				commander.AddComponentData<LocalToWorld>( defaultPrefab , new LocalToWorld { Value = float4x4.TRS( new float3{} , quaternion.identity , new float3{x=1,y=1,z=1} ) });
				
				var renderMesh = Prototypes.renderMesh;
				commander.SetSharedComponentData<RenderMesh>( defaultPrefab , renderMesh );
				// commander.SetComponentData<MaterialColor>( _defaultPrefab , new MaterialColor{ Value=new float4{x=1,y=1,z=1,w=1} } );// change: initialize manually
				
				#if ENABLE_HYBRID_RENDERER_V2
				commander.SetComponentData( defaultPrefab , new BuiltinMaterialPropertyUnity_RenderingLayer{ Value = new uint4{ x=(uint)renderMesh.layer } } );
				commander.SetComponentData( defaultPrefab , new BuiltinMaterialPropertyUnity_LightData{ Value = new float4{ z=1 } } );
				#endif

				#if DEBUG
				Debug.Log($"{nameof(Segments)}: systems initialized");
				#endif

				return world;
			}
		}
		
		
		public static EntityManager GetEntityManager ()
			=> GetWorld().EntityManager;
		

		public static Entity GetSegmentPrefabCopy ()
		{
			Initialize();
			Entity copy = commander.Instantiate( defaultPrefab );
			commander.AddComponent<Prefab>( copy );
			return copy;
		}
		public static Entity GetSegmentPrefabCopy ( Material material )
		{
			Entity copy = GetSegmentPrefabCopy();
			var renderMesh = commander.GetSharedComponentData<RenderMesh>( copy );
			renderMesh.material = material;
			commander.SetSharedComponentData<RenderMesh>( copy , renderMesh );
			return copy;
		}
		public static Entity GetSegmentPrefabCopy ( float width )
		{
			Entity copy = GetSegmentPrefabCopy();
			commander.SetComponentData( copy , new SegmentWidth{ Value=(half)width } );
			return copy;
		}
		public static Entity GetSegmentPrefabCopy ( Material material , float width )
		{
			Entity copy = GetSegmentPrefabCopy( material );
			commander.SetComponentData( copy , new SegmentWidth{ Value=(half)width } );
			return copy;
		}


		public static EntityArchetype GetSegmentArchetype ()
		{
			if( segmentArchetype.Valid )
				return segmentArchetype;
			
			segmentArchetype = commander.CreateArchetype( Prototypes.segment_prefab_components );
			return segmentArchetype;
		}


		public static void DestroyAllSegments ()
		{
			var query = commander.CreateEntityQuery( new ComponentType[]{ typeof(Segment) } );
			commander.DestroyEntity( query );
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Initialize () => GetWorld();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void _WorldInitializedWarningCheck ()
		{
			if( world==null || !world.IsCreated )
				Debug.LogError($"Call `{nameof(Segments)}.{nameof(Core)}.{nameof(Initialize)}()` first.");
		}


	}
}
