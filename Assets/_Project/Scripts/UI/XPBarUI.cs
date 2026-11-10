using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class XPBarUI : MonoBehaviour
{
    [SerializeField] PlayerXP target;
    [SerializeField] TMP_Text levelLabel;
    Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        if (target == null) Debug.LogError("XPBarUI: falta asignar PlayerXP.", this);
    }

    void OnEnable()
    {
        if (target == null) return;
        target.OnXPChanged += Refresh;
        Refresh(target.CurrentXP, target.XpToNextLevel, target.Level);
    }

    void OnDisable()
    {
        if (target != null) target.OnXPChanged -= Refresh;
    }

    void Refresh(int current, int needed, int level)
    {
        slider.maxValue = needed;
        slider.value = current;
        if (levelLabel != null) levelLabel.text = $"Nv. {level}";
    }
}