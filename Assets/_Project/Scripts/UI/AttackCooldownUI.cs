
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class AttackCooldownUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private MacheteAttack attack;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        slider.interactable = false;

        if (attack == null)
            Debug.LogError("AttackCooldownUI: falta asignar MacheteAttack.", this);
    }

    private void Update()
    {
        if (attack == null) return;

        slider.value = attack.CooldownProgress;
    }
}
