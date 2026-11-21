using System.Collections;
using UnityEngine;

// 掛在 TrainerRoot 上(跟 TrainerController 同一個物件)。
// 按 空白鍵 或 滑鼠左鍵:播 Throw 動畫 → 在出手那一刻從手上丟出精靈球(拋物線)。
// 附近有「停下來」的寶可夢時會自動瞄準牠。
[RequireComponent(typeof(TrainerController))]
public class PokeballThrow : MonoBehaviour
{
    [Header("精靈球")]
    [Tooltip("把場景裡的 Pokeball 拖進來(或 Pokeball 的 Prefab)。場景裡那顆會在開始時自動隱藏,只當樣本複製。")]
    public GameObject ballPrefab;
    [Tooltip("丟出去的球直徑(公尺)。0 = 用原本大小")]
    public float ballDiameter = 0.25f;

    [Header("手上的球")]
    [Tooltip("訓練家模型手上「保留」的那顆球(可以是骨頭,例如 RPokeballN)。丟出時縮成 0 隱藏,收服完再恢復。可不填。")]
    public GameObject handBall;
    [Tooltip("一開始就要藏起來的球骨頭(例如 LPokeballN、LPokeballM、RPokeballM)。開始時會縮成 0。")]
    public Transform[] hideOnStart;
    [Tooltip("手上的球如果由好幾根骨頭組成(例如 RPokeballM),放在這裡,丟出時會跟 Hand Ball 一起縮成 0")]
    public Transform[] moreHandBalls;
    [Tooltip("出手位置。不填 → 用手上的球位置 → 再不行用右手骨頭")]
    public Transform throwPoint;

    [Header("動畫")]
    [Tooltip("不填會自動找子物件的 Animator")]
    public Animator animator;
    [Tooltip("Animator 裡的 Trigger 參數名稱")]
    public string throwTrigger = "Throw";
    [Tooltip("沒有 Trigger 參數時,直接播這個名字的狀態")]
    public string throwStateName = "Throw";
    [Tooltip("勾選:不用管 Animator 的參數和箭頭,程式直接控制 Throw 狀態(開始時停在第一格,按鍵才播一次)")]
    public bool directControl = true;
    [Tooltip("勾選:沒在丟球時用程式擺出自然站姿(手放下);取消:停在 Throw 動畫的某一格")]
    public bool useModelPoseWhenIdle = true;
    [Tooltip("站姿:手臂往下放多少(-1 最下面 ~ 1 舉高)。Play 時可以直接拉,馬上看到效果")]
    [Range(-1f, 1f)] public float idleArmDown = -0.6f;
    [Tooltip("站姿:手臂往前多少(-1 往後 ~ 1 往前)")]
    [Range(-1f, 1f)] public float idleArmFront = 0.1f;
    [Tooltip("站姿:手肘伸直程度(1 = 全直,越小越彎)")]
    [Range(-1f, 1f)] public float idleElbow = 0.7f;
    [Tooltip("上面取消勾選時,停在 Throw 動畫的哪個位置(0 = 開頭, 1 = 結尾)")]
    [Range(0f, 1f)] public float idlePoseTime = 0f;
    [Tooltip("Throw 動畫長度(秒),在動畫檔 Inspector 的 Length 可以看到")]
    public float throwAnimLength = 0.833f;
    [Tooltip("按下後幾秒球離手(對準 Throw 動畫手往前甩的那一刻)")]
    public float releaseDelay = 0.35f;
    [Tooltip("丟球時人物不能走的秒數")]
    public float lockMoveTime = 0.8f;

    [Header("拋物線")]
    [Tooltip("球水平飛行速度,越小飛越久、弧線越高")]
    public float horizontalSpeed = 7f;
    [Tooltip("最遠可以瞄準多遠的寶可夢")]
    public float maxTargetDistance = 15f;
    [Header("蓄力(按住空白鍵越久丟越遠,放開才丟)")]
    [Tooltip("按住幾秒會蓄滿")]
    public float chargeTime = 1.2f;
    [Tooltip("最短丟多遠(以 1.8 公尺身高的人為準,人物比較大會自動等比例放大)")]
    public float minThrowDistance = 3f;
    [Tooltip("最遠丟多遠(同上)")]
    public float maxThrowDistance = 15f;
    [Tooltip("勾選:有停下來的寶可夢時自動瞄準牠(一定丟得到);取消:完全靠蓄力控制距離")]
    public bool autoAimStoppedPokemon = true;
    [Tooltip("勾選:丟出去的球大小自動 = 訓練家身高 × Ball Size Ratio(會忽略 Ball Diameter)")]
    public bool autoBallSize = true;
    public float ballSizeRatio = 0.12f;
    [Tooltip("在 Console 印出丟球過程,找問題用")]
    public bool debugLog = true;
    [Tooltip("球收回後多久可以再丟")]
    public float cooldown = 0.5f;

