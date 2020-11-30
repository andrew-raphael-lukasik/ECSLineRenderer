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
			var renderingLayer = new BuiltinMaterialPropertyUnity_RenderingLayer{ Value = new uint4{ x=(uint) renderMesh.layer } };
			var lightData = new BuiltinMaterialPropertyUnity_LightData{ Value = new float4{ z=1 } };
			var command = _endSimulationEcbSystem.CreateCommandBuffer();

			Entities
				.WithName("add_components_job_shared_segment_material_override_1")
				.WithNone<RenderMesh>()
				.WithAll<Segment>()
				.ForEach( ( in Entity entity , in SegmentSharedMaterialOverride material ) =>
				{
					renderMesh.material = material.Value;
					command.AddSharedComponent( entity , renderMesh );
				})
				.WithoutBurst().Run();
			JobHandle job1 = Entities
				.WithName("add_components_job_shared_segment_material_override_2")
				.WithNone<RenderMesh>()
				.WithAll<SegmentSharedMaterialOverride,Segment>()
				.ForEach( ( in Entity entity ) =>
				{
					command.RemoveComponent<SegmentSharedMaterialOverride>( entity );
					command.AddComponent<LocalToWorld>( entity );
					command.AddComponent<RenderBounds>( entity , renderBounds );
					command.AddComponent<WorldRenderBounds>( entity );
					command.AddComponent<SegmentAspectRatio>( entity );

					command.AddComponent<AmbientProbeTag>( entity );
					command.AddComponent<PerInstanceCullingTag>( entity );
					command.AddComponent<WorldToLocal_Tag>( entity );
					command.AddComponent<BuiltinMaterialPropertyUnity_RenderingLayer>( entity , renderingLayer );
					command.AddComponent<BuiltinMaterialPropertyUnity_LightData>( entity , lightData );
				})
				.WithBurst().ScheduleParallel( Dependency );

			JobHandle job2 = Entities
				.WithName("add_components_job_1")
				.WithNone<RenderMesh>()
				.WithAll<Segment>()
				.ForEach( ( in Entity entity , in SegmentMaterialOverride material ) =>
				{
					renderMesh.material = material;
					command.AddSharedComponent( entity , renderMesh );
				})
				.WithoutBurst().Schedule( Dependency );
			job2 = Entities
				.WithName("add_components_job_2")
				.WithNone<RenderMesh>()
				.WithAll<SegmentMaterialOverride,Segment>()
				.ForEach( ( in Entity entity ) =>
				{
					command.RemoveComponent<SegmentMaterialOverride>( entity );
					command.AddComponent<LocalToWorld>( entity );
					command.AddComponent<RenderBounds>( entity , renderBounds );
					command.AddComponent<WorldRenderBounds>( entity );
					command.AddComponent<SegmentAspectRatio>( entity );

					command.AddComponent<AmbientProbeTag>( entity );
					command.AddComponent<PerInstanceCullingTag>( entity );
					command.AddComponent<WorldToLocal_Tag>( entity );
					command.AddComponent<BuiltinMaterialPropertyUnity_RenderingLayer>( entity , renderingLayer );
					command.AddComponent<BuiltinMaterialPropertyUnity_LightData>( entity , lightData );
				})
				.WithBurst().ScheduleParallel( job2 );
			
			Dependency = JobHandle.CombineDependencies( job1 , job2 );

			_endSimulationEcbSystem.AddJobHandleForProducer( Dependency );
		}

	}
}
