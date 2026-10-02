using UnityEngine;

/// <summary>Marks a place where a health pack may appear (e.g. inside a building).</summary>
public class PickupSpot : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
