using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour {

    [Header("Player")]
    [SerializeField] SpriteRenderer SpriteRendererComponent;
    [SerializeField] Color HealthyColor = Color.white;
    [SerializeField] Color DeadColor = Color.red;

    [Header("Moving")]
    [SerializeField, Range(0f, 10f)] private float MaxSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float AccelerationTime = 0.1f;
    [SerializeField, Range(0f, 1f)] private float DecelerationTime = 0.25f;
    [Header("Shooting")]
    [SerializeField, Range(0f, 2f)] private float ChargeTime = 1f;
    [SerializeField, Range(0f, 1f)] private float ChargeThreshold = 0.5f;
    [SerializeField, Range(0f, 2f)] private float ReleaseTime = 0.5f;
    [SerializeField, Range(0f, 100f)] private float FireImpulse = 25f;
    [SerializeField] private Color EmptyChargeColor = Color.red;
    [SerializeField] private Color MediumChargeColor = Color.yellow;
    [SerializeField] private Color FullChargeColor = Color.green;
    [Header("Getting Hit")]
    [SerializeField, Range(0, 1000)] private int MaxHealth = 100; 
    [SerializeField, Range(0, 1000)] private int MaxDamageTaken = 25; 
    [SerializeField, Range(0f, 100f)] private float MaxImpactImpulse = 50f;

    public Transform MuzzleTransform;

    [SerializeField] private LineRenderer AimingIndicator;
    [SerializeField] private LineRenderer ChargeIndicator;
    [SerializeField] private Transform BulletPrefab;
    
    private Transform[] ChargePoints = new Transform[2];

    private PlayerInput InputActions;
    private Rigidbody2D RigidBodyComponent = null;

    private Vector3 LookInput = Vector2.zero;
    private Vector2 MovementInput = Vector2.zero;

    private bool IsCharging = false;
    private float ChargeLevel = 0f;

    private float Speed;

    private GameManager GameManager;

    private NetworkVariable<int> Health = new NetworkVariable<int>();

    public ulong UniqueNetId { get; private set; }

    private void Awake() {
        InputActions = new PlayerInput();
        InputActions.Enable();

        ChargePoints[0] = ChargeIndicator.transform.GetChild(0);
        ChargePoints[1] = ChargeIndicator.transform.GetChild(1);
        Speed = MaxSpeed;
    }

    void Update() {
        MovementInput = ReadMovementInput();
        LookInput = ReadLookInput();
        IsCharging = ReadChargeInput();
        DisplayAimingIndicatorClientRpc();

        if (!IsCharging && ChargeLevel > ChargeThreshold) {
            if (IsServer && IsLocalPlayer)
                Fire(ChargeLevel);
            else if (IsClient && IsLocalPlayer)
                RequestFireServerRpc(ChargeLevel);
            ChargeLevel = 0;
        }

        if (ChargeLevel < 1f && IsCharging) {
            ChargeLevel += Time.deltaTime / ChargeTime;
        }
        else if (ChargeLevel > 0 == !IsCharging) {
            ChargeLevel -= Time.deltaTime / ReleaseTime;
        }
        ChargeLevel = Mathf.Clamp01(ChargeLevel);

        if (IsOwner) {
            ChargeIndicator.SetPosition(0, ChargePoints[0].position);
            ChargeIndicator.SetPosition(1, Vector3.Lerp(ChargePoints[0].position, ChargePoints[1].position, ChargeLevel));
            if(ChargeLevel <= 0.5f)
                ChargeIndicator.material.SetColor("_Color", Color.Lerp(EmptyChargeColor, MediumChargeColor, ChargeLevel * 2));
            else
                ChargeIndicator.material.SetColor("_Color", Color.Lerp(MediumChargeColor, FullChargeColor, (ChargeLevel - 0.5f) * 2));

        }
    }
    private void FixedUpdate() {
        if (IsServer && IsLocalPlayer) {
            Move(MovementInput);
            Look(LookInput);

        } else if (IsClient && IsLocalPlayer) {
            RequestMoveServerRpc(MovementInput);
            RequestLookServerRpc(LookInput);
        }
    }

    public override void OnNetworkSpawn() {
        RigidBodyComponent = GetComponent<Rigidbody2D>();
        if (!IsOwner) {
            Destroy(AimingIndicator);
            Destroy(ChargeIndicator);
        }
        GameManager = NetworkManager.gameObject.GetComponent<GameManager>();
        UniqueNetId = NetworkManager.LocalClientId;
        Health.Value = MaxHealth;
        Health.OnValueChanged += UpdateColorRpc;
    }
    public override void OnNetworkDespawn() {
        Health.OnValueChanged -= UpdateColorRpc;
    }
    private void OnGUI() {
        GUILayout.BeginArea(new Rect(20, 450, 200, 200));
        GUILayout.Label("Player ID: " + UniqueNetId);
        GUILayout.EndArea();
    }

    private void Look(Vector3 LookVector) {

        Vector3 targetDirection = (LookVector - transform.position).normalized;
        transform.up = targetDirection;
    }
    private void Move(Vector2 MovementInput) {
        float adv = Speed * Time.fixedDeltaTime / AccelerationTime;
        float ddv = Speed * Time.fixedDeltaTime / DecelerationTime;

        RigidBodyComponent.velocity += MovementInput * adv;
        float mag = RigidBodyComponent.velocity.magnitude;

        Vector2 deceleration = Vector2.zero;
        if(MovementInput.x == 0) {
            float v = RigidBodyComponent.velocity.x;
            float sub = Mathf.Abs(v) > ddv ? Mathf.Sign(v) * ddv : v;
            deceleration.x = sub;
        }
        if (MovementInput.y == 0) {
            float v = RigidBodyComponent.velocity.y;
            float sub = Mathf.Abs(v) > ddv ? Mathf.Sign(v) * ddv : v;
            deceleration.y = sub;
        }
        
        RigidBodyComponent.velocity -= deceleration;

        if (mag > Speed)
            RigidBodyComponent.velocity = RigidBodyComponent.velocity / mag * Speed;
    }
    private void Fire(float ChargeLevel) {
        Transform spawnedBulledTransform = Instantiate(BulletPrefab);
        var bullet = spawnedBulledTransform.GetComponent<Bullet>();
        float power = (ChargeThreshold - ChargeLevel) / (ChargeThreshold - 1f);
        if (bullet)
            bullet.Initialize(this, transform.up.xy(), power);
        spawnedBulledTransform.GetComponent<NetworkObject>().Spawn(true);
        RigidBodyComponent.velocity -= transform.up.xy() * FireImpulse * power;
    }

    [ServerRpc] private void RequestMoveServerRpc(Vector2 Input) {
        Move(Input);
    }
    [ServerRpc] private void RequestLookServerRpc(Vector2 Input) {
        Look(Input);
    }
    [ServerRpc] private void RequestFireServerRpc(float ChargeLevel) {
        Fire(ChargeLevel);
    }
    public void Damage(Vector2 Direction, float Power) {
        Vector2 impulse = (Direction * Power * Power * MaxImpactImpulse);
        RigidBodyComponent.velocity += impulse;
        Health.Value -= (int)(Power * Power * MaxDamageTaken);
        Speed = MaxSpeed * Mathf.Sqrt((float)Health.Value / MaxHealth);
        if (Health.Value < 0) {
            Die();
        }
    }
    public void Die() {
        Debug.Log("Client sent death request.");
        if(IsOwner)
            HandleEndStateServerRpc(NetworkManager.LocalClientId);
    }
    [ServerRpc(RequireOwnership = false)] private void HandleEndStateServerRpc(ulong LoserId) {
        Debug.Log("Server received death request from client.");
        HandleEndStateClientRpc(LoserId);
    }
    [ClientRpc] private void HandleEndStateClientRpc(ulong LoserId) {
        Debug.Log("Client received death request from server.");
        var winScreen = NetworkManager.gameObject.GetComponent<GameManager>().WinScreen;
        if (!winScreen) {
            Debug.LogError("Could not find win screen.");
            return;
        }
        if (NetworkManager.LocalClientId != LoserId) {
            winScreen.Win();
        } else {
            winScreen.Lose();
            Destroy(gameObject);
        }
    }

    [Rpc(SendTo.Everyone)] private void UpdateColorRpc(int previous, int current) {
        float t = (float)Health.Value / MaxHealth;
        SpriteRendererComponent.color = Color.Lerp(DeadColor, HealthyColor, t * t);
    }

    private Vector3 ReadLookInput() {
        Vector2 raw = InputActions.Default.Look.ReadValue<Vector2>();
        Vector3 global = Camera.main.ScreenToWorldPoint(raw);
        global.z = 0;
        return global;
    }
    private Vector2 ReadMovementInput() { return InputActions.Default.Movement.ReadValue<Vector2>(); }
    private bool ReadChargeInput() { return InputActions.Default.Charge.ReadValue<float>() > 0; }

    [ClientRpc]
    private void DisplayAimingIndicatorClientRpc() {
        if (!IsOwner)
            return;
        Vector3[] positions = new Vector3[2];
        positions[0] = AimingIndicator.transform.position;
        positions[1] = LookInput;
        AimingIndicator.SetPositions(positions);
    }
}