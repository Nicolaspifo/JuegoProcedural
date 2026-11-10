using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UpgradeCardsUI : MonoBehaviour
{
    [SerializeField] PlayerXP playerXP;
    [SerializeField] PlayerRuntimeStats stats;
    [SerializeField] Health playerHealth;
    [SerializeField] GameObject panel;
    [SerializeField] UpgradeCardView[] cards;
    [SerializeField] List<UpgradeData> pool;
    [Tooltip("Cartas de relleno (solo curan, sin limite de usos). Salen cuando se agotan las normales o rellenan huecos.")]
    [SerializeField] List<UpgradeData> fallbackPool;
    [SerializeField] float inputDelay = 0.4f;

    public event System.Action<UpgradeData> OnUpgradeChosen;

    int pendingLevelUps;
    bool isOpen;

    bool IsAvailable(UpgradeData u) => stats == null || stats.CanApply(u);

    // Hay algo que ofrecer si queda una carta normal o una de relleno.
    bool HasOptions() =>
        (pool != null && pool.Exists(IsAvailable)) ||
        (fallbackPool != null && fallbackPool.Exists(IsAvailable));

    void OnEnable()
    {
        if (playerXP != null) playerXP.OnLevelUp += HandleLevelUp;
        else Debug.LogError("UpgradeCardsUI: falta asignar PlayerXP.", this);
    }

    void OnDisable()
    {
        if (playerXP != null) playerXP.OnLevelUp -= HandleLevelUp;
        PauseControl.Pop(this);
    }

    void HandleLevelUp(int level)
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (!HasOptions()) return;

        // Agotadas las cartas normales, si solo queda UNA de relleno se aplica directamente: sin pausa
        // (elegir siempre lo mismo en cada nivel romperia el ritmo). Con 2 o mas se muestran las cartas.
        bool hasNormal = pool != null && pool.Exists(IsAvailable);
        if (!hasNormal && fallbackPool != null)
        {
            var fill = fallbackPool.FindAll(IsAvailable);
            if (fill.Count == 1)
            {
                if (stats != null) stats.Apply(fill[0]);
                OnUpgradeChosen?.Invoke(fill[0]);
                return;
            }
        }

        pendingLevelUps++;
        if (!isOpen) Open();
    }

    void Open()
    {
        if (cards == null || cards.Length == 0)
        {
            Debug.LogWarning("UpgradeCardsUI: falta asignar las cartas en Cards.");
            pendingLevelUps = 0;
            return;
        }

        isOpen = true;
        pendingLevelUps--;
        PauseControl.Push(this);
        panel.SetActive(true);
        ShowRandomCards();
        StartCoroutine(EnableInputAfterDelay());
    }

    void ShowRandomCards()
    {
        var options = pool != null ? pool.FindAll(IsAvailable) : new List<UpgradeData>();
        Shuffle(options);

        // Si no hay cartas normales para todos los huecos, se completan con las de relleno.
        if (options.Count < cards.Length && fallbackPool != null)
        {
            var fill = fallbackPool.FindAll(IsAvailable);
            Shuffle(fill);
            for (int i = 0; i < fill.Count && options.Count < cards.Length; i++)
                options.Add(fill[i]);
        }

        for (int i = 0; i < cards.Length; i++)
        {
            bool hasOption = i < options.Count;
            cards[i].gameObject.SetActive(hasOption);
            if (!hasOption) continue;

            cards[i].Setup(options[i], Choose);
            cards[i].Button.interactable = false;
        }
    }

    static void Shuffle(List<UpgradeData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    IEnumerator EnableInputAfterDelay()
    {
        yield return new WaitForSecondsRealtime(inputDelay);

        foreach (var card in cards)
            if (card.gameObject.activeSelf) card.Button.interactable = true;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(cards[0].gameObject);
    }

    void Choose(UpgradeData upgrade)
    {
        if (stats != null) stats.Apply(upgrade);
        OnUpgradeChosen?.Invoke(upgrade);

        panel.SetActive(false);
        isOpen = false;

        if (pendingLevelUps > 0 && HasOptions()) Open();
        else
        {
            pendingLevelUps = 0;
            PauseControl.Pop(this);
        }
    }

    [ContextMenu("Probar: abrir cartas")]
    void TestOpen() => HandleLevelUp(0);
}