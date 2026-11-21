using System.Collections.Generic;
using UnityEngine;

// 掛在每一隻寶可夢上(PikachuF、Eevee、Rowlet)。
// 不用 NavMesh:在出生點附近隨機挑點走過去,用 Terrain 高度或射線把腳貼在地上。
// 訓練家走進 stopDistance 範圍內,寶可夢會停下來、轉頭看訓練家,這時就可以丟球收服。
public class PokemonWander : MonoBehaviour
{
    public static readonly List<PokemonWander> All = new List<PokemonWander>();

    public enum State { Wander, Idle, Alert, BeingCaught, Captured, Battle }

    [Header("顯示名稱(空白 = 用物件名稱)")]
    public string displayName = "";

    [Header("隨機亂跑")]
    [Tooltip("以開始位置為中心,最遠跑多遠")]
    public float wanderRadius = 8f;
    public float moveSpeed = 1.5f;
    public float turnSpeed = 360f;
    [Tooltip("走到目的地後休息幾秒(最小,最大)")]
    public Vector2 idleTime = new Vector2(1f, 3f);
    [Tooltip("挑目的地時,跟出生點的高度差超過這個就不去(避免爬上山)")]
    public float maxHeightDifference = 2f;

    [Header("跳跳走路(高度 = 身高的比例)")]
    public float hopRatio = 0.12f;
    public float hopFrequency = 8f;

    [Header("訓練家靠近就停下來")]
    [Tooltip("不填會自動找有 TrainerController 的物件")]
    public Transform trainer;
    public float stopDistance = 5f;

    [Header("配合訓練家大小")]
    [Tooltip("勾選:訓練家比 1.8 公尺大時,亂跑範圍、速度、停下距離自動等比例放大")]
    public bool scaleWithTrainer = true;
    [Tooltip("寶可夢身高 = 訓練家身高 × 這個比例。0 = 不自動調整大小。皮卡丘 0.3、伊布 0.3、木木梟 0.25")]
    public float heightToTrainer = 0f;

    [Header("模型方向修正:走路方向不對時改 90 / 180 / -90")]
    public float modelYawOffset = 0f;
    [Tooltip("腳離地太高/太低可以微調")]
    public float groundOffset = 0f;

    [Header("戰鬥數值")]
    [Tooltip("血量")]
    public int maxHP = 30;
    [Tooltip("攻擊力(每次攻擊大約扣這麼多血,會有 ±30% 隨機)")]
    public int attack = 15;
    [HideInInspector] public int hp;

    [Header("收服特效顏色")]
    public Color effectColor = new Color(1f, 0.85f, 0.3f);

    public State CurrentState { get; private set; } = State.Idle;
    public bool IsCatchable => CurrentState == State.Alert;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    public float Height { get; private set; } = 1f;

    // 身體中心(丟球瞄準用)
    public Vector3 AimPoint => transform.position + Vector3.up * (footOffset + Height * 0.5f);

    Vector3 home;
    Vector3 targetPos;
    float idleTimer;
    float wanderTimer;
    float hopPhase;
    float alertJump;      // 發現訓練家時跳一下
    float footOffset;     // 物件原點到腳底的距離

