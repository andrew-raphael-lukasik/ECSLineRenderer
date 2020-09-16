using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Transforms;

namespace EcsLineRenderer
{
	[WorldSystemFilter( WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor )]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	[UpdateBefore(typeof(SegmentInitializationSystem))]
	public class CreateSegmentsSystem : SystemBase
	{

		EntityArchetype _segmentArchetype;

		protected override void OnCreate ()
		{
			_segmentArchetype = EntityManager.CreateArchetype(
					typeof(Segment)
				,	typeof(SegmentWidth)
				,	typeof(SegmentMaterialOverride)
				,	typeof(MaterialColor)
			);
		}

		protected override void OnUpdate ()
		{
			var segmentArchetype = _segmentArchetype;
			var defaultSegmentMaterial = Internal.ResourceProvider.default_segment_material;
			var ecb = new EntityCommandBuffer( Allocator.TempJob );
			var cmd = ecb.AsParallelWriter();

			Entities
				.WithName("add_components_job")
				.WithNone<SegmentMaterialOverride>()
				.ForEach( ( in int entityInQueryIndex , in Entity entity , in DynamicBuffer<CreateSegmentsBufferElement> buffer ) =>
				{
					int len = buffer.Length;
					for( int i=0 ; i<len ; i++ )
					{
						var seg = buffer[i];
						var instance = cmd.CreateEntity( entityInQueryIndex , segmentArchetype );
						cmd.SetComponent( entityInQueryIndex , instance , new Segment{
							start	= seg.start ,
							end		= seg.end
						} );
						cmd.SetComponent( entityInQueryIndex , instance , new SegmentWidth{
							Value	= seg.width
						} );
						cmd.SetComponent( entityInQueryIndex , instance , defaultSegmentMaterial );
						cmd.SetComponent( entityInQueryIndex , instance , new MaterialColor{
							Value = new float4{ x=seg.color.r , y=seg.color.g , z=seg.color.b , w=seg.color.a }
						} );
					}

					cmd.DestroyEntity( entityInQueryIndex , entity );
				}
				).ScheduleParallel();

			Entities
				.WithName("add_components_job_material_override")
				.ForEach( ( in int entityInQueryIndex , in Entity entity , in DynamicBuffer<CreateSegmentsBufferElement> buffer , in SegmentMaterialOverride material ) =>
				{
					int len = buffer.Length;
					for( int i=0 ; i<len ; i++ )
					{
						var seg = buffer[i];
						var instance = cmd.CreateEntity( entityInQueryIndex , segmentArchetype );
						cmd.SetComponent( entityInQueryIndex , instance , new Segment{
							start	= seg.start ,
							end		= seg.end
						} );
						cmd.SetComponent( entityInQueryIndex , instance , new SegmentWidth{
							Value	= seg.width
						} );
						cmd.SetComponent( entityInQueryIndex , instance , material );
						cmd.SetComponent( entityInQueryIndex , instance , new MaterialColor{
							Value = new float4{ x=seg.color.r , y=seg.color.g , z=seg.color.b , w=seg.color.a }
						} );
					}

					cmd.DestroyEntity( entityInQueryIndex , entity );
				}
				).ScheduleParallel();
			
			Job
				.WithName("playback_commands")
				.WithCode( () =>
				{
					ecb.Playback( EntityManager );
					ecb.Dispose();
				}
				).WithoutBurst().Run();
		}

	}
}
