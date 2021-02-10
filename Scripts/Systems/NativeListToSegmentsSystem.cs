using System.Collections.Generic;
using Assertions = UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Transforms;

namespace Segments.Systems
{
	[WorldSystemFilter(0)]
	[UpdateInGroup( typeof(InitializationSystemGroup) )]
	public class NativeListToSegmentsSystem : SystemBase
	{
		
		EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;
		List<Batch> _batches = new List<Batch>();

		public static NativeList<JobHandle> dependencies;

		protected override void OnCreate ()
		{
			_endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
			dependencies = new NativeList<JobHandle>( Allocator.Persistent );
		}

		protected override void OnDestroy ()
		{
			Dependency.Complete();
			JobHandle.CombineDependencies( dependencies ).Complete();
			dependencies.Dispose();

			foreach( var batch in _batches )
				DestroyBatch( batch );
		}

		protected override void OnUpdate ()
		{
			var commander = EntityManager;
			var cmd = _endSimulationEcbSystem.CreateCommandBuffer();
			var segmentData = GetComponentDataFromEntity<Segment>( isReadOnly:true );

			if( dependencies.Length!=0 )
			{
				dependencies.Add( Dependency );
				Dependency = JobHandle.CombineDependencies( dependencies );
				dependencies.Clear();
			}

			for( int batchIndex=_batches.Count-1 ; batchIndex!=-1 ; batchIndex-- )
			{
				var batch = _batches[ batchIndex ];
				Entity prefab = batch.prefab;
				NativeList<float3x2> buffer = batch.buffer;
				NativeList<Entity> entities = batch.entities;

				if( buffer.IsCreated )
				{
					// var bufferArray = buffer.AsDeferredJobArray();
					// int bufferSize = bufferArray.Length;
					int bufferSize = buffer.AsParallelReader().Length;//buffer.Length;
					//Debug.LogWarning( $"buffer.IsCreated\tbufferSize:{bufferSize}" );
					if( entities.Length!=bufferSize )
					{
						if( entities.Length<bufferSize )
						{
							NativeArray<Entity> instantiated = commander.Instantiate( prefab , bufferSize-entities.Length , Allocator.Temp );
							//Debug.LogWarning( $"{instantiated.Length} segments instantiated" );
							entities.AddRange( instantiated );
							instantiated.Dispose();
						}
						else
						{
							commander.DestroyEntity( entities.AsArray().Slice(bufferSize) );
							//Debug.LogWarning( $"{bufferSize-entities.Length} segments destroyed" );
							entities.Length = bufferSize;
						}
					}
					
					Job
						.WithName("component_data_update_job")
						// .WithReadOnly( segmentData )
						.WithCode( () =>
						{
							//Debug.LogWarning("tick");
							for( int i=0 ; i<bufferSize ; i++ )
							{
								Entity entity = entities[i];
								
								#if UNITY_ASSERTIONS
								// Assertions.Assert.IsTrue( segmentData.HasComponent(entity) , "entity has no Segment component" );
								#endif
								
								// Segment existing = segmentData[ entity ];
								Segment expected = new Segment{ start=buffer[i].c0 , end=buffer[i].c1 };

								// if( math.lengthsq( existing.start-expected.start + existing.end-expected.end )>1e-4f )
									cmd.SetComponent( entity , expected );
							}
						} )
						.WithBurst().Schedule();
				}
				else if( entities.IsCreated )
				{
					_batches.RemoveAt( batchIndex );
					commander.DestroyEntity( entities );
					//Debug.LogWarning($"batch {batchIndex} destroyed");
				}
			}

			_endSimulationEcbSystem.AddJobHandleForProducer( Dependency );
		}

		public void CreateBatch ( in Entity segmentPrefab , out NativeList<float3x2> buffer )
		{
			buffer = new NativeList<float3x2>( Allocator.Persistent );
			var entities = new NativeList<Entity>( Allocator.Persistent );
			_batches.Add( new Batch{ prefab=segmentPrefab , entities=entities , buffer=buffer } );

			//Debug.LogWarning("new batch created");
		}

		public void DestroyBatch ( NativeList<float3x2> buffer )
		{
			// for( int i=_batches.Count-1 ; i!=-1 ; i-- )
			// {
			// 	var batch = _batches[i];
			// 	if( batch.buffer==buffer )
			// 	{
			// 		var commander = Core.GetEntityManager();
			// 		commander.DestroyEntity( batch.entities );
			// 		batch.entities.Dispose();
			// 		batch.buffer.Dispose();
			// 		_batches.RemoveAt( i );
			// 	}
			// }
		}
		void DestroyBatch ( Batch batch )
		{
			var commander = Core.GetEntityManager();
			commander.DestroyEntity( batch.entities );
			batch.entities.Dispose();
			batch.buffer.Dispose();
			_batches.Remove( batch );
		}

		struct Batch
		{
			public Entity prefab;
			public NativeList<Entity> entities;
			public NativeList<float3x2> buffer;
		}

	}
	
}
