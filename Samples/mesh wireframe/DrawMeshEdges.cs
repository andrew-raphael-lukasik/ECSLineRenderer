using System.Collections.Generic;
using UnityEngine;

using Unity.Mathematics;
using Unity.Entities;

namespace EcsLineRenderer.Samples
{
	[AddComponentMenu("")]
	[RequireComponent( typeof(MeshFilter) )]
	class DrawMeshEdges : MonoBehaviour
	{
		
		[SerializeField] Material _materialOverride = null;
		[SerializeField] float _widthOverride = 0.003f;

		Entity[] _entities;
		EntityManager _commandLR;
		Vector3[] _vertices = null;
		(int start,int end)[] _edges = null;


		void OnEnable ()
		{
			// create list of edges:
			var mf = GetComponent<MeshFilter>();
			{
				var mesh = mf.sharedMesh;
				_vertices = mesh.vertices;
				var triangles = mesh.triangles;
				var edges = new Dictionary<ulong,(int start,int end)>();
				for( int i=0 ; i<triangles.Length ; i+=3 )
				{
					int a = triangles[i];
					int b = triangles[i+1];
					int c = triangles[i+2];
					ulong hash;
					
					hash = (ulong)math.max(a,b)*(ulong)1e6 + (ulong)math.min(a,b);
					if( !edges.ContainsKey(hash) )
						edges.Add( hash , (a,b) );
					
					hash = (ulong)math.max(b,c)*(ulong)1e6 + (ulong)math.min(b,c);
					if( !edges.ContainsKey(hash) )
						edges.Add( hash , (b,c) );

					hash = (ulong)math.max(c,a)*(ulong)1e6 + (ulong)math.min(c,a);
					if( !edges.ContainsKey(hash) )
						edges.Add( hash , (c,a) );
				}
				_edges = new (int start, int end)[ edges.Count ];
				edges.Values.CopyTo( _edges , 0 );
			}

			// make sure LR world exists:
			var worldLR = LineRendererWorld.GetOrCreateWorld();
			_commandLR = worldLR.EntityManager;

			// initialize segment pool:
			if( _entities==null || _entities.Length==0 )
			{
				if( _materialOverride!=null )
				{
					if( _widthOverride>0f )
						LineRendererWorld.InstantiatePool( _edges.Length , out _entities , _widthOverride , _materialOverride );
					else
						LineRendererWorld.InstantiatePool( _edges.Length , out _entities , _materialOverride );
				}
				else
				{
					if( _widthOverride>0f )
						LineRendererWorld.InstantiatePool( _edges.Length , out _entities , _widthOverride );
					else
						LineRendererWorld.InstantiatePool( _edges.Length , out _entities );
				}
			}
		}

		void OnDisable ()
		{
			if( LineRendererWorld.IsCreated )
				LineRendererWorld.Downsize( ref _entities , 0 );
		}

		void Update ()
		{
			int index = 0;
			Matrix4x4 matrix = transform.localToWorldMatrix;
			LineRendererWorld.Upsize( ref _entities , index+_edges.Length );
			for( int i=0 ; i<_edges.Length ; i++ )
			{
				Entity entity = _entities[index++];
				var edgeIndices = _edges[i];
				_commandLR.SetComponentData( entity , new Segment {
						start	= matrix.MultiplyPoint( _vertices[edgeIndices.start] ) ,
						end		= matrix.MultiplyPoint( _vertices[edgeIndices.end] )
				} );
			}
		}

	}
}
