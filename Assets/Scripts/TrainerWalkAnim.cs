using UnityEngine;

// 掛在 TrainerRoot 上(跟 TrainerController 同一個物件)。
// 沒有走路動畫也沒關係:用程式讓腿前後擺、膝蓋彎、手臂擺動、身體上下起伏。
// 人物要是 Humanoid(你的訓練家是)。
[RequireComponent(typeof(TrainerController))]
public class TrainerWalkAnim : MonoBehaviour
{
    [Tooltip("不填會自動找子物件的 Animator")]
    public Animator animator;

    [Header("擺動幅度(度)")]
    public float legSwing = 30f;
    public float kneeBend = 45f;
    public float armSwing = 25f;
    [Tooltip("膝蓋往前彎的話改成 -1")]
    public float kneeDirection = 1f;
    [Tooltip("手臂往奇怪方向轉的話改成 -1")]
    public float armDirection = 1f;

    [Header("節奏")]
    [Tooltip("走路速度 3 時,每秒走幾步")]
    public float stepsPerSecond = 2.2f;
    [Tooltip("身體上下起伏(身高的比例)")]
    public float bobRatio = 0.02f;

    TrainerController controller;
    PokeballThrow thrower;
    Transform model;
    Vector3 modelBasePos;
    float phase;
    float weight;

    Transform[] bones = new Transform[6];
    Quaternion[] lastBase = new Quaternion[6];
    Quaternion[] lastResult = new Quaternion[6];
    bool[] hasLast = new bool[6];

    const int LUL = 0, RUL = 1, LLL = 2, RLL = 3, LUA = 4, RUA = 5;

    void Awake()
    {
        controller = GetComponent<TrainerController>();
        thrower = GetComponent<PokeballThrow>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        // 在 Awake 抓骨頭(這時 Animator 一定是開著的)
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning("[TrainerWalkAnim] 找不到 Humanoid 的 Animator,走路動畫不會動");
            enabled = false;
            return;
        }
        bones[LUL] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        bones[RUL] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        bones[LLL] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        bones[RLL] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        bones[LUA] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        bones[RUA] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        model = animator.transform;
        modelBasePos = model.localPosition;
    }

    void LateUpdate()
    {
        float speed = controller.CurrentSpeed;
        float targetWeight = speed > 0.1f ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, targetWeight, Time.deltaTime * 5f);

        float k = Mathf.Max(0.1f, controller.Height / 1.8f);
        phase += Time.deltaTime * stepsPerSecond * Mathf.PI * Mathf.Max(0.5f, speed / (3f * k));

        float s = Mathf.Sin(phase);
        Vector3 axis = transform.right;

        // 腿:左右相反
        Apply(LUL, axis, s * legSwing * weight);
        Apply(RUL, axis, -s * legSwing * weight);
        // 膝蓋:腳往後時彎
        Apply(LLL, axis, Mathf.Max(0f, s) * kneeBend * kneeDirection * weight);
        Apply(RLL, axis, Mathf.Max(0f, -s) * kneeBend * kneeDirection * weight);
        // 手:跟同側腿相反;丟球時不動手
        bool throwing = thrower != null && !controller.canMove;
        float armW = throwing ? 0f : weight;
        Apply(LUA, axis, -s * armSwing * armDirection * armW);
        Apply(RUA, axis, s * armSwing * armDirection * armW);

        // 身體起伏
        if (model != null && model != transform)
        {
            float bob = Mathf.Abs(Mathf.Cos(phase)) * bobRatio * controller.Height * weight;
            model.localPosition = modelBasePos + Vector3.up * bob / Mathf.Max(0.0001f, transform.lossyScale.y);
        }
    }

    // 在動畫算好的姿勢上再轉一點(不會越轉越多)
    void Apply(int i, Vector3 axis, float angle)
    {
        Transform b = bones[i];
        if (b == null) return;

        // Animator 這一格沒有重寫骨頭的話,先還原成上一格的原始姿勢
        Quaternion baseLocal = b.localRotation;
        if (hasLast[i] && Quaternion.Angle(baseLocal, lastResult[i]) < 0.01f)
            baseLocal = lastBase[i];
        b.localRotation = baseLocal;

        if (Mathf.Abs(angle) > 0.001f)
            b.rotation = Quaternion.AngleAxis(angle, axis) * b.rotation;

        lastBase[i] = baseLocal;
        lastResult[i] = b.localRotation;
        hasLast[i] = true;
    }
}
