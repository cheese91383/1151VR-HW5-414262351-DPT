using UnityEngine;

// 掛在 TrainerRoot 上。WASD 移動、Shift 跑步、自動轉向、重力貼地。
[RequireComponent(typeof(CharacterController))]
public class TrainerController : MonoBehaviour
{
    [Header("移動")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    [Tooltip("每秒最多轉幾度")]
    public float turnSpeed = 720f;
    public float gravity = -20f;
    [Tooltip("勾選:人物比 1.8 公尺大的話,走路速度自動等比例變快(看起來才不會很慢)")]
    public bool scaleSpeedWithHeight = true;
    [Tooltip("勾選:W 是「往畫面裡面走」,D 是「往畫面右邊走」(以攝影機方向為準)")]
    public bool cameraRelative = true;

    [Header("動畫(可不填,會自動找子物件的 Animator)")]
    public Animator animator;
    [Tooltip("Animator 裡如果有 Float 參數叫這個名字,就會送移動速度進去(有走路動畫才需要)")]
    public string speedParam = "Speed";

    [Header("開始時自動把 CharacterController 調成人物身高")]
    public bool autoFitController = true;

    [HideInInspector] public bool canMove = true;
    [HideInInspector] public bool lockedByEncounter = false;
    // 目前水平移動速度(走路動畫用)
    public float CurrentSpeed { get; private set; }
    // 人物身高(公尺)
    public float Height => cc != null ? cc.height * Mathf.Abs(transform.lossyScale.y) : 1.8f;

    CharacterController cc;
    Camera cam;
    float verticalVelocity;
    bool hasSpeedParam;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false; // 動畫不要自己移動人物,避免亂飄或倒地
            if (animator.runtimeAnimatorController != null)
            {
                foreach (var p in animator.parameters)
                    if (p.name == speedParam && p.type == AnimatorControllerParameterType.Float)
                        hasSpeedParam = true;
            }
        }
        if (autoFitController) FitController();
    }

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        Vector2 input = (canMove && !lockedByEncounter) ? GameInput.Move() : Vector2.zero;

        Vector3 dir;
        if (cameraRelative && cam != null)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = cam.transform.right; right.y = 0f; right.Normalize();
            dir = fwd * input.y + right * input.x;
        }
        else
        {
            dir = new Vector3(input.x, 0f, input.y);
        }
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float speed = GameInput.RunHeld() ? runSpeed : walkSpeed;
        if (scaleSpeedWithHeight) speed *= Mathf.Max(0.1f, Height / 1.8f);

        // 轉向:面向移動方向
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // 重力
        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        CurrentSpeed = dir.magnitude * speed;
        Vector3 move = dir * speed;
        move.y = verticalVelocity;
        cc.Move(move * Time.deltaTime);

        if (hasSpeedParam)
            animator.SetFloat(speedParam, dir.magnitude * speed, 0.1f, Time.deltaTime);
    }

    // 立刻轉向某個點(丟球前用)
    public void FaceTowards(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
    }

    // 依照模型大小自動設定 CharacterController 的 Height / Radius / Center
    void FitController()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        bool first = true;
        Bounds b = new Bounds();
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        if (first) return;

        float scaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        float h = b.size.y / scaleY;
        float bottom = (b.min.y - transform.position.y) / scaleY;
        if (h < 0.01f) return;

        cc.height = h;
        cc.radius = h * 0.2f;
        cc.skinWidth = Mathf.Max(0.001f, cc.radius * 0.1f);
        cc.stepOffset = Mathf.Min(0.3f / scaleY, h * 0.25f);
        cc.center = new Vector3(0f, bottom + h * 0.5f + cc.skinWidth, 0f);
    }
}
