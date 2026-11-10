using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeHudSlot : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text countText;
    [SerializeField] TMP_Text fallbackText;   // letra de respaldo si la mejora no tiene icono

    public void Setup(UpgradeData upgrade, int count)
    {
        bool hasIcon = upgrade.Icon != null;

        iconImage.sprite = upgrade.Icon;
        iconImage.enabled = hasIcon;

        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(!hasIcon);
            if (!hasIcon && !string.IsNullOrEmpty(upgrade.DisplayName))
                fallbackText.text = upgrade.DisplayName.Substring(0, 1).ToUpper();
        }

        if (countText != null)
        {
            bool isMax = count >= upgrade.MaxStacks;
            countText.gameObject.SetActive(count > 1 || isMax);
            countText.text = isMax ? "MAX" : $"x{count}";
        }
    }
}