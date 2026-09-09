using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Drives the main menu's description text (MRM-18) with this button's own message whenever
    /// the button becomes selected (gamepad D-pad navigation, gated by
    /// <see cref="MenuInputSchemeController"/> so it never fires in Keyboard &amp; Mouse scheme)
    /// or the mouse hovers over it. One instance per button, all pointing at the same shared
    /// <see cref="descriptionText"/>.
    ///
    /// <para><b>2026-09-08 correction:</b> the text must show nothing until something is actually
    /// hovered or selected - it must not default to the first button's text on menu load just
    /// because that button happens to be selected in gamepad scheme. <see cref="OnPointerExit"/>
    /// restores whatever the EventSystem's currently-selected object's own text is (or blanks it
    /// if nothing is selected) rather than simply clearing to blank - that keeps a gamepad user's
    /// selected button's text visible after a mouse graze, while a mouse-only user (nothing ever
    /// selected) correctly lands on blank.</para>
    /// </summary>
    public sealed class ButtonDescriptionDisplay : MonoBehaviour, ISelectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text descriptionText;

        [TextArea]
        [SerializeField] private string description;

        public void OnSelect(BaseEventData eventData)
        {
            descriptionText.text = description;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            descriptionText.text = description;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            ButtonDescriptionDisplay selectedDisplay = selected != null ? selected.GetComponent<ButtonDescriptionDisplay>() : null;
            descriptionText.text = selectedDisplay != null ? selectedDisplay.description : string.Empty;
        }
    }
}
