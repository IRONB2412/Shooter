/// <summary>
/// Global state for the HUD layout editor: whether we're arranging widgets, the
/// registry of movable widgets, and which one is currently selected for resizing.
/// (Not a MonoBehaviour — pure static state.)
/// </summary>
public static class HUDLayout
{
    public static bool EditMode;
    public static DraggableHUDElement Selected;
    public static readonly System.Collections.Generic.List<DraggableHUDElement> Elements = new();
}
