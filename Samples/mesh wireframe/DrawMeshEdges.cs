using System.Collections.Generic;
using UnityEngine;

using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

namespace Segments.Samples
{
	[AddComponentMenu("")]
	[RequireComponent( typeof(MeshFilter) )]
	class DrawMeshEdges : MonoBehaviour
	{
		
		[SerializeField] Material _materialOverride = null;
		[SerializeField] float _widthOverride = 0.003f;

		NativeArray<Entity> _entities;
		Entity _prefab;
		EntityManager _commandLR;
		Vector3[] _vertices = null;
		int2[] _edges = null;


		void Awake ()
		{
			// create list of edges:
			var mf = GetComponent<MeshFilter>();
			{
				var mesh = mf.sharedMesh;
				_vertices = mesh.vertices;
				_edges = ToEdges( mesh.triangles );
			}

			// make sure LR world exists:
			var worldLR = Core.GetWorld();
			_commandLR = worldLR.EntityManager;

			// initialize segment pool:
			if( _entities==null || _entities.Length==0 )
			{
				if( _materialOverride!=null )
				{
					if( _widthOverride>0f )
						Core.InstantiatePool( _edges.Length , out _entities , out _prefab , _widthOverride , _materialOverride );
					else
						Core.InstantiatePool( _edges.Length , out _entities , out _prefab , _materialOverride );
				}
				else
				{
					if( _widthOverride>0f )
						Core.InstantiatePool( _edges.Length , out _entities , out _prefab , _widthOverride );
					else
						Core.InstantiatePool( _edges.Length , out _entities , out _prefab );
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
			Matrix4x4 matrix = transform.localToWorldMatrix;
			Core.Upsize( ref _entities , _prefab , index+_edges.Length );
			for( int i=0 ; i<_edges.Length ; i++ )
			{
				Entity entity = _entities[index++];
				int2 edgeIndices = _edges[i];
				_commandLR.SetComponentData( entity , new Segment{
						start	= matrix.MultiplyPoint( _vertices[edgeIndices.x] ) ,
						end		= matrix.MultiplyPoint( _vertices[edgeIndices.y] )
				} );
			}
		}


		int2[] ToEdges ( int[] triangles )
		{
			var edges = new Dictionary<ulong,int2>();
			for( int i=0 ; i<triangles.Length ; i+=3 )
			{
				int a = triangles[i];
				int b = triangles[i+1];
				int c = triangles[i+2];
				ulong hash;
				
				hash = (ulong)math.max(a,b)*(ulong)1e6 + (ulong)math.min(a,b);
				if( !edges.ContainsKey(hash) )
					edges.Add( hash , new int2(a,b) );
				
				hash = (ulong)math.max(b,c)*(ulong)1e6 + (ulong)math.min(b,c);
				if( !edges.ContainsKey(hash) )
					edges.Add( hash , new int2(b,c) );

				hash = (ulong)math.max(c,a)*(ulong)1e6 + (ulong)math.min(c,a);
				if( !edges.ContainsKey(hash) )
					edges.Add( hash , new int2(c,a) );
			}
			var results = new int2[ edges.Count ];
			edges.Values.CopyTo( results , 0 );
			return results;
		}
		

	}
}
