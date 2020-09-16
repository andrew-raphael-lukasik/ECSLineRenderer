#if UNITY_EDITOR
using UnityEngine;
[AddComponentMenu("")]
class EDITOR_ONLY_rotor : MonoBehaviour
{
    void OnDrawGizmos () => Update();
    void Update () => transform.Rotate( 0f , 33f * Time.deltaTime , 0f );
}
#endif
