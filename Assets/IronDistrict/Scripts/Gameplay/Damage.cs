using UnityEngine;

namespace IronDistrict
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Direction;
        public bool Headshot;
        public GameObject Source;

        public DamageInfo(float amount, Vector3 point, Vector3 normal, Vector3 dir, bool head, GameObject src)
        {
            Amount = amount; Point = point; Normal = normal; Direction = dir; Headshot = head; Source = src;
        }
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
        bool IsAlive { get; }
    }

    /// <summary>Marks a hitbox child (e.g. head) with a damage multiplier.</summary>
    public class HitZone : MonoBehaviour
    {
        public float Multiplier = 2.0f;
    }
}
