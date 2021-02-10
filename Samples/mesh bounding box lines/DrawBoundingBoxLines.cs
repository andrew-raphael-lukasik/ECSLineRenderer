using UnityEngine;

using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

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
		
		void Awake ()
		{
			_meshRenderer = GetComponent<MeshRenderer>();

			// initialize segment pool:
			var world = Segments.Core.GetWorld();
			var entityManager = world.EntityManager;
			Entity prefab;

			if( _materialOverride!=null )
			{
				if( _widthOverride>0f )
					prefab = Segments.Core.GetSegmentPrefabCopy( _materialOverride , _widthOverride );
				else
					prefab = Segments.Core.GetSegmentPrefabCopy( _materialOverride );
			}
			else
			{
				if( _widthOverride>0f )
					prefab = Segments.Core.GetSegmentPrefabCopy( _widthOverride );
				else
					prefab = Segments.Core.GetSegmentPrefabCopy();
			}
			var sys = world.GetExistingSystem<Segments.Systems.NativeListToSegmentsSystem>();
			sys.CreateBatch( prefab , out _segments);
		}

		void OnDestroy ()
		{
			_segments.Dispose();
		}

		void Update ()
		{
			int index = 0;
			var bounds = _meshRenderer.bounds;
			Segments.Plot.Box(
				segments:	_segments ,
				index:		ref index ,
				size:		bounds.size ,
				pos:		bounds.center ,
				rot:		quaternion.identity
			);
		}

	}
}
