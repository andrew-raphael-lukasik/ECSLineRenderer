using System.Collections.Generic;
using Assertions = UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Transforms;

namespace Segments
{
	[WorldSystemFilter(0)]
	[UpdateInGroup( typeof(InitializationSystemGroup) )]
	public class NativeListToSegmentsSystem : SystemBase
	{
		
		EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;
		List<Batch> _batches = new List<Batch>();

		public NativeList<JobHandle> Dependencies;
		public JobHandle ScheduledJobs => Dependency;


		protected override void OnCreate ()
		{
			_endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
			Dependencies = new NativeList<JobHandle>( Allocator.Persistent );
		}


		protected override void OnDestroy ()
		{
			Dependency.Complete();
			JobHandle.CombineDependencies( Dependencies ).Complete();
			Dependencies.Dispose();

			foreach( var batch in _batches )
			{
				if( batch.entities.IsCreated )
				{
					EntityManager.DestroyEntity( batch.entities );
					batch.entities.Dispose();
				}
				if( batch.buffer.IsCreated )
				{
					batch.buffer.Dispose();
				}
			}
			_batches.Clear();
		}


		protected override void OnUpdate ()
		{
			var cmd = _endSimulationEcbSystem.CreateCommandBuffer();
			var segmentData = GetComponentDataFromEntity<Segment>( isReadOnly:true );

			if( Dependencies.Length!=0 )
			{
				Dependencies.Add( Dependency );
				Dependency = JobHandle.CombineDependencies( Dependencies );
				Dependencies.Clear();
			}

			for( int batchIndex=_batches.Count-1 ; batchIndex!=-1 ; batchIndex-- )
			{
				var batch = _batches[ batchIndex ];
				Entity prefab = batch.prefab;
				NativeList<float3x2> buffer = batch.buffer;
				NativeList<Entity> entities = batch.entities;

				if( buffer.IsCreated )
				{
					int bufferSize = buffer.AsParallelReader().Length;
					if( entities.Length!=bufferSize )
					{
						if( entities.Length<bufferSize )
						{
							NativeArray<Entity> instantiated = EntityManager.Instantiate( prefab , bufferSize-entities.Length , Allocator.Temp );
							entities.AddRange( instantiated );
							instantiated.Dispose();
						}
						else
						{
							EntityManager.DestroyEntity( entities.AsArray().Slice(bufferSize) );
							entities.Length = bufferSize;
						}
					}
					
					Job
						.WithName("component_data_update_job")
						.WithReadOnly( buffer )
						.WithNativeDisableContainerSafetyRestriction( buffer )
						.WithCode( () =>
						{
							for( int i=0 ; i<bufferSize ; i++ )
							{
								Entity entity = entities[i];

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
					EntityManager.DestroyEntity( entities );
				}
			}

			_endSimulationEcbSystem.AddJobHandleForProducer( Dependency );
		}


		public void CreateBatch ( in Entity segmentPrefab , out NativeList<float3x2> buffer )
		{
			buffer = new NativeList<float3x2>( Allocator.Persistent );
			var entities = new NativeList<Entity>( Allocator.Persistent );
			_batches.Add( new Batch{ prefab=segmentPrefab , entities=entities , buffer=buffer } );
		}


		public void DestroyBatch ( NativeList<float3x2> buffer )
		{
			throw new System.NotImplementedException();
			// for( int i=_batches.Count-1 ; i!=-1 ; i-- )
			// {
			// 	var batch = _batches[i];
			// 	if( batch.buffer==buffer )
			// 	{
			// 		EntityManager.DestroyEntity( batch.entities );
			// 		batch.entities.Dispose();
			// 		batch.buffer.Dispose();
			// 		_batches.RemoveAt( i );
			// 	}
			// }
		}
		

		struct Batch
		{
			public Entity prefab;
			public NativeList<Entity> entities;
			public NativeList<float3x2> buffer;
		}


	}
	
}
