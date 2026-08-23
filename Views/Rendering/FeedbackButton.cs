using Blish_HUD.Controls;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// A <see cref="StandardButton"/> that answers a press. Every button in
    /// this module is one of these; a bare StandardButton acknowledges a
    /// click with nothing at all.
    /// <para>
    /// Measured from the vendored Blish HUD 1.3.0 binary (ilspycmd) - what
    /// StandardButton does and does not already do:
    /// </para>
    /// <list type="bullet">
    /// <item>Hover: <c>OnMouseEntered</c>/<c>OnMouseLeft</c> tween
    /// AnimationState 0 &lt;-&gt; 8 over 0.25s, stepping through the
    /// "common/button-states" atlas. That works, and is left alone.</item>
    /// <item>Press: nothing. There is no OnLeftMouseButtonPressed override
    /// and no pressed frame in the atlas walk - the button looks identical
    /// held down as hovered.</item>
    /// <item>Sound: <c>OnClick</c> calls
    /// <c>PlaySoundEffectByName("audio\\button-click")</c>, but
    /// ContentService's audio reader is already rooted at ref.dat's "audio"
    /// folder, so the lookup becomes audio/audio/button-click.wav, fails the
    /// FileExists check, and returns silently. StandardButton is mute in
    /// 1.3.0; Checkbox and GlowButton, which pass the unprefixed
    /// "button-click", are not.</item>
    /// </list>
    /// <para>
    /// So the gap is press shading and sound, both supplied by
    /// <see cref="PressFeedback"/>. If a later Blish release fixes the
    /// double-prefixed path, this button will play the sound twice on a
    /// completed click and the PlayClick call in PressFeedback.Wire is what
    /// to drop.
    /// </para>
    /// </summary>
    internal class FeedbackButton : StandardButton
    {
        internal FeedbackButton()
        {
            PressFeedback.Wire(this);
        }
    }
}
