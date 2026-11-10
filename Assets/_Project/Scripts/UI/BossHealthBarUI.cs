using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] NightTimer timer;
    [SerializeField] GameObject root;       
    [SerializeField] Slider slider;
    [SerializeField] TMP_Text nameLabel;    
    [SerializeField] string bossName = "LA LLORONA";
    [SerializeField] float smoothSpeed = 3f;

    Health bossHealth;
    bool tracking;

    void Awake() => root.SetActive(false);

    void OnEnable()  => timer.OnBossSpawned += Show;
    void OnDisable() => timer.OnBossSpawned -= Show;

    void Show(GameObject boss)
    {
        bossHealth = boss.GetComponentInChildren<Health>();
        if (bossHealth == null)
        {
            Debug.LogWarning("BossHealthBarUI: el jefe no tiene componente Health.");
            return;
        }

        if (nameLabel != null) nameLabel.text = bossName;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        tracking = true;
        root.SetActive(true);
        Debug.Log($"[UI] Barra del jefe mostrada ({bossHealth.Current:0}/{bossHealth.Max:0}).");
    }

    void Update()
    {
        if (!tracking) return;

        if (bossHealth == null) { Hide(); return; }   // el jefe fue destruido

        float max = bossHealth.Max;
        float pct = max > 0f ? Mathf.Clamp01(bossHealth.Current / max) : 0f;
        slider.value = Mathf.MoveTowards(slider.value, pct, smoothSpeed * Time.deltaTime);

        if (pct <= 0f && slider.value <= 0.001f) Hide();
    }

    void Hide()
    {
        tracking = false;
        root.SetActive(false);
    }
}