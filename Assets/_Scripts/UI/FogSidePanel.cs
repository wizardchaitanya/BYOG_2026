using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sits on a UI panel placed over one half of the board (where the fog appears).
// Holds the instruction text and the Done button. MPDrawUI shows the one on the opponent's side.
public class FogSidePanel : MonoBehaviour
{
    public TMP_Text instructionText;
    public TMP_Text timerText;      // optional
    public Button doneButton;
    public TMP_Text doneText;       // optional
}