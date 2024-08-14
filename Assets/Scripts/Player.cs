using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour {

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

    private GameManager GameManager;


    public ulong UniqueNetId { get; private set; }

    private void Awake() {
        InputActions = new PlayerInput();
        InputActions.Enable();

        ChargePoints[0] = ChargeIndicator.transform.GetChild(0);
        ChargePoints[1] = ChargeIndicator.transform.GetChild(1);
        
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
        float adv = MaxSpeed * Time.fixedDeltaTime / AccelerationTime;
        float ddv = MaxSpeed * Time.fixedDeltaTime / DecelerationTime;

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

        if (mag > MaxSpeed)
            RigidBodyComponent.velocity = RigidBodyComponent.velocity / mag * MaxSpeed;
    }
    private void Fire(float ChargeLevel) {
        Transform spawnedBulledTransform = Instantiate(BulletPrefab);
        var bullet = spawnedBulledTransform.GetComponent<Bullet>();
        if(bullet)
            bullet.Initialize(this, transform.up.xy(), ChargeLevel);
        spawnedBulledTransform.GetComponent<NetworkObject>().Spawn(true);
        RigidBodyComponent.velocity -= transform.up.xy() * FireImpulse * ChargeLevel * ChargeLevel;
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
    public void BulletImpact(Vector2 Direction, float Power) {
        Vector2 impulse = (Direction * Power * Power * MaxImpactImpulse);
        RigidBodyComponent.velocity += impulse;
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