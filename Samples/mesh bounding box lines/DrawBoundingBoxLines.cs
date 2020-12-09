using UnityEngine;

using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

namespace EcsLineRenderer.Samples
{
	[AddComponentMenu("")]
	[RequireComponent( typeof(MeshRenderer) )]
	class DrawBoundingBoxLines : MonoBehaviour
	{

		[SerializeField] Material _materialOverride = null;
		[SerializeField] float _widthOverride = 0.003f;

		MeshRenderer _meshRenderer = null;
		NativeArray<Entity> _entities;
		Entity _prefab;
		const int k_cube_vertices = 12;


		void Awake ()
		{
			_meshRenderer = GetComponent<MeshRenderer>();

			// make sure LR world exists:
			var worldLR = LineRendererWorld.GetOrCreateWorld();

			// initialize segment pool:
			if( _entities==null || _entities.Length==0 )
			{
				if( _materialOverride!=null )
				{
					if( _widthOverride>0f )
						LineRendererWorld.InstantiatePool( k_cube_vertices , out _entities , out _prefab , _widthOverride , _materialOverride );
					else
						LineRendererWorld.InstantiatePool( k_cube_vertices , out _entities , out _prefab , _materialOverride );
				}
				else
				{
					if( _widthOverride>0f )
						LineRendererWorld.InstantiatePool( k_cube_vertices , out _entities , out _prefab , _widthOverride );
					else
						LineRendererWorld.InstantiatePool( k_cube_vertices , out _entities , out _prefab );
				}
			}
		}

		void OnDestroy ()
		{
			_entities.Dispose();
		}

		void Update ()
		{
			int index = 0;
			var bounds = _meshRenderer.bounds;
			var commands = LineRendererWorld.CreateCommandBuffer();
			LineRendererWorld.Upsize( ref _entities , _prefab , index+k_cube_vertices );
			Plot.Box(
				commands:	commands ,
				entities:	_entities ,
				index:		ref index ,
				size:		bounds.size ,
				pos:		bounds.center ,
				rot:		quaternion.identity
			);
		}

	}
}
