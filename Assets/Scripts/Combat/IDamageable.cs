using UnityEngine;

namespace MuckClone.Combat
{
    /// <summary>
    /// Interface padrão para qualquer entidade que possa receber dano e impulso físico (Knockback).
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage, Vector3 hitPoint, Vector3 knockbackForce);
    }
}
