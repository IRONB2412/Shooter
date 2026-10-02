/// <summary>
/// One place for every draw-order value (SpriteRenderer / Tilemap / Particle sortingOrder, all on the
/// Default sorting layer). Higher draws on top. Keep new objects in the right band so the player can
/// never end up hidden under something it should be above.
///
///   0  Floor tiles                       12-13 Tall grass (hides bots and everything else)
///   1  Walls / destructible blocks       14-17 Blood and ground effects
///   3  Foliage & rock decor              18    Aim line (always readable, even in grass)
///   5  Doors                             19    Smoke clouds (hide bots, not the local player)
///   6  Props (crates, barrels)           20    LOCAL PLAYER — never covered by grass or smoke
///   7  Health packs                      21-27 Muzzle flashes, impacts, explosions, flash-bangs
///   8  Bullet trails, blast-radius disk
///   9  Bullets, thrown grenades
///   10 Bots and remote players
/// </summary>
public static class SortingOrders
{
    public const int Decor = 3;
    public const int Props = 6;
    public const int HealthPack = 7;
    public const int Projectiles = 9;
    public const int Characters = 10;
    public const int Grass = 12;
    public const int AimLine = 18;
    public const int Smoke = 19;
    public const int LocalPlayer = 20;
}
