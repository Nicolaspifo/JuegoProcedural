using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UpgradeCardView : MonoBehaviour
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] Image iconImage;

    Button button;
    public Button Button
    {
        get
        {
            if (button == null) button = GetComponent<Button>();
            return button;
        }
    }

    public void Setup(UpgradeData upgrade, System.Action<UpgradeData> onPicked)
    {
        Debug.Log($"upgrade:{upgrade != null} title:{titleText != null} desc:{descriptionText != null}", this);
        titleText.text = upgrade.DisplayName;
        descriptionText.text = upgrade.Description;

        if (iconImage != null)
        {
            iconImage.sprite = upgrade.Icon;
            iconImage.enabled = upgrade.Icon != null;
        }

        Button.onClick.RemoveAllListeners();
        Button.onClick.AddListener(() => onPicked(upgrade));
    }
}