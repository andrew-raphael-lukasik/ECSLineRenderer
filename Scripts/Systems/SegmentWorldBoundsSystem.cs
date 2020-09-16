using System.Runtime.CompilerServices;

using Unity.Mathematics;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;

namespace EcsLineRenderer
{
	[WorldSystemFilter( WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor )]
	[UpdateInGroup( typeof(InitializationSystemGroup) )]
	[UpdateAfter( typeof(SegmentTransformSystem) )]
	public class SegmentWorldBoundsSystem : SystemBase
	{

		EntityQuery _query;
		EntityQuery _query_noCWRB;

		protected override void OnCreate ()
		{
			_query = GetEntityQuery ( new EntityQueryDesc{
				All = new[] {
						ComponentType.ReadOnly<Segment>()

					,	ComponentType.ChunkComponent<ChunkWorldRenderBounds>()
					,	ComponentType.ReadWrite<WorldRenderBounds>()
					,	ComponentType.ReadOnly<RenderBounds>()
					,	ComponentType.ReadOnly<LocalToWorld>()
				}
			});
			_query.SetChangedVersionFilter( new[] {
					ComponentType.ReadOnly<RenderBounds>()
				,	ComponentType.ReadOnly<LocalToWorld>()
			});
			
			_query_noCWRB = GetEntityQuery ( new EntityQueryDesc{
				All = new[] { ComponentType.ReadOnly<Segment>() } ,
				None = new[] { ComponentType.ChunkComponent<ChunkWorldRenderBounds>() }
			} );
		}

		protected override void OnUpdate ()
		{
			var ecb = new EntityCommandBuffer( Allocator.TempJob );
			var cmd = ecb.AsParallelWriter();

			if( LineRendererWorld.IsCreated && this.World==LineRendererWorld.GetWorld() )
			{
				var boundsJob = new BoundsJob
				{
					rendererBounds = GetComponentTypeHandle<RenderBounds>(true),
					localToWorld = GetComponentTypeHandle<LocalToWorld>(true),
					worldRenderBounds = GetComponentTypeHandle<WorldRenderBounds>(),
					chunkWorldRenderBounds = GetComponentTypeHandle<ChunkWorldRenderBounds>(),
				};
				boundsJob.ScheduleParallel( _query );
			}

			Entities
				.WithName($"add_missing_{nameof(WorldRenderBounds)}_components_job")
				.WithAll<Segment>()
				.WithNone<WorldRenderBounds>()
				.ForEach( ( in int entityInQueryIndex , in Entity entity )=>
				{
					cmd.AddComponent<WorldRenderBounds>( entityInQueryIndex , entity );
				})
				.ScheduleParallel();

			Job
				.WithName("playback_commands")
				.WithCode( ()=>
				{
					ecb.Playback( EntityManager );
					ecb.Dispose();
				})
				.WithoutBurst().Run();

			EntityManager.AddChunkComponentData<ChunkWorldRenderBounds>(
				_query_noCWRB ,
				default(ChunkWorldRenderBounds)
			);
		}

		[Unity.Burst.BurstCompile]
        struct BoundsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<RenderBounds> rendererBounds;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> localToWorld;
            public ComponentTypeHandle<WorldRenderBounds> worldRenderBounds;
            public ComponentTypeHandle<ChunkWorldRenderBounds> chunkWorldRenderBounds;
            public void Execute ( ArchetypeChunk chunk , int chunkIndex , int firstEntityIndex )
            {
                var worldBounds = chunk.GetNativeArray( this.worldRenderBounds );
                var localBounds = chunk.GetNativeArray( this.rendererBounds );
                var localToWorld = chunk.GetNativeArray( this.localToWorld );
                MinMaxAABB combined = MinMaxAABB.Empty;
                for( int i=0 ; i!=localBounds.Length ; i++ )
                {
                    var transformed = AABB.Transform( localToWorld[i].Value , localBounds[i].Value );
                    worldBounds[i] = new WorldRenderBounds{ Value = transformed };
                    combined.Encapsulate( transformed );
                }
                chunk.SetChunkComponentData( chunkWorldRenderBounds , new ChunkWorldRenderBounds{
					Value = combined
				});
            }
        }

	}
}
