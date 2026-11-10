using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Datos")]
    [SerializeField] private PlayerStats stats;


    [Header("Input")]
    [SerializeField] private InputActionAsset actions;

    private PlayerRuntimeStats runtimeStats; // opcional: velocidad con mejoras aplicadas
    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    /// <summary>
    /// Ultima direccion que el jugador pulso (zero hasta que se mueve por primera vez). Sale del input, no de la
    /// velocidad del Rigidbody: si el jugador empuja contra enemigos la velocidad real es ~0 pero la direccion sigue.
    /// </summary>
    public Vector2 LastMoveDirection { get; private set; }

    /// <summary>Input de movimiento de este frame (magnitud 0..1). Zero si no hay input.</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>True mientras el jugador mantiene una direccion de movimiento.</summary>
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f;

    private float MoveSpeed
    {
        get
        {
            // La velocidad base sale de PlayerStats; las mejoras (PlayerRuntimeStats) solo la multiplican.
            float baseSpeed = stats != null ? stats.moveSpeed : 5f;
            return runtimeStats != null ? baseSpeed * runtimeStats.MoveSpeedMultiplier : baseSpeed;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // evita vibracion al chocar y con la camara
        TryGetComponent(out runtimeStats);

        if (actions != null)
        {
            moveAction = actions.FindAction("Move", throwIfNotFound: true);
        }
    }

    private void OnEnable()
    {
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        moveInput = Vector2.zero;
    }

    private void Update()
    {
        if (moveAction == null) return;

        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        if (moveInput.sqrMagnitude > 0.01f)
            LastMoveDirection = moveInput.normalized;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * MoveSpeed;
    }
}
