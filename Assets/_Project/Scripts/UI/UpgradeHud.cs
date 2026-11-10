using System.Collections.Generic;
using UnityEngine;

public class UpgradeHud : MonoBehaviour
{
    [SerializeField] PlayerRuntimeStats stats;
    [SerializeField] RectTransform container;
    [SerializeField] UpgradeHudSlot slotPrefab;
    [SerializeField] List<UpgradeData> catalog;   // todas las mejoras, en el orden en que quieres verlas

    readonly Dictionary<UpgradeData, UpgradeHudSlot> slots = new Dictionary<UpgradeData, UpgradeHudSlot>();

    void Awake()
    {
        // Una casilla por mejora, creada oculta y en el orden del catálogo
        foreach (var upgrade in catalog)
        {
            if (upgrade == null || slots.ContainsKey(upgrade)) continue;

            var slot = Instantiate(slotPrefab, container);
            slot.gameObject.SetActive(false);
            slots[upgrade] = slot;
        }
    }

    void OnEnable()
    {
        stats.StatsChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (stats != null) stats.StatsChanged -= Refresh;
    }

    void Refresh()
    {
        foreach (var pair in slots)
        {
            int count = stats.CountOf(pair.Key);
            bool owned = count > 0;

            pair.Value.gameObject.SetActive(owned);
            if (owned) pair.Value.Setup(pair.Key, count);
        }
    }
}