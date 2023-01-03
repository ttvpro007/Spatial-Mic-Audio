using UnityEngine;

public class SphereArea : MonoBehaviour
{
    public float radius = 1f;

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
