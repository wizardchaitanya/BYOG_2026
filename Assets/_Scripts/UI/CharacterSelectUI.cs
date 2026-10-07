using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Character + chalk colour picker. Wire Prev / Next to the arrow buttons' OnClick.
public class CharacterSelectUI : MonoBehaviour
{
    public CharacterDatabase database;

    [Header("Preview")]
    public Image previewImage;
    public TMP_Text nameText;
    public TMP_Text counterText;             // optional, shows "2 / 5"
    public Image chalkLinePreview;           // optional: a thin line image that shows the chosen chalk colour

    [Header("Chalk colours")]
    public Button swatchPrefab;              // a Button with an Image; its colour is set from the palette
    public Transform swatchContainer;        // add a Horizontal/Grid Layout Group
    public float selectedSwatchScale = 1.25f;

    int index;
    readonly List<Button> swatches = new List<Button>();

    void OnEnable()
    {
        if (!database || database.Count == 0) { Debug.LogWarning("CharacterSelectUI: database is empty", this); return; }
        index = Mathf.Clamp(PlayerProfile.CharacterIndex, 0, database.Count - 1);
        BuildSwatches();
        Refresh();
    }

    public void Next() { Select(index + 1); }
    public void Prev() { Select(index - 1); }

    void Select(int i)
    {
        if (!database || database.Count == 0) return;
        index = (i + database.Count) % database.Count;
        PlayerProfile.CharacterIndex = index;
        PlayerProfile.ChalkColor = database.Get(index).defaultChalkColor;   // each character starts with its own colour
        AudioManager.UIClick();
        Refresh();
    }

    void PickColor(Color c)
    {
        PlayerProfile.ChalkColor = c;
        AudioManager.UIClick();
        Refresh();
    }

    void BuildSwatches()
    {
        if (swatches.Count > 0 || !swatchPrefab || !swatchContainer) return;
        foreach (var color in database.palette)
        {
            var b = Instantiate(swatchPrefab, swatchContainer);
            b.GetComponent<Image>().color = color;
            Color captured = color;
            b.onClick.AddListener(() => PickColor(captured));
            swatches.Add(b);
        }
    }

    void Refresh()
    {
        var c = database.Get(index);
        if (previewImage) { previewImage.sprite = c.MenuSprite; previewImage.preserveAspect = true; }
        if (nameText) nameText.text = c.displayName;
        if (counterText) counterText.text = (index + 1) + " / " + database.Count;

        Color chosen = PlayerProfile.ChalkColor;
        if (chalkLinePreview) chalkLinePreview.color = chosen;

        for (int i = 0; i < swatches.Count; i++)
        {
            bool selected = Near(database.palette[i], chosen);
            swatches[i].transform.localScale = Vector3.one * (selected ? selectedSwatchScale : 1f);
        }
    }

    static bool Near(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;
}
