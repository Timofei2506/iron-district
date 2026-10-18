using System.Collections.Generic;
using UnityEngine;

namespace IronDistrict
{
    /// <summary>Static registry filled by the generator, consumed by spawner / pickups.</summary>
    public static class WorldData
    {
        public static readonly List<Vector3> EnemySpawnPoints = new List<Vector3>();
        public static readonly List<Vector3> PickupSpots = new List<Vector3>();
        public static readonly List<Vector3> Landmarks = new List<Vector3>();

        public static Vector3 PlayerSpawn = new Vector3(30f, 1.4f, -232f);
        public static Vector2 WorldExtent = new Vector2(500f, 500f);

        public static void Clear()
        {
            EnemySpawnPoints.Clear();
            PickupSpots.Clear();
            Landmarks.Clear();
            PlayerSpawn = new Vector3(30f, 1.4f, -232f);
        }

        public static bool InsideWorld(Vector3 p, float margin = 20f)
        {
            return Mathf.Abs(p.x) < WorldExtent.x - margin && Mathf.Abs(p.z) < WorldExtent.y - margin;
        }
    }
}
