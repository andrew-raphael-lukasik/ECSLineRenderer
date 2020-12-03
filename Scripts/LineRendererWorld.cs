using System.Runtime.CompilerServices;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace EcsLineRenderer
{
	public static class LineRendererWorld
	{


		static World _world;
		public static bool IsCreated => _world!=null && _world.IsCreated;

		static EntityArchetype _segmentArchetype = default(EntityArchetype);
		static Entity _defaultPrefab;

		public static EndSimulationEntityCommandBufferSystem CommandBufferSystem { get; private set; }
		public static EntityCommandBuffer CreateCommandBuffer () => CommandBufferSystem.CreateCommandBuffer();
		public static void AddCommandBufferDependency ( JobHandle producerJob ) => CommandBufferSystem.AddJobHandleForProducer( producerJob );


		public static World GetOrCreateWorld ()
		{
			if( IsCreated ) return _world;
			_world = CreateNewDedicatedRenderingWorld( k_world_name );

			{
				var command = _world.EntityManager;
				_defaultPrefab = command.CreateEntity( GetSegmentArchetype() );
				command.SetComponentData<Segment>( _defaultPrefab , Prototypes.segment );
				command.SetComponentData<SegmentWidth>( _defaultPrefab , Prototypes.segmentWidth );
				command.SetComponentData<SegmentAspectRatio>( _defaultPrefab , new SegmentAspectRatio{ Value = 1f } );
				command.AddComponentData<RenderBounds>( _defaultPrefab , Prototypes.renderBounds );
				command.AddComponentData<LocalToWorld>( _defaultPrefab , new LocalToWorld { Value = float4x4.TRS( new float3{} , quaternion.identity , new float3{x=1,y=1,z=1} ) });
				
				var renderMesh = Prototypes.renderMesh;
				command.SetSharedComponentData<RenderMesh>( _defaultPrefab , renderMesh );
				command.SetComponentData<MaterialColor>( _defaultPrefab , new MaterialColor{ Value=new float4{x=1,y=1,z=1,w=1} } );
				
				#if ENABLE_HYBRID_RENDERER_V2
				command.SetComponentData( _defaultPrefab , new BuiltinMaterialPropertyUnity_RenderingLayer{ Value = new uint4{ x=(uint)renderMesh.layer } } );
				command.SetComponentData( _defaultPrefab , new BuiltinMaterialPropertyUnity_LightData{ Value = new float4{ z=1 } } );
				#endif
			}

			CommandBufferSystem = _world.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
			
			return _world;
		}
		
		public static World GetWorld ()
		{
			_WorldExistsWarningCheck();
			return _world;
		}
		
		public static EntityManager GetEntityManager ()
		{
			_WorldExistsWarningCheck();
			return _world.EntityManager;
		}

		public static void Dispose () => _world?.Dispose();


		public static Entity GetSegmentPrefabCopy ()
		{
			var command = GetEntityManager();
			Entity copy = command.Instantiate( _defaultPrefab );
			command.AddComponent<Prefab>( copy );
			return copy;
		}

		public static EntityArchetype GetSegmentArchetype ()
		{
			var command = GetEntityManager();
			if( _segmentArchetype.Valid )
				return _segmentArchetype;
			
			_segmentArchetype = command.CreateArchetype( Prototypes.segment_prefab_components );
			return _segmentArchetype;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void _WorldExistsWarningCheck ()
		{
			if( _world==null || !_world.IsCreated )
				Debug.LogWarning($"{nameof(EcsLineRenderer)}'s world does not exists yet. Call `{nameof(GetOrCreateWorld)}()` first.");
		}


		public static World CreateNewDedicatedRenderingWorld ( string name )
		{
			var world = new World( name );
			var systems = new System.Type[]
				{
						typeof(HybridRendererSystem)
					
						// fixes: "Internal: deleting an allocation that is older than its permitted lifetime of 4 frames (age = 15)"
					,	typeof(EndSimulationEntityCommandBufferSystem)
				}
				.Concat( Prototypes.worldSystems )
				.ToArray();
			DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups( world , systems );
			ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop( world );
			return world;
		}


		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab )
						=> InstantiatePool( length , out entities , out prefab , Prototypes.k_defaul_segment_width , Internal.ResourceProvider.default_material );
		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab , float width )
						=> InstantiatePool( length , out entities , out prefab , width , Internal.ResourceProvider.default_material );
		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab , Material material )
						=> InstantiatePool( length , out entities , out prefab , Prototypes.k_defaul_segment_width , material );
		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab , float width , Material material )
		{
			GetOrCreateWorld();// make sure world exists
			var command = GetEntityManager();
			prefab = GetSegmentPrefabCopy();
			{
				if( material!=null )
				{
					var renderMesh = command.GetSharedComponentData<RenderMesh>( prefab );
					if( renderMesh.material!=material )
					{
						renderMesh.material = material;
						command.SetSharedComponentData( prefab , renderMesh );
					}
				}
				else Debug.LogError($"material is null");
				command.SetComponentData( prefab , new SegmentWidth{ Value = (half)width } );
			}
			entities = command.Instantiate( srcEntity:prefab , instanceCount:length , allocator:Allocator.Persistent );
		}


		/// <summary> Resizes pool length ONLY when it's != requestedLength </summary>
		public static void Resize ( ref NativeArray<Entity> entities , Entity prefab , int requestedLength )
		{
			Assert.IsTrue( entities.IsCreated );
			int length = entities.Length;
			if( length != requestedLength )
			{
				if( length < requestedLength ) Upsize( ref entities , prefab , requestedLength );
				else if( length > requestedLength ) Downsize( ref entities , requestedLength );
			}
		}
		

		/// <summary> Upsizes pool length when it's < minLength </summary>
		public static void Upsize ( ref NativeArray<Entity> entities , Entity prefab , int minLength )
		{
			Assert.IsTrue( entities.IsCreated );
			int length = entities.Length;
			if( length < minLength )
			{
				int difference = minLength - length;

				#if UNITY_EDITOR
				Debug.Log($"↑ upsizing pool (length) {length} < {minLength} (minLength)");
				#endif

				var command = GetEntityManager();
				NativeArray<Entity> newEntities = command.Instantiate( srcEntity:prefab , instanceCount:difference , allocator:Allocator.Temp );
				var resizedArray = new NativeArray<Entity>( minLength , Allocator.Persistent , NativeArrayOptions.UninitializedMemory );
				NativeArray<Entity>.Copy( src:entities , srcIndex:0 , dst:resizedArray , dstIndex:0 , length:length );
				NativeArray<Entity>.Copy( src:newEntities , srcIndex:0 , dst:resizedArray , dstIndex:length , length:difference );
				entities.Dispose();
				newEntities.Dispose();
				entities = resizedArray;
			}
		}

		/// <summary> Downsizes pool length when it's > maxLength </summary>
		public static void Downsize ( ref NativeArray<Entity> entities , int maxLength )
		{
			if( !entities.IsCreated ) Debug.LogError( $"{nameof(entities)}.IsCreated returns false" );
			Assert.IsTrue( entities.IsCreated , $"{nameof(entities)}.IsCreated returns false" );
			int length = entities.Length;
			if( length>maxLength )
			{
				#if UNITY_EDITOR
				Debug.Log($"↓ downsizing pool (length) {length} > {maxLength} (maxLength)");
				#endif

				DestroyNow( entities.Slice( maxLength , length-maxLength ) );

				var oldArray = entities;
				var resizedArray = new NativeArray<Entity>( length:maxLength , Allocator.Persistent );
				NativeArray<Entity>.Copy( src:oldArray , srcIndex:0 , dst:resizedArray , dstIndex:0 , length:maxLength );
				entities = resizedArray;
				oldArray.Dispose();
			}
		}


		/// <summary> Schedules DestroyEntity commands for all entities in the collection. </summary>
		public static void Destroy ( NativeArray<Entity> entities , EntityCommandBuffer command )
		{
			if( !entities.IsCreated ) return;
			Destroy( entities.Slice() , command );
		}
		/// <summary> Schedules DestroyEntity commands for all entities in the collection. </summary>
		public static void Destroy ( NativeSlice<Entity> entities , EntityCommandBuffer command )
		{
			for( int i=0 ; i<entities.Length ; i++ )
				command.DestroyEntity( entities[i] );
		}


		/// <summary> Destroys all entities in the collection immediately. </summary>
		public static void DestroyNow ( NativeArray<Entity> entities )
		{
			if( !entities.IsCreated ) return;
			DestroyNow( entities.Slice() );
		}
		/// <summary> Destroys all entities in the collection immediately. </summary>
		public static void DestroyNow ( NativeSlice<Entity> entities )
		{
			var command = GetEntityManager();
			for( int i=0 ; i<entities.Length ; i++ )
				command.DestroyEntity( entities[i] );
		}


	}
}
