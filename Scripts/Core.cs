using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

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

		public static EndSimulationEntityCommandBufferSystem CommandBufferSystem { get; private set; }
		public static EntityCommandBuffer CreateCommandBuffer () => CommandBufferSystem.CreateCommandBuffer();
		public static void AddCommandBufferDependency ( JobHandle producerJob ) => CommandBufferSystem.AddJobHandleForProducer( producerJob );


		public static World GetWorld ()
		{
			if( world!=null )
				return world;
			else
			{
				world = World.DefaultGameObjectInjectionWorld;
				DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups( world , Prototypes.worldSystems );
				CommandBufferSystem = world.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
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


		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab )
						=> InstantiatePool( length , out entities , out prefab , Prototypes.k_defaul_segment_width , Internal.ResourceProvider.default_material );
		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab , float width )
						=> InstantiatePool( length , out entities , out prefab , width , Internal.ResourceProvider.default_material );
		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab , Material material )
						=> InstantiatePool( length , out entities , out prefab , Prototypes.k_defaul_segment_width , material );
		public static void InstantiatePool ( int length , out NativeArray<Entity> entities , out Entity prefab , float width , Material material )
		{
			Initialize();
			prefab = GetSegmentPrefabCopy();
			{
				if( material!=null )
				{
					var renderMesh = commander.GetSharedComponentData<RenderMesh>( prefab );
					if( renderMesh.material!=material )
					{
						renderMesh.material = material;
						commander.SetSharedComponentData( prefab , renderMesh );
					}
				}
				else Debug.LogError($"material is null");
				commander.SetComponentData( prefab , new SegmentWidth{ Value = (half)width } );
			}
			entities = commander.Instantiate( srcEntity:prefab , instanceCount:length , allocator:Allocator.Persistent );
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

				NativeArray<Entity> newEntities = commander.Instantiate( srcEntity:prefab , instanceCount:difference , allocator:Allocator.Temp );
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
		public static void Destroy ( NativeArray<Entity> entities , EntityCommandBuffer commands )
		{
			if( !entities.IsCreated ) return;
			Destroy( entities.Slice() , commands );
		}
		/// <summary> Schedules DestroyEntity commands for all entities in the collection. </summary>
		public static void Destroy ( NativeSlice<Entity> entities , EntityCommandBuffer commands )
		{
			for( int i=0 ; i<entities.Length ; i++ )
				commands.DestroyEntity( entities[i] );
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
			for( int i=0 ; i<entities.Length ; i++ )
				commander.DestroyEntity( entities[i] );
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
