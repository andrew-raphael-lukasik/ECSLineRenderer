using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Transforms;

namespace EcsLineRenderer
{
	[WorldSystemFilter(0)]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	[UpdateBefore(typeof(SegmentInitializationSystem))]
	public class CreateSegmentsSystem : SystemBase
	{

		EntityArchetype _segmentArchetype;
		EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

		protected override void OnCreate ()
		{
			_segmentArchetype = EntityManager.CreateArchetype(
					typeof(Segment)
				,	typeof(SegmentWidth)
				,	typeof(SegmentMaterialOverride)
				,	typeof(MaterialColor)
			);
			_endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
		}

		protected override void OnUpdate ()
		{
			var segmentArchetype = _segmentArchetype;
			var defaultSegmentMaterial = Internal.ResourceProvider.default_segment_material;
			var commands = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();

			Entities
				.WithName("add_components_job")
				.WithNone<SegmentMaterialOverride>()
				.ForEach( ( in int entityInQueryIndex , in Entity entity , in DynamicBuffer<CreateSegmentsBufferElement> buffer ) =>
				{
					int len = buffer.Length;
					for( int i=0 ; i<len ; i++ )
					{
						var seg = buffer[i];
						var instance = commands.CreateEntity( entityInQueryIndex , segmentArchetype );
						commands.SetComponent( entityInQueryIndex , instance , new Segment{
							start	= seg.start ,
							end		= seg.end
						} );
						commands.SetComponent( entityInQueryIndex , instance , new SegmentWidth{
							Value	= seg.width
						} );
						commands.SetComponent( entityInQueryIndex , instance , defaultSegmentMaterial );
						commands.SetComponent( entityInQueryIndex , instance , new MaterialColor{
							Value = new float4{ x=seg.color.r , y=seg.color.g , z=seg.color.b , w=seg.color.a }
						} );
					}

					commands.DestroyEntity( entityInQueryIndex , entity );
				} )
				.WithBurst().ScheduleParallel();

			Entities
				.WithName("add_components_job_material_override")
				.ForEach( ( in int entityInQueryIndex , in Entity entity , in DynamicBuffer<CreateSegmentsBufferElement> buffer , in SegmentMaterialOverride material ) =>
				{
					int len = buffer.Length;
					for( int i=0 ; i<len ; i++ )
					{
						var seg = buffer[i];
						var instance = commands.CreateEntity( entityInQueryIndex , segmentArchetype );
						commands.SetComponent( entityInQueryIndex , instance , new Segment{
							start	= seg.start ,
							end		= seg.end
						} );
						commands.SetComponent( entityInQueryIndex , instance , new SegmentWidth{
							Value	= seg.width
						} );
						commands.SetComponent( entityInQueryIndex , instance , material );
						commands.SetComponent( entityInQueryIndex , instance , new MaterialColor{
							Value = new float4{ x=seg.color.r , y=seg.color.g , z=seg.color.b , w=seg.color.a }
						} );
					}

					commands.DestroyEntity( entityInQueryIndex , entity );
				} )
				.WithBurst().ScheduleParallel();
			
			_endSimulationEcbSystem.AddJobHandleForProducer( Dependency );
		}

	}
}
