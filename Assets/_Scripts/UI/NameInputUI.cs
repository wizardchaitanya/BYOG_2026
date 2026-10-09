using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Name pop-up. First launch: forced (no cancel). Later: opened by a "Change name" button -> Open().
public class NameInputUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_InputField input;
    public Button confirmButton;
    public Button cancelButton;           // hidden on first launch
    public TMP_Text errorText;            // optional
    public TMP_Text currentNameText;      // optional: shows the saved name on the menu
    public int minLength = 2;
    public int maxLength = 12;

    bool required;

    void Start()
    {
        input.characterLimit = maxLength;
        input.onValidateInput += (text, i, ch) => (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_') ? ch : '\0';
        input.onSubmit.AddListener(_ => Confirm());               // Enter key
        confirmButton.onClick.AddListener(Confirm);
        if (cancelButton) cancelButton.onClick.AddListener(Cancel);

        Refresh();
        if (PlayerProfile.HasName) panel.SetActive(false);
        else Show(true);
    }

    // hook this to the "Change name" button
    public void Open() { AudioManager.UIClick(); Show(!PlayerProfile.HasName); }

    void Show(bool mustEnter)
    {
        required = mustEnter;
        panel.SetActive(true);
        input.text = PlayerProfile.HasName ? PlayerProfile.PlayerName : "";
        if (errorText) errorText.text = "";
        if (cancelButton) cancelButton.gameObject.SetActive(!mustEnter);
        input.ActivateInputField();
    }

    void Confirm()
    {
        string n = PlayerProfile.Clean(input.text);
        if (n.Length < minLength)
        {
            if (errorText) errorText.text = "Name must be at least " + minLength + " characters";
            return;
        }
        AudioManager.UIClick();
        PlayerProfile.PlayerName = n;
        PhotonNetwork.NickName = n;
        Refresh();
        panel.SetActive(false);
    }

    void Cancel()
    {
        if (required) return;
        AudioManager.UIClick();
        panel.SetActive(false);
    }

    void Refresh()
    {
        if (currentNameText) currentNameText.text = PlayerProfile.HasName ? PlayerProfile.PlayerName : "";
    }
}