using UnityEngine;

using Unity.Mathematics;
using Unity.Entities;

namespace EcsLineRenderer.Samples
{
	[AddComponentMenu("")]
	[RequireComponent( typeof(MeshRenderer) )]
	class DrawBoundingBoxLines : MonoBehaviour
	{

		MeshRenderer _meshRenderer = null;

		static Entity[] _entities;
		static int _index = 0;
		static World _worldLR;
		static EntityManager _commandLR;

		const int k_cube_vertices = 12;

		void Start ()
		{
			_meshRenderer = GetComponent<MeshRenderer>();

			// make sure LR world exists:
			if( _worldLR==null || !_worldLR.IsCreated )
			{
				_worldLR = LineRendererWorld.GetOrCreateWorld();
				_commandLR = _worldLR.EntityManager;
			}

			// initialize segment pool:
			LineRendererWorld.InstantiatePool( k_cube_vertices , out _entities );
		}

		void Update ()
		{
			var bounds = _meshRenderer.bounds;
			
			LineRendererWorld.Upsize( ref _entities , _index+k_cube_vertices );
			Plot.Box(
				command:	_commandLR ,
				entities:	 _entities ,
				index:		ref _index ,
				size:		bounds.size ,
				pos:		bounds.center ,
				rot:		quaternion.identity
			);
		}

		void LateUpdate ()
		{
			// reset shared index:
			_index = 0;
		}

		void OnDestroy ()
		{
			LineRendererWorld.Downsize( ref _entities , _entities.Length-k_cube_vertices );
		}

	}
}