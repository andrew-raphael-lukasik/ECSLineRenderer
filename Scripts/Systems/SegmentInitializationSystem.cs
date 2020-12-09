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
	public class SegmentInitializationSystem : SystemBase
	{

		EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

		protected override void OnCreate ()
		{
			_endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
		}

		protected override void OnUpdate ()
		{
			var renderMesh = Prototypes.renderMesh;
			var renderBounds = Prototypes.renderBounds;
			var commands = _endSimulationEcbSystem.CreateCommandBuffer();

			#if ENABLE_HYBRID_RENDERER_V2
			var renderingLayer = new BuiltinMaterialPropertyUnity_RenderingLayer{ Value = new uint4{ x=(uint) renderMesh.layer } };
			var lightData = new BuiltinMaterialPropertyUnity_LightData{ Value = new float4{ z=1 } };
			#endif

			Entities
				.WithName("add_components_job_shared_segment_material_override_1")
				.WithNone<RenderMesh>()
				.WithAll<Segment>()
				.ForEach( ( in Entity entity , in SegmentSharedMaterialOverride material ) =>
				{
					renderMesh.material = material.Value;
					commands.AddSharedComponent( entity , renderMesh );
				})
				.WithoutBurst().Run();
			JobHandle job1 = Entities
				.WithName("add_components_job_shared_segment_material_override_2")
				.WithNone<RenderMesh>()
				.WithAll<SegmentSharedMaterialOverride,Segment>()
				.ForEach( ( in Entity entity ) =>
				{
					commands.RemoveComponent<SegmentSharedMaterialOverride>( entity );
					commands.AddComponent<LocalToWorld>( entity );
					commands.AddComponent<RenderBounds>( entity , renderBounds );
					commands.AddComponent<WorldRenderBounds>( entity );
					commands.AddComponent<SegmentAspectRatio>( entity );

					#if ENABLE_HYBRID_RENDERER_V2
					// commands.AddComponent<AmbientProbeTag>( entity );
					// commands.AddComponent<PerInstanceCullingTag>( entity );
					// commands.AddComponent<WorldToLocal_Tag>( entity );
					commands.AddComponent<BuiltinMaterialPropertyUnity_RenderingLayer>( entity , renderingLayer );
					commands.AddComponent<BuiltinMaterialPropertyUnity_LightData>( entity , lightData );
					#endif
				})
				.WithBurst().ScheduleParallel( Dependency );

			JobHandle job2 = Entities
				.WithName("add_components_job_1")
				.WithNone<RenderMesh>()
				.WithAll<Segment>()
				.ForEach( ( in Entity entity , in SegmentMaterialOverride material ) =>
				{
					renderMesh.material = material;
					commands.AddSharedComponent( entity , renderMesh );
				})
				.WithoutBurst().Schedule( Dependency );
			job2 = Entities
				.WithName("add_components_job_2")
				.WithNone<RenderMesh>()
				.WithAll<SegmentMaterialOverride,Segment>()
				.ForEach( ( in Entity entity ) =>
				{
					commands.RemoveComponent<SegmentMaterialOverride>( entity );
					commands.AddComponent<LocalToWorld>( entity );
					commands.AddComponent<RenderBounds>( entity , renderBounds );
					commands.AddComponent<WorldRenderBounds>( entity );
					commands.AddComponent<SegmentAspectRatio>( entity );

					#if ENABLE_HYBRID_RENDERER_V2
					// commands.AddComponent<AmbientProbeTag>( entity );
					// commands.AddComponent<PerInstanceCullingTag>( entity );
					// commands.AddComponent<WorldToLocal_Tag>( entity );
					commands.AddComponent<BuiltinMaterialPropertyUnity_RenderingLayer>( entity , renderingLayer );
					commands.AddComponent<BuiltinMaterialPropertyUnity_LightData>( entity , lightData );
					#endif
				})
				.WithBurst().ScheduleParallel( job2 );
			
			Dependency = JobHandle.CombineDependencies( job1 , job2 );

			_endSimulationEcbSystem.AddJobHandleForProducer( Dependency );
		}

	}
}
