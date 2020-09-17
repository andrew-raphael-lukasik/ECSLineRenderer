using UnityEngine;

namespace EcsLineRenderer.Samples
{
    [AddComponentMenu("")]
    class TransformRotate : MonoBehaviour
    {

        void OnDrawGizmos ()
            => Update();
        
        void Update ()
            => transform.Rotate( 0f , 33f * Time.deltaTime , 0f );
        
    }
}
