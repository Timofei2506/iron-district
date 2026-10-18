using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = System.Random;

namespace IronDistrict
{
    /// <summary>
    /// Builds the whole 1 km x 1 km world procedurally:
    /// downtown, industrial zone, village, forest, roads, props, navmesh.
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        public static WorldGenerator Instance;
        public const float HalfWorld = 500f;

        Transform root;
        NavMeshData navData;
        NavMeshDataInstance navInstance;
        Random rnd;

        readonly List<Rect> occupied = new List<Rect>(); // x/z rects used to avoid trees/bin placement

        static readonly Rect CityRect = Rect.MinMaxRect(-135f, 52f, 135f, 292f);
        static readonly Rect IndRect = Rect.MinMaxRect(140f, -85f, 350f, 220f);
        static readonly Rect VillageRect = Rect.MinMaxRect(-340f, -120f, -145f, 135f);

        Material[] facadeMats;
        Material matRoof, matGlass, matGlassLit, matFrame, matMetal, matDark, matRust, matWood, matBrick;
        Material matConcrete, matConcreteDark, matAsphalt, matGrass, matDirt, matHazard, matSand;

        int buildingCounter, propCounter;

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            RemoveNav();
            if (Instance == this) Instance = null;
        }

        // ============================================================== API
        /// <summary>Removes navmesh data + world root (used when regenerating a new seed).</summary>
        public void UnloadWorld()
        {
            RemoveNav();
            var old = GameObject.Find("World");
            if (old != null) DestroyImmediate(old);
        }

        void RemoveNav()
        {
            if (navInstance.valid)
            {
                try { NavMesh.RemoveNavMeshData(navInstance); } catch { }
            }
            navInstance = default;
            navData = null;
        }

        public IEnumerator Generate(int seed, Action<string> status)
        {
            rnd = new Random(seed);
            WorldData.Clear();
            occupied.Clear();
            buildingCounter = propCounter = 0;

            var go = new GameObject("World");
            root = go.transform;

            status?.Invoke("МАТЕРИАЛЫ");
            PrepareMaterials();
            yield return null;

            status?.Invoke("ЛАНДШАФТ");
            BuildGround();
            yield return null;

            status?.Invoke("ДОРОГИ");
            BuildRoads();
            yield return null;

            status?.Invoke("ГОРОД");
            BuildCity();
            yield return null;

            status?.Invoke("ПРОМЗОНА");
            BuildIndustrial();
            yield return null;

            status?.Invoke("ДЕРЕВНЯ");
            BuildVillage();
            yield return null;

            status?.Invoke("ЛЕС И ГОРЫ");
            BuildNature();
            yield return null;

            status?.Invoke("ПРОПС И БАЗА ИГРОКА");
            BuildCamp();
            BuildPickups();
            BuildBoundary();
            yield return null;

            status?.Invoke("НАВИГАЦИЯ");
            BakeNavMesh();
            yield return null;

            status?.Invoke("ГОТОВО");
        }

        // ========================================================== materials
        void PrepareMaterials()
        {
            facadeMats = new[]
            {
                AssetLibrary.Tiled("M_Facade", 1f),
                AssetLibrary.Tiled("M_Facade", 1f, new Color(1.12f, 1.10f, 1.06f)),
                AssetLibrary.Tiled("M_Facade", 1f, new Color(1.02f, 0.94f, 0.84f)),
                AssetLibrary.Tiled("M_Facade", 1f, new Color(0.88f, 0.93f, 1.02f)),
                AssetLibrary.Tiled("M_ConcreteDark", 1f),
                AssetLibrary.Tiled("M_Brick", 1f),
            };
            matRoof = AssetLibrary.Tiled("M_ConcreteDark", 1f, new Color(0.8f, 0.8f, 0.82f));
            matGlass = AssetLibrary.Glass();
            matGlassLit = AssetLibrary.GlassLit();
            matFrame = AssetLibrary.WindowFrame();
            matMetal = AssetLibrary.Tiled("M_Metal", 1f);
            matDark = AssetLibrary.DarkMetal();
            matRust = AssetLibrary.Rust(1f);
            matWood = AssetLibrary.Wood(1f);
            matBrick = AssetLibrary.Brick(1f);
            matConcrete = AssetLibrary.Concrete(1f);
            matConcreteDark = AssetLibrary.ConcreteDark(1f);
            matAsphalt = AssetLibrary.Asphalt(1f);
            matGrass = AssetLibrary.Grass(1f);
            matDirt = AssetLibrary.Dirt(1f);
            matHazard = AssetLibrary.Hazard(1f);
            matSand = AssetLibrary.Sand(1f);
        }

        // ============================================================= ground
        void BuildGround()
        {
            var ground = Geo.GroundRect("Ground", root, 0, 0, 1300, 1300, 0f, AssetLibrary.Grass(1f), 0.12f);
            var col = ground.AddComponent<BoxCollider>();
            col.size = new Vector3(1300, 2, 1300);
            col.center = new Vector3(0, -1, 0);

            // industrial asphalt pad
            Geo.GroundRect("IndPad", root, 245, 68, 205, 300, 0.02f, matAsphalt, 0.10f);
            // city plaza pads under blocks
            // village dirt
            Geo.GroundRect("VillageGround", root, -242, 6, 200, 250, 0.02f, matDirt, 0.10f);
        }

        void BuildRoads()
        {
            RoadZ(30f, -470f, 320f, 16f);      // main boulevard
            RoadX(170f, -470f, 400f, 14f);     // cross street
            RoadZ(-110f, 55f, 290f, 10f);
            RoadZ(-40f, 55f, 290f, 10f);
            RoadZ(100f, 55f, 290f, 10f);
            RoadX(105f, -135f, 130f, 10f);
            RoadX(235f, -135f, 130f, 10f);
            RoadX(68f, -470f, 400f, 10f);      // south arterial
            RoadX(-40f, 140f, 350f, 10f);      // industrial access
            RoadX(10f, -345f, -140f, 8f);      // village path
            RoadX(-95f, -345f, -140f, 6f);
        }

        void RoadZ(float x, float z0, float z1, float w)
        {
            Geo.GroundRect("Road", root, x, (z0 + z1) * 0.5f, w, Mathf.Abs(z1 - z0), 0.03f, matAsphalt, 0.10f);
        }

        void RoadX(float z, float x0, float x1, float w)
        {
            Geo.GroundRect("Road", root, (x0 + x1) * 0.5f, z, Mathf.Abs(x1 - x0), w, 0.03f, matAsphalt, 0.10f);
        }

        // =============================================================== city
        void BuildCity()
        {
            float[] blockX = { -75f, -5f, 65f };
            float[] blockZ = { 82.5f, 137.5f, 202.5f, 262.5f };

            // concrete pads
            foreach (var bx in blockX)
                foreach (var bz in blockZ)
                    Geo.GroundRect("Pad", root, bx, bz, 58f, 64f, 0.021f, matConcreteDark, 0.06f);

            int idx = 0;
            foreach (var bx in blockX)
            {
                foreach (var bz in blockZ)
                {
                    float dist = Mathf.Abs(bx - 30f) + Mathf.Abs(bz - 170f);
                    bool landmark = dist < 55f;
                    int count = randChance(0.55f) ? 1 : 2;

                    if (count == 1)
                    {
                        float w = lerp(24f, 40f);
                        float d = lerp(22f, 40f);
                        float h = landmark ? lerp(52f, 96f) : lerp(16f, 44f);
                        SpawnBuilding(bx + rand(-3f, 3f), bz + rand(-3f, 3f), w, d, h, landmark);
                    }
                    else
                    {
                        bool splitX = randChance(0.5f);
                        for (int k = 0; k < 2; k++)
                        {
                            float off = splitX ? (k == 0 ? -13f : 13f) : (k == 0 ? -13.5f : 13.5f);
                            float w = splitX ? lerp(15f, 21f) : lerp(24f, 32f);
                            float d = splitX ? lerp(24f, 34f) : lerp(15f, 20f);
                            float h = landmark ? lerp(30f, 66f) : lerp(12f, 38f);
                            float x = bx + (splitX ? off : 0f) + rand(-2f, 2f);
                            float z = bz + (splitX ? 0f : off) + rand(-2f, 2f);
                            SpawnBuilding(x, z, w, d, h, landmark && k == 0);
                        }
                    }
                    idx++;

                    // street spawn points around the block
                    if (randChance(0.85f))
                        WorldData.EnemySpawnPoints.Add(new Vector3(bx + rand(-24f, 24f), 0.2f, bz + rand(20f, 36f)));
                    if (randChance(0.5f))
                        WorldData.EnemySpawnPoints.Add(new Vector3(bx + rand(-28f, 28f), 0.2f, bz - rand(16f, 30f)));
                }
            }

            // avenue spawn points
            for (float z = 95f; z <= 285f; z += 47f)
                WorldData.EnemySpawnPoints.Add(new Vector3(30f + rand(-14f, 14f), 0.2f, z));

            // low-rise fringe along the boulevard south of downtown
            SpawnBuilding(-4f, 34f, 26f, 18f, 9f, false);
            SpawnBuilding(64f, 34f, 22f, 16f, 12f, false);

        }

        void SpawnBuilding(float cx, float cz, float w, float d, float h, bool landmark)
        {
            w = Mathf.Max(8f, w);
            d = Mathf.Max(8f, d);
            h = Mathf.Max(6f, h);

            var b = Geo.Obj("Building_" + buildingCounter++, root, new Vector3(cx, 0, cz));
            Material facade = facadeMats[rnd.Next(facadeMats.Length)];

            // ------- body (walls + base + parapet)
            var body = new MeshBuilder();
            body.AddBox(new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), 0f, 0.35f);
            body.AddBox(new Vector3(0, 0.55f, 0), new Vector3(w + 0.3f, 1.1f, d + 0.3f), 0f, 0.35f);
            // parapet
            float pt = 0.35f;
            body.AddBox(new Vector3(0, h + 0.45f, d * 0.5f - pt * 0.5f), new Vector3(w, 0.9f, pt), 0f, 0.35f);
            body.AddBox(new Vector3(0, h + 0.45f, -d * 0.5f + pt * 0.5f), new Vector3(w, 0.9f, pt), 0f, 0.35f);
            body.AddBox(new Vector3(w * 0.5f - pt * 0.5f, h + 0.45f, 0), new Vector3(pt, 0.9f, d), 0f, 0.35f);
            body.AddBox(new Vector3(-w * 0.5f + pt * 0.5f, h + 0.45f, 0), new Vector3(pt, 0.9f, d), 0f, 0.35f);

            Geo.MeshObj("Body", b.transform, body.Build("bldg"), new[] { facade });

            // ------- windows
            WindowGrid(b.transform, w, d, h);
            // ------- roof clutter + door
            RoofDetail(b.transform, w, d, h, landmark);

            // ------- collider
            var col = b.AddComponent<BoxCollider>();
            col.size = new Vector3(w, h, d);
            col.center = new Vector3(0, h * 0.5f, 0);

            occupied.Add(new Rect(cx - w * 0.5f - 1.5f, cz - d * 0.5f - 1.5f, w + 3f, d + 3f));

            if (h > 45f) WorldData.Landmarks.Add(new Vector3(cx, h * 0.5f, cz));
            WorldData.PickupSpots.Add(new Vector3(cx + rand(-w, w) * 0.5f, 0.5f, cz - d * 0.5f - 3f));
        }

        void WindowGrid(Transform parent, float w, float d, float h)
        {
            var glass = new MeshBuilder();
            var lit = new MeshBuilder();
            float y0 = 4.4f, stepY = 3.3f, wy = 2.0f, wx = 1.55f;
            int rows = Mathf.FloorToInt((h - y0 - 1.5f) / stepY);
            if (rows < 1) return;

            for (int side = 0; side < 4; side++)
            {
                bool zSide = side < 2;
                float faceW = (zSide ? w : d) - 2.2f;
                if (faceW < 2f) continue;
                int cols = Mathf.Max(1, Mathf.FloorToInt(faceW / 3.1f));

                Vector3 right = side switch
                {
                    0 => Vector3.right,             // +Z
                    1 => Vector3.left,              // -Z
                    2 => Vector3.back,              // +X
                    _ => Vector3.forward            // -X
                };
                Vector3 normal = side switch
                {
                    0 => Vector3.forward,
                    1 => Vector3.back,
                    2 => Vector3.right,
                    _ => Vector3.left
                };
                float half = (zSide ? d : w) * 0.5f;

                for (int r = 0; r < rows; r++)
                {
                    float y = y0 + r * stepY;
                    for (int c = 0; c < cols; c++)
                    {
                        if (randChance(0.13f)) continue;
                        float t = -faceW * 0.5f + (c + 0.5f) * (faceW / cols);
                        Vector3 center = right * t + Vector3.up * y + normal * (half + 0.06f);
                        if (randChance(0.09f))
                            lit.AddQuad(center, right, Vector3.up, wx, wy, 0f);
                        else
                            glass.AddQuad(center, right, Vector3.up, wx, wy, 0f);
                    }
                }
            }

            // ground-floor shop windows on the -Z face
            Vector3 shopRight = Vector3.left;
            float shopBase = -d * 0.5f - 0.06f;
            for (int i = 0; i < 2; i++)
            {
                Vector3 c = shopRight * (i == 0 ? -w * 0.24f : w * 0.24f) + Vector3.up * 1.9f + Vector3.forward * shopBase;
                glass.AddQuad(c, shopRight, Vector3.up, Mathf.Min(3.4f, w * 0.28f), 2.6f, 0f);
            }

            if (!glass.IsEmpty)
            {
                var go = Geo.MeshObj("Windows", parent, glass.Build("windows"), new[] { matGlass });
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!lit.IsEmpty)
            {
                var go = Geo.MeshObj("WindowsLit", parent, lit.Build("windowsLit"), new[] { matGlassLit });
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void RoofDetail(Transform parent, float w, float d, float h, bool landmark)
        {
            var mb = new MeshBuilder();
            mb.Submesh = 0; // metal
            int ac = 1 + rnd.Next(2);
            for (int i = 0; i < ac; i++)
            {
                Vector3 p = new Vector3(rand(-w * 0.28f, w * 0.28f), h + 1.2f, rand(-d * 0.28f, d * 0.28f));
                mb.AddBox(p, new Vector3(rand(1.4f, 2.6f), 1.6f, rand(1.4f, 2.6f)), rand(0f, 90f), 0.5f);
            }
            if (landmark || h > 55f)
            {
                mb.AddTaperedCylinder(new Vector3(w * 0.3f, h, 0), 0.16f, 0.05f, rand(8f, 15f), 6, true);
            }
            // door on -Z
            mb.Submesh = 1; // frame material
            mb.AddQuad(new Vector3(0, 1.35f, -d * 0.5f - 0.07f), Vector3.left, Vector3.up, 2.3f, 2.7f, 0f);

            Geo.MeshObj("Roof", parent, mb.Build("roof"), new[] { matMetal, matFrame });
        }

        // ========================================================= industrial
        void BuildIndustrial()
        {
            // perimeter fence with a north + west gate
            FenceLine(new Vector3(148, 0, -80), new Vector3(148, 0, 215), true, 14f);
            FenceLine(new Vector3(345, 0, -80), new Vector3(345, 0, 215), true);
            FenceLine(new Vector3(148, 0, -80), new Vector3(345, 0, -80), false);
            FenceLine(new Vector3(148, 0, 215), new Vector3(345, 0, 215), false, 16f);

            // warehouses
            SpawnWarehouse(200, 15, 52, 26, 11, 0f);
            SpawnWarehouse(200, 105, 52, 26, 12, 0f);
            SpawnWarehouse(200, 190, 46, 24, 10, 0f);
            SpawnWarehouse(292, 60, 34, 22, 9, 90f);
            SpawnWarehouse(292, 150, 34, 22, 9, 90f);
            SpawnWarehouse(258, -34, 40, 20, 8, 0f);

            // silos
            for (int i = 0; i < 4; i++)
            {
                float x = 306f + i * 8.5f;
                var silo = Geo.Cylinder("Silo", root, new Vector3(x, 9f, -58f), 3.6f, 18f, matMetal, 16, 0.5f);
                Geo.Cylinder("SiloTop", silo.transform, new Vector3(0, 9.6f, 0), 3.3f, 1.4f, matRust, 16, 0.5f);
                var c = silo.AddComponent<CapsuleCollider>();
                c.radius = 3.6f; c.height = 18f; c.direction = 1;
                occupied.Add(new Rect(x - 4, -66, 8, 8));
                WorldData.EnemySpawnPoints.Add(new Vector3(x, 0.2f, -50f));
            }
            WorldData.Landmarks.Add(new Vector3(320, 8, -58));

            // chimney + smoke
            var chimney = Geo.Cylinder("Chimney", root, new Vector3(330, 17f, 200f), 1.8f, 34f, matBrick, 14, 0.4f);
            var cc = chimney.AddComponent<CapsuleCollider>();
            cc.radius = 1.9f; cc.height = 34f; cc.direction = 1;
            occupied.Add(new Rect(327, 197, 7, 7));
            MakeSmoke(new Vector3(330, 34.5f, 200f));

            // pipe runs
            var pipes = new MeshBuilder();
            for (int i = 0; i < 3; i++)
            {
                var pgo = Geo.Cylinder("Pipe", root, new Vector3(280, 1.6f + i * 0.9f, 118f), 0.35f, 26f, matRust, 10, 0.5f);
                pgo.transform.rotation = Quaternion.Euler(0, 0, 90);
                occupied.Add(new Rect(267, 116, 26, 4));
            }

            // containers
            Color[] contColors =
            {
                new Color(1.25f, 0.55f, 0.45f), new Color(0.5f, 0.75f, 1.2f),
                new Color(1.1f, 0.95f, 0.5f), new Color(0.85f, 0.9f, 0.95f)
            };
            for (int i = 0; i < 16; i++)
            {
                float x = rand(160f, 240f);
                float z = rand(-55f, 55f) + (i > 9 ? 105f : 0f);
                SpawnContainer(new Vector3(x, 0, z), i % 3 == 0 ? 90f : 0f, contColors[rnd.Next(contColors.Length)]);
            }

            WorldData.EnemySpawnPoints.Add(new Vector3(175, 0.2f, 55));
            WorldData.EnemySpawnPoints.Add(new Vector3(260, 0.2f, 30));
            WorldData.EnemySpawnPoints.Add(new Vector3(300, 0.2f, 100));
            WorldData.EnemySpawnPoints.Add(new Vector3(210, 0.2f, 145));
            WorldData.EnemySpawnPoints.Add(new Vector3(190, 0.2f, 205));
            WorldData.EnemySpawnPoints.Add(new Vector3(320, 0.2f, 0));
            WorldData.PickupSpots.Add(new Vector3(176, 0.5f, 30));
            WorldData.PickupSpots.Add(new Vector3(200, 0.5f, 120));
            WorldData.PickupSpots.Add(new Vector3(285, 0.5f, 80));
        }

        void SpawnWarehouse(float cx, float cz, float w, float d, float h, float yawDeg)
        {
            var wr = Geo.Obj("Warehouse_" + propCounter++, root, new Vector3(cx, 0, cz), Quaternion.Euler(0, yawDeg, 0));
            var body = new MeshBuilder();
            body.AddBox(new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), 0f, 0.3f);
            body.AddBox(new Vector3(0, h + 0.25f, 0), new Vector3(w + 1.6f, 0.5f, d + 1.6f), 0f, 0.3f);
            Geo.MeshObj("Body", wr.transform, body.Build("warehouse"), new[] { AssetLibrary.Tiled("M_Metal", 1f, new Color(0.82f, 0.86f, 0.9f)) });

            // big hazard door on -Z
            var doors = new MeshBuilder();
            doors.AddQuad(new Vector3(0, h * 0.5f, -d * 0.5f - 0.07f), Vector3.left, Vector3.up, Mathf.Min(10f, w * 0.5f), h * 0.82f, 0.25f);
            Geo.MeshObj("Door", wr.transform, doors.Build("door"), new[] { matHazard });

            var col = wr.AddComponent<BoxCollider>();
            col.size = new Vector3(w, h + 0.6f, d);
            col.center = new Vector3(0, (h + 0.6f) * 0.5f, 0);

            // world-space occupied rect (approx for 0/90 yaw)
            float ew = Mathf.Approximately(yawDeg, 90f) ? d : w;
            float ed = Mathf.Approximately(yawDeg, 90f) ? w : d;
            occupied.Add(new Rect(cx - ew * 0.5f - 2, cz - ed * 0.5f - 2, ew + 4, ed + 4));

            // crates in front
            Vector3 fwd = Quaternion.Euler(0, yawDeg, 0) * Vector3.back;
            for (int i = 0; i < 3; i++)
                SpawnCrate(new Vector3(cx, 0.6f, cz) + fwd * (d * 0.5f + rand(4f, 8f)) + Vector3.right * rand(-6f, 6f));

            WorldData.EnemySpawnPoints.Add(new Vector3(cx, 0.2f, cz) + fwd * (d * 0.5f + 6f));
        }

        void SpawnContainer(Vector3 pos, float yaw, Color tint)
        {
            var go = Geo.Obj("Container", root, pos, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0, 1.35f, 0), new Vector3(2.5f, 2.7f, 6.2f), 0f, 0.5f);
            // ribs
            for (int i = -2; i <= 2; i++)
                mb.AddBox(new Vector3(0, 1.35f, i * 1.2f), new Vector3(2.62f, 2.55f, 0.16f), 0f, 0.5f);
            Geo.MeshObj("Body", go.transform, mb.Build("container"), new[] { AssetLibrary.Tiled("M_Rust", 1f, tint) });
            Geo.AddBoxCollider(go, new Vector3(2.6f, 2.8f, 6.3f), new Vector3(0, 1.4f, 0));
            occupied.Add(new Rect(pos.x - 3.5f, pos.z - 1.8f, 7f, 3.6f));
        }

        // ============================================================ village
        void BuildVillage()
        {
            Vector3[] houseSpots =
            {
                new Vector3(-200, 0, 62), new Vector3(-238, 0, 66), new Vector3(-276, 0, 60),
                new Vector3(-196, 0, 20), new Vector3(-238, 0, 14), new Vector3(-280, 0, 18),
                new Vector3(-200, 0, -26), new Vector3(-244, 0, -30), new Vector3(-286, 0, -24),
                new Vector3(-204, 0, -72), new Vector3(-250, 0, -70), new Vector3(-292, 0, -64),
            };
            foreach (var p in houseSpots)
            {
                SpawnHouse(p + new Vector3(rand(-3f, 3f), 0, rand(-3f, 3f)), rand(0f, 360f));
            }

            SpawnWell(new Vector3(-238, 0, 36));

            // fences around a couple of yards + village edge
            FenceLine(new Vector3(-320, 0, -110), new Vector3(-320, 0, 120), true, 10f);
            FenceLine(new Vector3(-320, 0, -110), new Vector3(-158, 0, -110), false, 12f);

            for (int i = 0; i < 18; i++)
                SpawnCrate(new Vector3(rand(-310f, -170f), 0.6f, rand(-95f, 105f)));

            for (int i = 0; i < 8; i++)
            {
                var bale = Geo.Cylinder("Hay", root, new Vector3(rand(-310f, -180f), 0.55f, rand(-60f, 90f)), 0.65f, 1.2f, AssetLibrary.Solid("hay", new Color(0.62f, 0.52f, 0.25f), 0f, 0.1f), 10, 0.5f);
                bale.transform.rotation = Quaternion.Euler(0, rand(0f, 360f), 90f);
            }

            WorldData.EnemySpawnPoints.Add(new Vector3(-215, 0.2f, 40));
            WorldData.EnemySpawnPoints.Add(new Vector3(-260, 0.2f, -12));
            WorldData.EnemySpawnPoints.Add(new Vector3(-300, 0.2f, 40));
            WorldData.EnemySpawnPoints.Add(new Vector3(-230, 0.2f, -60));
            WorldData.EnemySpawnPoints.Add(new Vector3(-180, 0.2f, 80));
            WorldData.EnemySpawnPoints.Add(new Vector3(-290, 0.2f, -80));
            WorldData.PickupSpots.Add(new Vector3(-238, 0.5f, 28));
            WorldData.PickupSpots.Add(new Vector3(-204, 0.5f, -8));
            WorldData.PickupSpots.Add(new Vector3(-276, 0.5f, 44));
        }

        void SpawnHouse(Vector3 pos, float yaw)
        {
            var h = Geo.Obj("House_" + propCounter++, root, pos, Quaternion.Euler(0, yaw, 0));
            bool wooden = randChance(0.6f);
            Material wallMat = wooden ? AssetLibrary.Tiled("M_Wood", 1f, new Color(0.9f, 0.85f, 0.8f)) : matBrick;

            var body = new MeshBuilder();
            body.AddBox(new Vector3(0, 1.7f, 0), new Vector3(7f, 3.4f, 5.4f), 0f, 0.45f);
            Geo.MeshObj("Body", h.transform, body.Build("house"), new[] { wallMat });

            var roof = new MeshBuilder();
            roof.AddBox(new Vector3(0, 4.35f, 1.62f), new Vector3(7.9f, 0.24f, 3.55f), Quaternion.Euler(31f, 0, 0), 0.4f);
            roof.AddBox(new Vector3(0, 4.35f, -1.62f), new Vector3(7.9f, 0.24f, 3.55f), Quaternion.Euler(-31f, 0, 0), 0.4f);
            Geo.MeshObj("Roof", h.transform, roof.Build("roof"), new[] { AssetLibrary.Tiled("M_Rust", 1f, new Color(0.75f, 0.72f, 0.7f)) });

            var win = new MeshBuilder();
            win.AddQuad(new Vector3(-1.8f, 1.75f, -2.72f), Vector3.left, Vector3.up, 1.15f, 1.15f, 0f);
            win.AddQuad(new Vector3(1.8f, 1.75f, -2.72f), Vector3.left, Vector3.up, 1.15f, 1.15f, 0f);
            win.AddQuad(new Vector3(0, 1.3f, -2.72f), Vector3.left, Vector3.up, 1.1f, 2.2f, 0f);
            // door on +Z too
            win.AddQuad(new Vector3(0, 1.3f, 2.72f), Vector3.right, Vector3.up, 1.1f, 2.2f, 0f);
            Geo.MeshObj("Openings", h.transform, win.Build("win"), new[] { matFrame });

            var col = h.AddComponent<BoxCollider>();
            col.size = new Vector3(7f, 4.8f, 5.4f);
            col.center = new Vector3(0, 2.4f, 0);

            Vector3 fwd = Quaternion.Euler(0, yaw, 0) * Vector3.back;
            occupied.Add(new Rect(pos.x - 6f, pos.z - 5f, 12f, 10f));
            WorldData.PickupSpots.Add(pos + fwd * 4.5f + new Vector3(0, 0.5f, 0));
        }

        void SpawnWell(Vector3 pos)
        {
            var w = Geo.Obj("Well", root, pos);
            Geo.Cylinder("Ring", w.transform, new Vector3(0, 0.55f, 0), 1.15f, 1.1f, matConcrete, 14, 0.5f);
            Geo.Box("PostL", w.transform, new Vector3(-1.05f, 1.6f, 0), new Vector3(0.16f, 2.2f, 0.16f), matWood);
            Geo.Box("PostR", w.transform, new Vector3(1.05f, 1.6f, 0), new Vector3(0.16f, 2.2f, 0.16f), matWood);
            var roof = new MeshBuilder();
            roof.AddBox(new Vector3(0, 3.0f, 0), new Vector3(2.9f, 0.18f, 1.9f), Quaternion.Euler(25f, 0, 0), 0.5f);
            Geo.MeshObj("Roof", w.transform, roof.Build("wellroof"), new[] { matWood });
            var c = w.AddComponent<CapsuleCollider>();
            c.radius = 1.25f; c.height = 2.4f; c.center = new Vector3(0, 1.2f, 0);
            occupied.Add(new Rect(pos.x - 2.5f, pos.z - 2.5f, 5, 5));
        }

        // =============================================================== camp
        void BuildCamp()
        {
            Vector3 p = WorldData.PlayerSpawn;
            var camp = Geo.Obj("StartCamp", root, new Vector3(p.x, 0, p.z - 4f)).transform;

            // barriers
            SpawnBarrier(new Vector3(24, 0.5f, -246), 20f);
            SpawnBarrier(new Vector3(38, 0.5f, -244), -15f);
            SpawnBarrier(new Vector3(31, 0.5f, -238), 90f);

            // crates + tent
            SpawnCrate(new Vector3(36, 0.6f, -230));
            SpawnCrate(new Vector3(34.4f, 0.6f, -231.2f));
            var tent = new MeshBuilder();
            tent.AddBox(new Vector3(0, 1.2f, 0), new Vector3(3.6f, 2.4f, 0.2f), Quaternion.Euler(0, 0, -62f), 0.5f);
            tent.AddBox(new Vector3(1.6f, 1.2f, 0), new Vector3(3.6f, 2.4f, 0.2f), Quaternion.Euler(0, 0, 62f), 0.5f);
            var tentGo = Geo.MeshObj("Tent", camp, tent.Build("tent"), new[] { AssetLibrary.Solid("tent", new Color(0.35f, 0.38f, 0.26f), 0f, 0.1f) }, new Vector3(-5f, 0, 2f), Quaternion.Euler(0, 40f, 0));
            Geo.AddBoxCollider(tentGo, new Vector3(3.6f, 2.4f, 2.8f), new Vector3(0.8f, 1.2f, 0));

            // lamps near the camp + along arterial roads
            SpawnLamp(new Vector3(22, 0, -228), 90f);
            SpawnLamp(new Vector3(39, 0, -236), -90f);
            for (float z = -190; z <= 300; z += 48f)
            {
                SpawnLamp(new Vector3(21.2f, 0, z), 90f);
                SpawnLamp(new Vector3(39.2f, 0, z + 22f), -90f);
            }

            // wrecked cars near camp and on boulevard
            SpawnCar(new Vector3(24, 0, -215), 8f, 0);
            SpawnCar(new Vector3(42, 0, -200), -172f, 1);
            for (int i = 0; i < 22; i++)
            {
                float x = rand(14f, 46f);
                float z = rand(-160f, 300f);
                if (Mathf.Abs(z - 170f) < 12f) continue;
                SpawnCar(new Vector3(x, 0, z), 180f + rand(-14f, 14f), rnd.Next(5));
            }
            SpawnCar(new Vector3(-60, 0, 170), 92f, 2);
            SpawnCar(new Vector3(120, 0, 166), 86f, 3);

            // barrier rows closing the avenue near downtown
            for (int i = 0; i < 6; i++)
                SpawnBarrier(new Vector3(30f + i * 2.6f, 0.5f, 52f), rand(-8f, 8f));

            WorldData.EnemySpawnPoints.Add(new Vector3(30, 0.2f, -150));
            WorldData.EnemySpawnPoints.Add(new Vector3(60, 0.2f, -100));
            WorldData.EnemySpawnPoints.Add(new Vector3(-40, 0.2f, -60));
            WorldData.EnemySpawnPoints.Add(new Vector3(90, 0.2f, -20));
        }

        // ============================================================= nature
        void BuildNature()
        {
            BuildMountains();

            int trees = 250;
            int spawned = 0, guard = 0;
            while (spawned < trees && guard++ < 4000)
            {
                float x = rand(-480f, 480f);
                float z = rand(-480f, 480f);
                if (!FreeForNature(x, z)) continue;
                float density = 1f;
                // denser forest north and south-west
                if (z > 300f) density = 1.25f;
                if (!randChance(Mathf.Clamp01(density)))
                {
                    if (!randChance(0.4f)) continue;
                }
                SpawnTree(new Vector3(x, 0, z));
                spawned++;
            }

            for (int i = 0; i < 85; i++)
            {
                float x = rand(-480f, 480f), z = rand(-480f, 480f);
                if (!FreeForNature(x, z, 4f)) continue;
                SpawnRock(new Vector3(x, 0, z));
            }

            for (int i = 0; i < 120; i++)
            {
                float x, z;
                if (randChance(0.5f))
                {
                    x = rand(-320f, 320f); z = rand(-320f, 320f);
                }
                else
                {
                    x = rand(-136f, 136f); z = rand(40f, 300f);
                    if (NearRoad(x, z, 12f)) continue;
                }
                if (!FreeForNature(x, z, 1.5f)) continue;
                SpawnBush(new Vector3(x, 0, z));
            }
        }

        bool NearRoad(float x, float z, float margin)
        {
            if (Mathf.Abs(x - 30f) < margin && z > -470f && z < 320f) return true;
            if (Mathf.Abs(z - 170f) < margin && x > -470f && x < 400f) return true;
            if (Mathf.Abs(z - 68f) < margin) return true;
            if (Mathf.Abs(z - 10f) < margin && x < -130f) return true;
            if (Mathf.Abs(z + 40f) < margin && x > 130f) return true;
            return false;
        }

        bool FreeForNature(float x, float z, float margin = 0f)
        {
            if (Mathf.Abs(x) > 478f || Mathf.Abs(z) > 478f) return false;
            if (CityRect.Contains(new Vector2(x, z))) return false;
            if (IndRect.Contains(new Vector2(x, z))) return false;
            if (VillageRect.Contains(new Vector2(x, z))) return false;
            if (NearRoad(x, z, 14f + margin)) return false;
            if (Mathf.Abs(x - 30f) < 24f && z < -190f && z > -260f) return false; // camp
            var p = new Vector2(x, z);
            for (int i = 0; i < occupied.Count; i++)
            {
                var r = occupied[i];
                if (r.Contains(p)) return false;
                if (margin > 0 && r.Contains(p + new Vector2(margin, 0))) return false;
            }
            return true;
        }

        Mesh[] pineMeshes, broadleafMeshes;
        int leafVariants = 3;

        void EnsureTreeMeshes()
        {
            if (pineMeshes != null) return;
            pineMeshes = new Mesh[leafVariants];
            broadleafMeshes = new Mesh[leafVariants];
            for (int v = 0; v < leafVariants; v++)
            {
                var mb = new MeshBuilder();
                mb.Submesh = 0;
                float th = 2.6f + v * 0.4f;
                mb.AddTaperedCylinder(Vector3.zero, 0.3f, 0.16f, th, 7, true, 0.4f);
                mb.Submesh = 1;
                int layers = 3;
                for (int i = 0; i < layers; i++)
                {
                    float rBase = 2.5f - i * 0.62f + v * 0.12f;
                    float y = th * 0.65f + i * 1.75f;
                    mb.AddTaperedCylinder(new Vector3(0, y, 0), rBase, 0.03f, 2.9f - i * 0.3f, 9, true, 0.4f);
                }
                pineMeshes[v] = mb.Build("pine" + v);

                var mb2 = new MeshBuilder();
                mb2.Submesh = 0;
                float th2 = 3.0f + v * 0.3f;
                mb2.AddTaperedCylinder(Vector3.zero, 0.34f, 0.2f, th2, 7, true, 0.4f);
                mb2.Submesh = 1;
                var rngT = new Random(90 + v);
                float baseY = th2 + 0.6f;
                for (int i = 0; i < 4; i++)
                {
                    float rad = 1.7f + (float)rngT.NextDouble() * 0.9f;
                    Vector3 off = new Vector3(
                        (float)(rngT.NextDouble() - 0.5) * 1.7f,
                        baseY + i * 0.75f + (float)rngT.NextDouble() * 0.5f,
                        (float)(rngT.NextDouble() - 0.5) * 1.7f);
                    mb2.AddSphere(off, new Vector3(rad, rad * 0.82f, rad), 8, 6);
                }
                broadleafMeshes[v] = mb2.Build("leaf" + v);
            }
        }

        void SpawnTree(Vector3 pos)
        {
            EnsureTreeMeshes();
            bool pine = randChance(0.55f);
            int v = rnd.Next(leafVariants);
            var mesh = pine ? pineMeshes[v] : broadleafMeshes[v];
            var mats = pine
                ? new[] { AssetLibrary.Bark(), AssetLibrary.PineLeaf(v) }
                : new[] { AssetLibrary.Bark(), AssetLibrary.Leaf(v) };
            float scale = lerp(0.85f, 1.45f);
            var go = Geo.MeshObj(pine ? "Pine" : "Tree", root, mesh, mats, pos,
                Quaternion.Euler(rand(-2f, 2f), rand(0f, 360f), rand(-2f, 2f)));
            go.transform.localScale = Vector3.one * scale;
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = 0.3f; c.height = 2.6f; c.center = new Vector3(0, 1.3f, 0);
            occupied.Add(new Rect(pos.x - 1.6f, pos.z - 1.6f, 3.2f, 3.2f));
        }

        void SpawnRock(Vector3 pos)
        {
            float s = lerp(0.5f, 2.4f);
            var go = Geo.Sphere("Rock", root, pos + Vector3.up * s * 0.32f,
                new Vector3(s, s * rand(0.45f, 0.8f), s * rand(0.8f, 1.3f)),
                AssetLibrary.Rock(), 7, 5);
            go.transform.rotation = Quaternion.Euler(rand(-12f, 12f), rand(0f, 360f), rand(-12f, 12f));
            var c = go.AddComponent<SphereCollider>();
            c.radius = 0.7f;
            occupied.Add(new Rect(pos.x - s, pos.z - s, s * 2, s * 2));
        }

        void SpawnBush(Vector3 pos)
        {
            float s = lerp(0.55f, 1.3f);
            var go = Geo.Sphere("Bush", root, pos + Vector3.up * s * 0.45f,
                new Vector3(s, s * 0.75f, s), AssetLibrary.Leaf(rnd.Next(leafVariants)), 7, 5);
            go.transform.rotation = Quaternion.Euler(0, rand(0f, 360f), 0);
        }

        void BuildMountains()
        {
            var mat = AssetLibrary.Solid("mountain", new Color(0.30f, 0.33f, 0.30f), 0f, 0.1f);
            for (int i = 0; i < 26; i++)
            {
                float ang = i / 26f * Mathf.PI * 2f + rand(-0.06f, 0.06f);
                float dist = rand(600f, 800f);
                float x = Mathf.Cos(ang) * dist;
                float z = Mathf.Sin(ang) * dist;
                float h = rand(90f, 210f);
                float r = rand(110f, 240f);
                var go = Geo.MeshObj("Mountain", root, MakeConeMesh(), new[] { mat },
                    new Vector3(x, -2f, z), Quaternion.Euler(0, rand(0f, 360f), 0));
                go.transform.localScale = new Vector3(r, h, r * rand(0.7f, 1.3f));
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        Mesh MakeConeMesh()
        {
            var mb = new MeshBuilder();
            mb.AddTaperedCylinder(Vector3.zero, 1f, 0.02f, 1f, 7, true, 0.5f);
            return mb.Build("mountain");
        }

        // ============================================================== props
        void SpawnCrate(Vector3 pos)
        {
            var go = Geo.BoxCol("Crate", root, pos, new Vector3(1.1f, 1.1f, 1.1f), matWood, Quaternion.Euler(0, rand(0f, 360f), 0), 0.5f);
            if (randChance(0.4f))
            {
                Geo.Box("Crate", root, pos + new Vector3(rand(-0.2f, 0.2f), 1.15f, rand(-0.2f, 0.2f)),
                    new Vector3(0.9f, 0.9f, 0.9f), matWood, Quaternion.Euler(0, rand(0f, 360f), 0), 0.5f);
            }
        }

        void SpawnBarrier(Vector3 pos, float yaw)
        {
            var go = Geo.BoxCol("Barrier", root, pos, new Vector3(2.2f, 1f, 0.55f), matConcrete,
                Quaternion.Euler(0, yaw, 0), 0.5f);
            Geo.Box("Stripe", go.transform, new Vector3(0, 0.18f, 0.29f), new Vector3(2.22f, 0.22f, 0.02f),
                matHazard, Quaternion.identity, 0.5f);
        }

        void SpawnLamp(Vector3 pos, float yaw)
        {
            var go = Geo.Obj("Lamp", root, pos, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder();
            mb.Submesh = 0;
            mb.AddTaperedCylinder(Vector3.zero, 0.1f, 0.07f, 6f, 7, true, 0.5f);
            mb.AddBox(new Vector3(0.75f, 5.85f, 0), new Vector3(1.6f, 0.1f, 0.1f), 0f, 0.5f);
            mb.Submesh = 1;
            mb.AddBox(new Vector3(1.5f, 5.75f, 0), new Vector3(0.55f, 0.14f, 0.3f), 0f, 0.5f);
            Geo.MeshObj("Mesh", go.transform, mb.Build("lamp"), new[] { matDark, AssetLibrary.LampGlow() });

            var c = go.AddComponent<CapsuleCollider>();
            c.radius = 0.13f; c.height = 6f; c.center = new Vector3(0, 3f, 0);
            occupied.Add(new Rect(pos.x - 0.5f, pos.z - 0.5f, 1, 1));
        }

        void SpawnCar(Vector3 pos, float yaw, int colorIdx)
        {
            Color[] colors =
            {
                new Color(0.55f, 0.16f, 0.13f), new Color(0.20f, 0.26f, 0.34f),
                new Color(0.72f, 0.72f, 0.70f), new Color(0.25f, 0.30f, 0.22f),
                new Color(0.42f, 0.42f, 0.45f),
            };
            var go = Geo.Obj("Car", root, pos, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder();
            mb.Submesh = 0;
            mb.AddBox(new Vector3(0, 0.62f, 0), new Vector3(1.92f, 0.6f, 4.35f), 0f, 0.4f);
            mb.AddBox(new Vector3(0, 1.18f, -0.15f), new Vector3(1.72f, 0.56f, 2.2f), 0f, 0.4f);
            mb.Submesh = 1; // wheels
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1.42f, 1.42f })
                    mb.AddBox(new Vector3(sx * 0.9f, 0.33f, sz), new Vector3(0.24f, 0.62f, 0.62f), 0f, 0.5f);
            mb.Submesh = 2; // glass
            mb.AddBox(new Vector3(0, 1.2f, -0.16f), new Vector3(1.74f, 0.42f, 1.6f), 0f, 0.4f);
            Geo.MeshObj("Body", go.transform, mb.Build("car"),
                new[] { AssetLibrary.Painted(colors[colorIdx % colors.Length]), matDark, AssetLibrary.Glass() });

            Geo.AddBoxCollider(go, new Vector3(1.95f, 1.5f, 4.4f), new Vector3(0, 0.75f, 0));
            occupied.Add(new Rect(pos.x - 2.4f, pos.z - 2.4f, 4.8f, 4.8f));
        }

        void FenceLine(Vector3 a, Vector3 b, bool alongZ, float gap = 0f)
        {
            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 1f) return;
            Vector3 dir = delta / len;
            float step = 2.2f;
            int count = Mathf.FloorToInt(len / step);
            if (count <= 0) return;

            var line = Geo.Obj("FenceLine", root, a, Quaternion.LookRotation(dir));
            var mb = new MeshBuilder();
            var blocked = new bool[count];
            float gapCenter = len * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) * step;
                if (gap > 0f && Mathf.Abs(t - gapCenter) < gap * 0.5f) { blocked[i] = false; continue; }
                blocked[i] = true;

                mb.AddBox(new Vector3(0, 1.05f, t), new Vector3(0.12f, 0.12f, step), 0f, 0.5f);
                mb.AddBox(new Vector3(0, 0.52f, t), new Vector3(0.1f, 0.1f, step), 0f, 0.5f);
                mb.AddBox(new Vector3(0, 1.0f, t - step * 0.5f), new Vector3(0.16f, 2f, 0.16f), 0f, 0.5f);
            }
            if (!mb.IsEmpty)
                Geo.MeshObj("Fence", line.transform, mb.Build("fence"), new[] { matDark });

            // merge consecutive pieces into long colliders
            int runStart = -1;
            for (int i = 0; i <= count; i++)
            {
                bool blockedHere = i < count && blocked[i];
                if (blockedHere && runStart < 0) runStart = i;
                if (!blockedHere && runStart >= 0)
                {
                    int runLen = i - runStart;
                    float zMid = (runStart + runLen * 0.5f) * step;
                    var col = line.AddComponent<BoxCollider>();
                    col.center = new Vector3(0, 1.05f, zMid);
                    col.size = new Vector3(0.22f, 2.1f, runLen * step);
                    runStart = -1;
                }
            }
            occupied.Add(new Rect(Mathf.Min(a.x, b.x) - 1f, Mathf.Min(a.z, b.z) - 1f,
                Mathf.Abs(b.x - a.x) + 2f, Mathf.Abs(b.z - a.z) + 2f));
        }

        void BuildBoundary()
        {
            float h = 40f;
            var wallMat = AssetLibrary.Solid("invisible", new Color(0, 0, 0, 0), 0f, 0f);
            var go = Geo.Obj("Bounds", root);
            void Wall(Vector3 c, Vector3 size)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = c; col.size = size;
            }
            Wall(new Vector3(0, h * 0.5f, 502), new Vector3(1010, h, 4));
            Wall(new Vector3(0, h * 0.5f, -502), new Vector3(1010, h, 4));
            Wall(new Vector3(502, h * 0.5f, 0), new Vector3(4, h, 1010));
            Wall(new Vector3(-502, h * 0.5f, 0), new Vector3(4, h, 1010));
        }

        void BuildPickups()
        {
            int i = 0;
            foreach (var p in WorldData.PickupSpots)
            {
                if (!WorldData.InsideWorld(p, 5f)) continue;
                var type = (i % 3 == 1) ? PickupKind.Medkit : PickupKind.Ammo;
                Pickup.Spawn(p, type);
                i++;
            }
        }

        void MakeSmoke(Vector3 pos)
        {
            var go = new GameObject("ChimneySmoke");
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 9f;
            main.startSpeed = 0.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startColor = new Color(0.72f, 0.72f, 0.75f, 0.28f);
            main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 2.6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 1.4f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = 0.9f;
            vel.y = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            vel.z = 0.35f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.7f, 0.7f, 0.72f), 0f),
                new GradientColorKey(new Color(0.8f, 0.8f, 0.82f), 1f)
            }, new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.15f),
                new GradientAlphaKey(0.22f, 0.6f), new GradientAlphaKey(0f, 1f)
            });
            col.color = grad;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 1.6f));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = AssetLibrary.Unlit("smoke", Color.white, AssetLibrary.Tex("Smoke"));
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            ps.Play();
        }

        // =========================================================== navmesh
        void BakeNavMesh()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(1040, 260, 1040));
            var sources = new List<NavMeshBuildSource>();
            try
            {
                NavMeshBuilder.CollectSources(bounds, LayerMask.GetMask("Default"), NavMeshCollectGeometry.PhysicsColliders,
                    0, new List<NavMeshBuildMarkup>(), sources);
            }
            catch (Exception e)
            {
                Debug.LogWarning("NavMesh collect failed: " + e.Message);
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            if (NavMesh.GetSettingsCount() == 0 || settings.agentTypeID < 0)
            {
                settings = new NavMeshBuildSettings
                {
                    agentTypeID = 0,
                    agentRadius = 0.45f,
                    agentHeight = 1.9f,
                    agentSlope = 45f,
                    agentClimb = 0.45f,
                    ledgeDropHeight = 0.5f,
                    maxJumpAcrossDistance = 0.2f,
                    minRegionArea = 6f,
                    overrideVoxelSize = false,
                    overrideTileSize = false
                };
            }
            settings.agentRadius = 0.42f;
            settings.agentHeight = 1.9f;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 45f;
            settings.minRegionArea = 8f;

            navData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (navData == null)
            {
                Debug.LogError("NavMesh build returned null");
                return;
            }
            navInstance = NavMesh.AddNavMeshData(navData);

            if (!NavMesh.SamplePosition(WorldData.PlayerSpawn, out _, 14f, NavMesh.AllAreas))
                Debug.LogWarning("Player spawn is not on navmesh (still fine for CharacterController).");
        }

        // ============================================================ helpers
        float rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        bool randChance(float p) => rnd.NextDouble() < p;
        float lerp(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
    }
}
