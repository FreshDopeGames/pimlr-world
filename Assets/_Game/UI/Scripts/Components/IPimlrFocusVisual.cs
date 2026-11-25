// #UI: a selectable that draws its own focus/cursor highlight. PimlrMenuNavigator skips its generic pulsing ring
// for these, so the player only ever sees ONE cursor highlight (the themed one).
//
// Unreal note: CommonUI buttons draw their own focus brush; the equivalent is disabling the default
// focus visualization (Project Settings > Slate > "Show focus") for widgets that style focus themselves.

namespace PIMLR.UI
{
    public interface IPimlrFocusVisual
    {
        bool DrawsOwnFocus { get; }
    }
}