    void Awake()
    {
        All.Add(this);
        MeasureModel();
        EnsureCollider();
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Start()
    {
        if (trainer == null)
        {
            var t = FindAnyObjectByType<TrainerController>();
            if (t != null) trainer = t.transform;
        }
        var tc = trainer != null ? trainer.GetComponent<TrainerController>() : null;
        if (tc != null)
        {
            if (heightToTrainer > 0f && Height > 0.0001f)
            {
                float want = tc.Height * heightToTrainer;
                transform.localScale *= want / Height;
                MeasureModel();
            }
            if (scaleWithTrainer)
            {
                float k = Mathf.Max(0.1f, tc.Height / 1.8f);
                wanderRadius *= k;
                moveSpeed *= k;
                stopDistance *= k;
                maxHeightDifference *= k;
            }
        }

        // 寶可夢不要跟訓練家互撞(避免寶可夢跳起來把訓練家和鏡頭頂上去)
        if (trainer != null)
            foreach (var mine in GetComponentsInChildren<Collider>())
                foreach (var his in trainer.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(mine, his, true);

        home = transform.position;
        idleTimer = Random.Range(idleTime.x, idleTime.y);
        CurrentState = State.Idle;
        SnapToGround(0f);
    }

    void Update()
    {
        if (CurrentState == State.BeingCaught || CurrentState == State.Captured || CurrentState == State.Battle) return;

        float dist = float.MaxValue;
        if (trainer != null)
        {
            Vector3 d = trainer.position - transform.position; d.y = 0f;
            dist = d.magnitude;
        }

        // 訓練家靠近 → 停下來
        if (CurrentState != State.Alert && dist < stopDistance)
        {
            CurrentState = State.Alert;
            alertJump = 0.35f;
        }
        else if (CurrentState == State.Alert && dist > stopDistance * 1.3f)
        {
            CurrentState = State.Idle;
            idleTimer = Random.Range(idleTime.x, idleTime.y);
        }

        float hop = 0f;
        switch (CurrentState)
        {
            case State.Alert:
                if (trainer != null) TurnTowards(trainer.position - transform.position, turnSpeed * 2f);
                break;

            case State.Idle:
                idleTimer -= Time.deltaTime;
                if (idleTimer <= 0f) PickNewTarget();
                break;

            case State.Wander:
                Vector3 to = targetPos - transform.position; to.y = 0f;
                wanderTimer -= Time.deltaTime;
                if (to.magnitude < 0.3f || wanderTimer <= 0f)
                {
                    CurrentState = State.Idle;
                    idleTimer = Random.Range(idleTime.x, idleTime.y);
                }
                else
                {
                    TurnTowards(to, turnSpeed);
                    Vector3 step = to.normalized * moveSpeed * Time.deltaTime;
                    transform.position += step;
                    hopPhase += Time.deltaTime * hopFrequency;
                    hop = Mathf.Abs(Mathf.Sin(hopPhase)) * Height * hopRatio;
                }
                break;
        }

        // 發現訓練家時跳一下
        if (alertJump > 0f)
        {
            alertJump -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(alertJump / 0.35f);
            hop = Mathf.Sin(t * Mathf.PI) * Height * 0.35f;
        }

        SnapToGround(hop);
    }

    void PickNewTarget()
    {
        float homeGround = GroundHelper.GroundY(home, transform);
        for (int i = 0; i < 12; i++)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            Vector3 p = home + new Vector3(r.x, 0f, r.y);
            if (!GroundHelper.InsideTerrain(p)) continue;
            float gy = GroundHelper.GroundY(p, transform);
            if (Mathf.Abs(gy - homeGround) > maxHeightDifference) continue;
            targetPos = p;
            wanderTimer = wanderRadius * 2f / Mathf.Max(0.1f, moveSpeed) + 2f;
            CurrentState = State.Wander;
            return;
        }
        idleTimer = 1f; // 找不到好位置,等一下再找
    }

    void TurnTowards(Vector3 dir, float degPerSec)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degPerSec * Time.deltaTime);
    }

    void SnapToGround(float hop)
    {
        Vector3 p = transform.position;
        float gy = GroundHelper.GroundY(p, transform);
        p.y = gy - footOffset + groundOffset + hop;
        transform.position = p;
    }

    // 量模型高度與腳底位置
    void MeasureModel()
    {
        if (GroundHelper.TryGetBounds(gameObject, out Bounds b))
        {
            Height = Mathf.Max(0.05f, b.size.y);
            footOffset = b.min.y - transform.position.y;
        }
    }

    // 沒有碰撞器就自動加一個 Trigger 的 BoxCollider(讓精靈球打得到)
    void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;
        if (!GroundHelper.TryGetBounds(gameObject, out Bounds b)) return;
        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        Vector3 s = transform.lossyScale;
        box.center = transform.InverseTransformPoint(b.center);
        box.size = new Vector3(
            b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
            b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
    }

    // ===== 給精靈球呼叫 =====
    public void BeginCapture()
    {
        CurrentState = State.BeingCaught;
    }

    public void MarkCaptured()
    {
        CurrentState = State.Captured;
        gameObject.SetActive(false);
    }

    // ===== 給 EncounterManager(遇敵/戰鬥)呼叫 =====
    public void EnterBattle()
    {
        CurrentState = State.Battle;
        hp = maxHP;
    }

    // 戰鬥結束、回到草地上亂跑
    public void ExitBattle()
    {
        CurrentState = State.Idle;
        idleTimer = Random.Range(idleTime.x, idleTime.y);
    }

    // 放到某個位置並貼地
    public void PlaceAt(Vector3 pos)
    {
        transform.position = pos;
        SnapToGround(0f);
    }

    // 立刻面向某個點
    public void FaceInstant(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    // 已收服的寶可夢放出來戰鬥
    public void SendOut(Vector3 pos, Vector3 lookAt)
    {
        gameObject.SetActive(true);
        EnterBattle();
        PlaceAt(pos);
        FaceInstant(lookAt);
    }

    // 收回精靈球
    public void ReturnToBall()
    {
        CurrentState = State.Captured;
        gameObject.SetActive(false);
    }

    // 逃走後回到出生點
    public void Respawn()
    {
        gameObject.SetActive(true);
        PlaceAt(home);
        ExitBattle();
    }
}

// 找地面高度的小工具(寶可夢、精靈球共用)
public static class GroundHelper
{
    public static bool InsideTerrain(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        if (t == null) return true; // 沒有 Terrain 就不限制
        Vector3 tp = t.transform.position;
        Vector3 size = t.terrainData.size;
        return p.x > tp.x + 1f && p.x < tp.x + size.x - 1f &&
               p.z > tp.z + 1f && p.z < tp.z + size.z - 1f;
    }

    // 先用 Terrain 高度;不在 Terrain 上就從上方 2 公尺往下打射線
    public static float GroundY(Vector3 p, Transform ignore)
    {
        var t = Terrain.activeTerrain;
        if (t != null)
        {
            Vector3 tp = t.transform.position;
            Vector3 size = t.terrainData.size;
            if (p.x >= tp.x && p.x <= tp.x + size.x && p.z >= tp.z && p.z <= tp.z + size.z)
                return t.SampleHeight(p) + tp.y;
        }

        Vector3 origin = p + Vector3.up * 2f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        float y = p.y;
        foreach (var h in hits)
        {
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            if (h.collider.GetComponentInParent<PokemonWander>() != null) continue;
            if (h.collider.GetComponentInParent<TrainerController>() != null) continue;
            if (h.collider.GetComponentInParent<PokeballProjectile>() != null) continue;
            if (h.distance < bestDist) { bestDist = h.distance; y = h.point.y; }
        }
        return y;
    }

    public static bool TryGetBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return !first;
    }
}
