using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled XP pickup. Spins in place, gets vacuumed toward the player within range,
    /// grants XP on contact, and returns to its pool (also after a lifetime, uncollected).
    /// </summary>
    public class XpGem : MonoBehaviour
    {
        [SerializeField] private float attractRadius = 5f;
        [SerializeField] private float pickupRadius = 0.9f;
        [SerializeField] private float moveSpeed = 11f;
        [SerializeField] private float spinSpeed = 140f;
        [SerializeField] private float lifetime = 18f;
        [Tooltip("Height above the player's feet the gem homes toward (so it flies to the chest, not the floor).")]
        [SerializeField] private float targetHeightOffset = 0.8f;

        private int _value;
        private Transform _player;
        private Action<XpGem> _return;
        private bool _active;
        private float _life;

        public void Init(int value, Transform player, Action<XpGem> ret)
        {
            _value = value;
            _player = player;
            _return = ret;
            _active = true;
            _life = lifetime;
        }

        private void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;

            transform.Rotate(Vector3.up, spinSpeed * dt, Space.World);

            _life -= dt;
            if (_life <= 0f) { Despawn(); return; }

            if (_player == null) return;
            Vector3 to = (_player.position + Vector3.up * targetHeightOffset) - transform.position;
            float d = to.magnitude;
            if (d <= pickupRadius)
            {
                if (GameManager.Instance != null) GameManager.Instance.AddXp(_value);
                Despawn();
                return;
            }
            if (d < attractRadius)
                transform.position += (to / Mathf.Max(d, 0.0001f)) * moveSpeed * dt;
        }

        private void Despawn()
        {
            _active = false;
            _return?.Invoke(this);
        }
    }
}
