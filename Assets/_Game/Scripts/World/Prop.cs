using UnityEngine;

/// <summary>
/// Marker for a solid, indestructible prop (cabinet, trolley, crate...). All props
/// share this one component and behaviour; only their sprite differs, which the
/// Level Builder assigns per placement. Sits on the Obstacle layer so it blocks
/// movement, bullets and line-of-sight, and grenades can't carve it.
/// </summary>
public class Prop : MonoBehaviour { }