    [Header("音效(可不填)")]
    public AudioClip throwSound;
    public AudioClip captureSound;

    [Header("自訂特效 Prefab(可不填,不填就用程式產生的粒子特效)")]
    public GameObject customCaptureEffect;

    [Header("畫面提示文字")]
    public bool showHUD = true;

    // 給 EncounterManager 用
    [HideInInspector] public bool inputEnabled = true;
    // 蓄力條上的建議力道標記(0~1,-1 = 不顯示)
    [HideInInspector] public float hintPower = -1f;
    // 指定只能收服這一隻(遇敵時用)。其他寶可夢站在旁邊,球會穿過去、不會誤抓
    [HideInInspector] public PokemonWander lockedTarget;
    public float MinThrowDistance => minThrowDistance;
    public float MaxThrowDistance => maxThrowDistance;
    public bool IsBusy => busy;
    public readonly System.Collections.Generic.List<PokemonWander> captured = new System.Collections.Generic.List<PokemonWander>();
    public System.Action<PokemonWander> Captured;
    public System.Action BallFinished;

    TrainerController controller;
    Vector3 handBallScale = Vector3.one;
    bool handHidden;
    Vector3[] moreHandBallScales;
    bool busy;
    bool charging;
    float charge;   // 0~1
    int capturedCount;
    int totalCount;
    string message = "";
    float messageTimer;

    Transform[] poseBones;
    Vector3[] posePos;
    Quaternion[] poseRot;
    Vector3[] poseScale;

    void Awake()
    {
        controller = GetComponent<TrainerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        // 記住模型原本的姿勢(Animator 還沒動之前)
        if (animator != null)
        {
            poseBones = animator.GetComponentsInChildren<Transform>(true);
            posePos = new Vector3[poseBones.Length];
            poseRot = new Quaternion[poseBones.Length];
            poseScale = new Vector3[poseBones.Length];
            for (int i = 0; i < poseBones.Length; i++)
            {
                posePos[i] = poseBones[i].localPosition;
                poseRot[i] = poseBones[i].localRotation;
                poseScale[i] = poseBones[i].localScale;
            }
        }
    }

    HumanPoseHandler poseHandler;
    HumanPose idlePose;
    bool animPlaying;

    void SetupIdlePose()
    {
        if (animator == null || !animator.isHuman || animator.avatar == null) return;
        poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        idlePose = new HumanPose();
        poseHandler.GetHumanPose(ref idlePose);
    }

    // 用 Humanoid 肌肉值擺出自然站姿(全部歸零 = 放鬆站姿,再調手臂)
    void ApplyIdlePose()
    {
        if (poseHandler == null) return;
        var p = idlePose;
        p.muscles = new float[idlePose.muscles.Length];
        string[] names = HumanTrait.MuscleName;
        for (int i = 0; i < p.muscles.Length && i < names.Length; i++)
        {
            string n = names[i];
            if (n.EndsWith("Arm Down-Up")) p.muscles[i] = idleArmDown;
            else if (n.EndsWith("Arm Front-Back")) p.muscles[i] = idleArmFront;
            else if (n.EndsWith("Forearm Stretch")) p.muscles[i] = idleElbow;
        }
        poseHandler.SetHumanPose(ref p);
    }

    void OnDestroy()
    {
        if (poseHandler != null) poseHandler.Dispose();
    }

    void RestoreModelPose()
    {
        if (poseBones == null) return;
        for (int i = 0; i < poseBones.Length; i++)
        {
            if (poseBones[i] == null || poseBones[i] == animator.transform) continue;
            poseBones[i].localPosition = posePos[i];
            poseBones[i].localRotation = poseRot[i];
            poseBones[i].localScale = poseScale[i];
        }
    }

