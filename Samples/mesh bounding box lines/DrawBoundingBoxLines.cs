using UnityEngine;

using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;

namespace Segments.Samples
{
	[AddComponentMenu("")]
	[RequireComponent( typeof(MeshRenderer) )]
	class DrawBoundingBoxLines : MonoBehaviour
	{

		[SerializeField] Material _materialOverride = null;
		[SerializeField] float _widthOverride = 0.003f;

		MeshRenderer _meshRenderer = null;
		NativeList<float3x2> _segments;
		Segments.NativeListToSegmentsSystem _segmentsSystem;
		public JobHandle Dependency;
		
		void OnEnable ()
		{
			_meshRenderer = GetComponent<MeshRenderer>();

			var world = Segments.Core.GetWorld();
			_segmentsSystem = world.GetExistingSystem<Segments.NativeListToSegmentsSystem>();

			// initialize segment list:
			Entity prefab;
			if( _materialOverride!=null )
			{
				if( _widthOverride>0f ) prefab = Segments.Core.GetSegmentPrefabCopy( _materialOverride , _widthOverride );
				else prefab = Segments.Core.GetSegmentPrefabCopy( _materialOverride );
			}
			else
			{
				if( _widthOverride>0f ) prefab = Segments.Core.GetSegmentPrefabCopy( _widthOverride );
				else prefab = Segments.Core.GetSegmentPrefabCopy();
			}
			_segmentsSystem.CreateBatch( prefab , out _segments );
		}

		void OnDisable ()
		{
			Dependency.Complete();
			if( _segments.IsCreated ) _segments.Dispose();
		}

		void Update ()
		{
			Dependency.Complete();

			var job = new MyJob{
				bounds		= _meshRenderer.bounds ,
				segments	= _segments
			};

			Dependency = job.Schedule();
			_segmentsSystem.Dependencies.Add( Dependency );
		}

		public struct MyJob : IJob
		{
			[ReadOnly] public Bounds bounds;
			public NativeList<float3x2> segments;
			void IJob.Execute ()
			{
				int index = 0;
				Segments.Plot.Box(
					segments:	segments ,
					index:		ref index ,
					size:		bounds.size ,
					pos:		bounds.center ,
					rot:		quaternion.identity
				);
				segments.Length = index;
			}
		}

	}
}
