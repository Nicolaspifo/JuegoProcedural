using UnityEngine;

public class SfxEmitter : MonoBehaviour
{
    public enum Trigger
    {
        [Tooltip("Cuando el jugador golpea con el machete (MacheteAttack).")]
        PlayerAttack,

        [Tooltip("Cuando el enemigo conecta su ataque cuerpo a cuerpo (EnemyContactDamage).")]
        EnemyAttack,

        [Tooltip("Cuando el enemigo empieza a cargar su ataque (EnemyContactDamage). Ideal para un rugido de aviso.")]
        EnemyWindup,

        [Tooltip("Cuando la Llorona empieza su grito (CryAttack).")]
        LloronaCry,

        [Tooltip("Cuando este objeto recibe dano (Health). Por defecto ignora el veneno.")]
        DamageReceived,

        [Tooltip("Cuando este objeto muere (Health). Sirve para el grito de muerte de la Llorona.")]
        Death,

        [Tooltip("Solo suena cuando otro script llama a Play().")]
        Manual
    }

    [Header("Cuando suena")]
    [SerializeField] private Trigger playOn = Trigger.Manual;

    [Header("Clips (se elige uno al azar)")]
    [SerializeField] private AudioClip[] clips;

    [Header("Mezcla")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Tooltip("Variacion aleatoria de tono. 0 = siempre igual. Valores chicos (0.03 a 0.08) dan variedad.")]
    [SerializeField, Range(0f, 0.5f)] private float pitchJitter = 0f;

    [Tooltip("Con 2 o mas clips, evita que el mismo suene dos veces seguidas.")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Tooltip("En DamageReceived, ignora el dano por veneno (recomendado para no saturar).")]
    [SerializeField] private bool ignorePoison = true;

    private Health health;
    private int lastIndex = -1;

    private void OnEnable()
    {
        switch (playOn)
        {
            case Trigger.PlayerAttack:
                if (TryGetComponent(out MacheteAttack attack)) attack.Attacked += OnAttack;
                else WarnMissing("MacheteAttack");
                break;

            case Trigger.EnemyAttack:
                if (TryGetComponent(out EnemyContactDamage enemy)) enemy.Attacked += Play;
                else WarnMissing("EnemyContactDamage");
                break;

            case Trigger.EnemyWindup:
                if (TryGetComponent(out EnemyContactDamage windup)) windup.WindupStarted += OnWindup;
                else WarnMissing("EnemyContactDamage");
                break;

            case Trigger.LloronaCry:
                if (TryGetComponent(out CryAttack cry)) cry.CryStarted += Play;
                else WarnMissing("CryAttack");
                break;

            case Trigger.DamageReceived:
                health = GetComponent<Health>();
                if (health != null) Health.AnyDamaged += OnAnyDamaged;
                else WarnMissing("Health");
                break;

            case Trigger.Death:
                health = GetComponent<Health>();
                if (health != null) health.Died += Play;
                else WarnMissing("Health");
                break;
        }
    }

    private void OnDisable()
    {
        
        if (TryGetComponent(out MacheteAttack attack)) attack.Attacked -= OnAttack;

        if (TryGetComponent(out EnemyContactDamage enemy))
        {
            enemy.Attacked -= Play;
            enemy.WindupStarted -= OnWindup;
        }

        if (TryGetComponent(out CryAttack cry)) cry.CryStarted -= Play;

        Health.AnyDamaged -= OnAnyDamaged;

        if (health != null) health.Died -= Play;
        health = null;
    }

    
    public void Play()
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = PickClip();
        if (clip == null) return;

        float pitch = pitchJitter > 0f
            ? 1f + Random.Range(-pitchJitter, pitchJitter)
            : 1f;

        Sfx.Play(clip, volume, pitch);
    }

    private AudioClip PickClip()
    {
        if (clips.Length == 1) return clips[0];

        int index = Random.Range(0, clips.Length);

        
        if (avoidImmediateRepeat && index == lastIndex)
            index = (index + Random.Range(1, clips.Length)) % clips.Length;

        lastIndex = index;
        return clips[index];
    }

    private void OnAttack(Vector2 direction) => Play();
    private void OnWindup(float duration) => Play();

    private void OnAnyDamaged(Health damaged, float amount, DamageType type)
    {
        if (damaged != health) return;
        if (ignorePoison && type == DamageType.Poison) return;

        Play();
    }

    private void WarnMissing(string componentName)
    {
        Debug.LogWarning(
            $"[SfxEmitter] '{name}' usa el trigger {playOn} pero no tiene {componentName}. " +
            "Agrega ese componente o cambia el trigger.", this);
    }
}
