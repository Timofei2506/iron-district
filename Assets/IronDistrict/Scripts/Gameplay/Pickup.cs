using UnityEngine;

namespace IronDistrict
{
    public enum PickupKind { Ammo, Medkit }

    /// <summary>Spinning, bobbing pickup with proximity collection.</summary>
    public class Pickup : MonoBehaviour
    {
        public PickupKind Kind;
        float spawnTime;
        Vector3 basePos;
        Transform visual;
        Material glowMat;

        public static Pickup Spawn(Vector3 pos, PickupKind kind)
        {
            var go = new GameObject(kind == PickupKind.Ammo ? "AmmoPickup" : "MedkitPickup");
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.75f;
            col.isTrigger = true;

            go.transform.position = new Vector3(pos.x, Mathf.Max(pos.y, 0.35f) + 0.35f, pos.z);
            var p = go.AddComponent<Pickup>();
            p.Kind = kind;
            p.spawnTime = Time.time;
            p.BuildVisual();
            return p;
        }

        void BuildVisual()
        {
            var pivot = Geo.Obj("Visual", transform);
            visual = pivot.transform;

            var glow = Kind == PickupKind.Ammo
                ? new Color(1f, 0.65f, 0.15f)
                : new Color(0.3f, 0.95f, 0.4f);
            glowMat = AssetLibrary.Solid("pickupGlow" + Kind, glow, 0f, 0.6f, glow * 1.6f);

            var mb = new MeshBuilder();
            if (Kind == PickupKind.Ammo)
            {
                mb.AddBox(new Vector3(0, 0, 0), new Vector3(0.42f, 0.26f, 0.28f), 0f, 0.6f);
                mb.AddBox(new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.05f, 0.2f), 0f, 0.6f);
            }
            else
            {
                mb.AddBox(new Vector3(0, 0, 0), new Vector3(0.44f, 0.3f, 0.3f), 0f, 0.6f);
                mb.AddBox(new Vector3(0, 0.16f, 0), new Vector3(0.12f, 0.04f, 0.03f), 0f, 0.6f);
            }
            Geo.MeshObj("Box", visual, mb.Build("pickup"), new[] { glowMat });

            var ring = Geo.Obj("Ring", visual, new Vector3(0, -0.28f, 0));
            var rmb = new MeshBuilder();
            rmb.AddBox(Vector3.zero, new Vector3(0.7f, 0.02f, 0.7f), 0f, 0.5f);
            Geo.MeshObj("RingMesh", ring.transform, rmb.Build("ring"), new[] { glowMat });

            basePos = transform.position;
        }

        void Update()
        {
            float t = Time.time;
            transform.position = basePos + Vector3.up * (0.14f + Mathf.Sin(t * 2.2f) * 0.13f);
            visual.rotation = Quaternion.Euler(0, t * 65f, 0);

            var root = GameRoot.I;
            if (root == null || root.State != GameRoot.GameState.Playing || root.Player == null) return;

            float d = Vector3.Distance(root.Player.transform.position, transform.position);
            if (d > 60f) return;
            if (d < 2.1f)
            {
                Collect(root.Player);
            }
            else if (d < 30f && Time.time - spawnTime > 1f)
            {
                // magnet-ish drift when close
                Vector3 to = root.Player.transform.position + Vector3.up - transform.position;
                basePos += to.normalized * (Time.deltaTime * (30f - d) * 0.55f);
            }
        }

        void Collect(PlayerController player)
        {
            if (Kind == PickupKind.Ammo)
            {
                if (player.Weapon.Reserve >= player.Weapon.ReserveMax) return;
                player.Weapon.AddReserve(40);
                GameManager.I?.Hud?.ShowToast("+40 ПАТРОНОВ");
            }
            else
            {
                if (player.Health.Health >= player.Health.MaxHealth) return;
                player.Health.Heal(35f);
                GameManager.I?.Hud?.ShowToast("+35 HP");
            }
            Sfx.Play("sfx_pickup", 0.8f, 1f);
            Destroy(gameObject);
        }
    }
}
