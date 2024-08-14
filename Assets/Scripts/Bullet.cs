using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.Processors;

public class Bullet : NetworkBehaviour {
    [SerializeField, Range(0f, 50f)] private float MaxSpeed = 10f;
    [SerializeField, Range(0f, 15f)] private float MaxLifeTime = 5f;
    [SerializeField] private GameObject Sprite;

    private Collider2D Collider = null;
    private Vector2 Velocity;
    private float Power = 0;

    private Player Instigator = null;
    private float LifetimeCounter = 0f;

    private void FixedUpdate() {
        transform.position += (Velocity * Time.fixedDeltaTime).xy0();
        LifetimeCounter += Time.fixedDeltaTime;
        if (LifetimeCounter > MaxLifeTime)
            DestroyServerRpc();
    }

    public override void OnNetworkSpawn() {
        Collider = GetComponent<Collider2D>();
    }

    public void Initialize(Player Instigator, Vector2 Direction, float Power) {
        this.Instigator = Instigator;
        this.Power = Power;

        float speed = Power * Power * MaxSpeed;
        speed = Mathf.Clamp(speed, 0f, MaxSpeed);
        Velocity = Direction * speed;
        transform.up = Direction;
        transform.position = Instigator.MuzzleTransform.position;
        Debug.Log(Instigator.MuzzleTransform.position);
    }

    private void OnTriggerEnter2D(Collider2D collider) {
        if (Instigator && collider.gameObject == Instigator.gameObject)
            return;
        Debug.Log("Hit!");
        var player = collider.gameObject.GetComponent<Player>();
        if (player) {
            player.BulletImpact(transform.up.xy0(), Power);
        }
        DestroyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void DestroyServerRpc() {
        Debug.Log("Destroyed Bullet");
        var ps = GetComponent<ParticleSystem>();
        //if (ps) {
        //    ps.Stop();
        //    Collider.enabled = false;
        //    Sprite.SetActive(false);
        //}
        Destroy(gameObject);
    }
}
