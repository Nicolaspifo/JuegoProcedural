using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stunnable : MonoBehaviour
{
    [SerializeField] Behaviour[] disableWhileStunned;
    [SerializeField] float immunityAfterStun = 2f;

    Rigidbody2D rb;
    float immuneUntil;

    public bool IsStunned { get; private set; }
    public bool IsImmune => Time.time < immuneUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (disableWhileStunned == null || disableWhileStunned.Length == 0)
        {
            var list = new List<Behaviour>();
            var move = GetComponent<PlayerMovement>();
            var attack = GetComponent<MacheteAttack>();
            if (move != null) list.Add(move);
            if (attack != null) list.Add(attack);
            disableWhileStunned = list.ToArray();
        }
    }

    public bool TryStun(float duration)
    {
        if (IsStunned || IsImmune) return false;
        StartCoroutine(StunRoutine(duration));
        return true;
    }

    IEnumerator StunRoutine(float duration)
    {
        IsStunned = true;
        foreach (var b in disableWhileStunned) if (b != null) b.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        Debug.Log($"[Aturdido] {duration}s");

        yield return new WaitForSeconds(duration);

        foreach (var b in disableWhileStunned) if (b != null) b.enabled = true;
        IsStunned = false;
        immuneUntil = Time.time + immunityAfterStun;
    }
}