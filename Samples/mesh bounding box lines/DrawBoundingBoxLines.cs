using UnityEngine;

using Unity.Mathematics;
using Unity.Entities;

namespace EcsLineRenderer.Samples
{
	[AddComponentMenu("")]
	[RequireComponent( typeof(MeshRenderer) )]
	class DrawBoundingBoxLines : MonoBehaviour
	{

		[SerializeField] Material _materialOverride = null;

		MeshRenderer _meshRenderer = null;
		Entity[] _entities;
		World _worldLR;
		EntityManager _commandLR;
		const int k_cube_vertices = 12;


		void OnEnable ()
		{
			_meshRenderer = GetComponent<MeshRenderer>();

			// make sure LR world exists:
			_worldLR = LineRendererWorld.GetOrCreateWorld();
			_commandLR = _worldLR.EntityManager;

			// initialize segment pool:
			if( _entities==null || _entities.Length==0 )
			{
				if( _materialOverride!=null )
					LineRendererWorld.InstantiatePool( k_cube_vertices , out _entities , _materialOverride );
				else
					LineRendererWorld.InstantiatePool( k_cube_vertices , out _entities );
			}
		}

		void OnDisable ()
		{
			if( LineRendererWorld.IsCreated )
				LineRendererWorld.Downsize( ref _entities , 0 );
		}

		void Update ()
		{
			var bounds = _meshRenderer.bounds;
			
			int index = 0;
			LineRendererWorld.Upsize( ref _entities , index+k_cube_vertices );
			Plot.Box(
				command:	_commandLR ,
				entities:	 _entities ,
				index:		ref index ,
				size:		bounds.size ,
				pos:		bounds.center ,
				rot:		quaternion.identity
			);
		}

	}
}
