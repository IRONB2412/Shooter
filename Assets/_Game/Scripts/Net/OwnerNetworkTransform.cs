using Unity.Netcode.Components;

/// <summary>
/// NetworkTransform where the OWNER is the authority (client-authoritative for
/// players, server-authoritative for server-owned bots). This makes each player
/// move their own character responsively and have it replicate to everyone else.
/// </summary>
public class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