    void Start()
    {
        // 場景裡的 Pokeball 只當「樣本」,隱藏起來
        if (ballPrefab != null && ballPrefab.scene.IsValid())
            ballPrefab.SetActive(false);
        totalCount = PokemonWander.All.Count;
        if (handBall != null) handBallScale = handBall.transform.localScale;
        if (moreHandBalls != null)
        {
            moreHandBallScales = new Vector3[moreHandBalls.Length];
            for (int i = 0; i < moreHandBalls.Length; i++)
                if (moreHandBalls[i] != null) moreHandBallScales[i] = moreHandBalls[i].localScale;
        }
        if (hideOnStart != null)
            foreach (var t in hideOnStart)
                if (t != null) t.localScale = Vector3.zero;
        if (directControl) FreezeOnFirstFrame();
    }

    bool HasThrowState()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        return animator.HasState(0, Animator.StringToHash(throwStateName)) ||
               animator.HasState(0, Animator.StringToHash("Base Layer." + throwStateName));
    }

    // 沒在丟球時的站姿
    void FreezeOnFirstFrame()
    {
        if (useModelPoseWhenIdle)
        {
            if (animator != null) animator.enabled = false;
            RestoreModelPose();
            animPlaying = false;
            if (poseHandler == null) SetupIdlePose();
            ApplyIdlePose();
            return;
        }
        if (!HasThrowState()) return;
        animator.enabled = true;
        animator.speed = 1f;
        animator.Play(throwStateName, 0, idlePoseTime);
        animator.Update(0f);
        animator.speed = 0f;
    }

    IEnumerator PlayOnceThenFreeze()
    {
        animPlaying = true;
        animator.enabled = true;
        animator.speed = 1f;
        animator.Play(throwStateName, 0, 0f);
        yield return new WaitForSeconds(Mathf.Max(0.1f, throwAnimLength));
        FreezeOnFirstFrame();
    }

    float TrainerHeight()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) return 1.8f;
        return cc.height * Mathf.Abs(transform.lossyScale.y);
    }

    void Update()
    {
        if (messageTimer > 0f) messageTimer -= Time.deltaTime;
        if (directControl && useModelPoseWhenIdle && !animPlaying) ApplyIdlePose();
        if (busy || !inputEnabled) { charging = false; return; }

        if (!charging && GameInput.ThrowPressed())
        {
            charging = true;
            charge = 0f;
            if (debugLog) Debug.Log("[PokeballThrow] 開始蓄力");
        }
        if (charging)
        {
            charge = Mathf.Clamp01(charge + Time.deltaTime / Mathf.Max(0.05f, chargeTime));
            if (!GameInput.ThrowHeld())
            {
                charging = false;
                if (debugLog) Debug.Log("[PokeballThrow] 放開丟球,力道 " + Mathf.RoundToInt(charge * 100) + "%");
                StartCoroutine(ThrowRoutine(charge));
            }
        }
    }

    PokemonWander FindTarget()
    {
        PokemonWander best = null;
        float bestDist = maxTargetDistance * Mathf.Max(0.1f, TrainerHeight() / 1.8f);
        foreach (var p in PokemonWander.All)
        {
            if (p == null || !p.isActiveAndEnabled || !p.IsCatchable) continue;
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    // 指定目標丟球(遇敵選單的「捕捉」用)
    public bool ThrowAt(PokemonWander target)
    {
        if (busy || ballPrefab == null) return false;
        StartCoroutine(ThrowRoutine(1f, target));
        return true;
    }

    IEnumerator ThrowRoutine(float power, PokemonWander forced = null)
    {
        if (ballPrefab == null)
        {
            ShowMessage("PokeballThrow 的 Ball Prefab 還沒設定!", 3f);
            yield break;
        }

        busy = true;
        // 遇敵中(lockedTarget):一律面向被鎖定的那隻,力道自己控制,不會被旁邊停下來的別隻搶走目標
        PokemonWander target = forced != null ? forced
            : (lockedTarget != null ? null : (autoAimStoppedPokemon ? FindTarget() : null));
        if (lockedTarget != null) controller.FaceTowards(lockedTarget.transform.position);
        else if (target != null) controller.FaceTowards(target.transform.position);

        controller.canMove = false;
        PlayThrowAnimation();

        yield return new WaitForSeconds(releaseDelay);
        ReleaseBall(target, power);

        yield return new WaitForSeconds(Mathf.Max(0f, lockMoveTime - releaseDelay));
        controller.canMove = true;
        // busy 會在球結束後(OnBallFinished)才解除
    }

    void PlayThrowAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        if (directControl && HasThrowState() && animator != null)
        {
            StartCoroutine(PlayOnceThenFreeze());
            return;
        }

        foreach (var p in animator.parameters)
        {
            if (p.name == throwTrigger && p.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(throwTrigger);
                return;
            }
        }
        int shortHash = Animator.StringToHash(throwStateName);
        int fullHash = Animator.StringToHash("Base Layer." + throwStateName);
        if (animator.HasState(0, shortHash) || animator.HasState(0, fullHash))
            animator.CrossFadeInFixedTime(throwStateName, 0.1f, 0, 0f);
    }

    Vector3 GetReleasePosition()
    {
        if (throwPoint != null) return throwPoint.position;
        if (handBall != null) return handBall.transform.position;
        if (animator != null && animator.isHuman)
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null) return hand.position;
        }
        return transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
    }

    void ReleaseBall(PokemonWander target, float power)
    {
        float k = Mathf.Max(0.1f, TrainerHeight() / 1.8f);   // 人物大小倍率
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();

        // 出手點往前推到身體外面,避免球卡在自己身上
        Vector3 start = GetReleasePosition();
        var myCC = GetComponent<CharacterController>();
        float bodyR = myCC != null ? myCC.radius * Mathf.Abs(transform.lossyScale.x) : 0.3f * k;
        Vector3 fromCenter = start - transform.position; fromCenter.y = 0f;
        float ahead = Vector3.Dot(fromCenter, fwd);
        if (ahead < bodyR * 1.3f) start += fwd * (bodyR * 1.3f - ahead);
        handHidden = true;

        GameObject ball = Instantiate(ballPrefab, start, Random.rotation);
        ball.name = "ThrownPokeball";
        ball.SetActive(true);

        // 調整大小
        float diameter = autoBallSize ? TrainerHeight() * ballSizeRatio : ballDiameter;
        if (diameter > 0f && GroundHelper.TryGetBounds(ball, out Bounds b))
        {
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size > 0.0001f) ball.transform.localScale *= diameter / size;
        }
        if (debugLog)
            Debug.Log("[PokeballThrow] 球丟出去了,位置 " + start + ",直徑 " + diameter +
                      ",訓練家身高 " + TrainerHeight() + ",目標 " + (target != null ? target.DisplayName : "無") +
                      ",Hand Ball " + (handBall != null ? handBall.name : "沒填"));

        // 碰撞器:沒有就自動加球形碰撞器
        Collider[] cols = ball.GetComponentsInChildren<Collider>();
        if (cols.Length == 0)
        {
            var sc = ball.AddComponent<SphereCollider>();
            if (GroundHelper.TryGetBounds(ball, out Bounds bb))
            {
                float worldR = Mathf.Max(bb.extents.x, bb.extents.y, bb.extents.z);
                sc.center = ball.transform.InverseTransformPoint(bb.center);
                sc.radius = worldR / Mathf.Max(0.0001f, ball.transform.lossyScale.x);
            }
            cols = new Collider[] { sc };
        }
        foreach (var c in cols) c.isTrigger = false;

        // 不要撞到訓練家自己
        foreach (var mine in GetComponentsInChildren<Collider>())
            foreach (var c in cols)
                Physics.IgnoreCollision(c, mine, true);

        // Rigidbody
        var rb = ball.GetComponent<Rigidbody>();
        if (rb == null) rb = ball.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 0.2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 拋物線:算出「剛好在 flightTime 秒後打到目標」的初速度
        Vector3 aim;
        if (target != null)
        {
            aim = target.AimPoint;
        }
        else
        {
            float dist = Mathf.Lerp(minThrowDistance, maxThrowDistance, power) * k;
            aim = transform.position + fwd * dist;
            aim.y = GroundHelper.GroundY(aim, transform);
        }
        Vector3 flat = aim - start; flat.y = 0f;
        float flightTime = Mathf.Clamp(flat.magnitude / Mathf.Max(0.1f, horizontalSpeed * Mathf.Sqrt(k)), 0.5f, 2f);
        Vector3 velocity = (aim - start) / flightTime - 0.5f * Physics.gravity * flightTime;
        rb.linearVelocity = velocity;
        rb.angularVelocity = transform.right * 15f; // 往前翻滾

        var proj = ball.AddComponent<PokeballProjectile>();
        proj.Init(this);

        if (throwSound != null) AudioSource.PlayClipAtPoint(throwSound, start);
    }

    // ===== 給 PokeballProjectile 呼叫 =====
    public void OnCaptured(PokemonWander p, Vector3 ballPos)
    {
        capturedCount++;
        if (!captured.Contains(p)) captured.Add(p);
        if (Captured != null) Captured(p);
        if (captureSound != null) AudioSource.PlayClipAtPoint(captureSound, ballPos);
        if (customCaptureEffect != null)
        {
            var fx = Instantiate(customCaptureEffect, ballPos, Quaternion.identity);
            Destroy(fx, 5f);
        }
        if (capturedCount >= totalCount && totalCount > 0)
            ShowMessage("太好了!" + p.DisplayName + " 收服成功!全部寶可夢都收服了!", 5f);
        else
            ShowMessage("太好了!" + p.DisplayName + " 收服成功!", 3f);
    }

    public void OnMissed()
    {
        ShowMessage("沒丟中!靠近一點讓寶可夢停下來再丟", 2.5f);
    }

    public void OnBallFinished()
    {
        StartCoroutine(ReadyAgain());
    }

    IEnumerator ReadyAgain()
    {
        yield return new WaitForSeconds(cooldown);
        handHidden = false;
        if (handBall != null) handBall.transform.localScale = handBallScale;
        SetMoreHandBalls(true);
        busy = false;
        if (BallFinished != null) BallFinished();
    }

    // 在 Animator 更新之後才縮小,避免動畫把骨頭大小蓋回去
    void LateUpdate()
    {
        if (hideOnStart != null)
            foreach (var t in hideOnStart)
                if (t != null) t.localScale = Vector3.zero;
        if (handHidden)
        {
            if (handBall != null) handBall.transform.localScale = Vector3.zero;
            SetMoreHandBalls(false);
        }
    }

    void SetMoreHandBalls(bool show)
    {
        if (moreHandBalls == null || moreHandBallScales == null) return;
        for (int i = 0; i < moreHandBalls.Length; i++)
            if (moreHandBalls[i] != null)
                moreHandBalls[i].localScale = show ? moreHandBallScales[i] : Vector3.zero;
    }

    public void ShowMessage(string text, float seconds)
    {
        message = text;
        messageTimer = seconds;
    }

    void OnGUI()
    {
        if (!showHUD) return;
        float s = Screen.height / 720f;

        var small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * s) };
        small.normal.textColor = Color.white;
        var big = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(30 * s),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        big.normal.textColor = Color.yellow;

        GUI.Label(new Rect(15 * s, 10 * s, 900 * s, 40 * s), "已收服: " + capturedCount + " / " + totalCount, small);
        GUI.Label(new Rect(15 * s, 40 * s, 900 * s, 40 * s), "WASD 移動   Shift 跑步   按住空白鍵蓄力、放開丟精靈球", small);

        // 蓄力條
        if (charging)
        {
            float w = 300 * s, h = 22 * s;
            float x = (Screen.width - w) * 0.5f, y = Screen.height * 0.68f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(Color.green, Color.red, charge);
            GUI.DrawTexture(new Rect(x, y, w * charge, h), Texture2D.whiteTexture);
            GUI.color = old;
            if (hintPower >= 0f)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x + w * hintPower - 2, y - 6 * s, 4, h + 12 * s), Texture2D.whiteTexture);
                GUI.color = old;
            }
            GUI.Label(new Rect(x, y - 30 * s, w, 28 * s), "力道 " + Mathf.RoundToInt(charge * 100) + "%(放開丟出)", small);
        }

        string center = "";
        if (messageTimer > 0f) center = message;
        else if (!busy && inputEnabled)
        {
            var t = FindTarget();
            if (t != null) center = t.DisplayName + " 停下來了!按住空白鍵再放開,丟出精靈球!";
        }
        if (center != "")
            GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 60 * s), center, big);
    }
}
