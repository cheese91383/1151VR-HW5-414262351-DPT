using UnityEngine;

// 掛在 Main Camera 上。攝影機從「側面」跟著訓練家,
// 寶可夢放在訓練家右邊時,丟球就會是「由左至右」的拋物線。
public class CameraFollow : MonoBehaviour
{
    [Tooltip("不填會自動找有 TrainerController 的物件(請不要放寶可夢)")]
    public Transform target;
    [Tooltip("攝影機相對訓練家的位置(世界座標,不會跟著人物旋轉)")]
    public Vector3 offset = new Vector3(0f, 3f, -9f);
    [Tooltip("攝影機看向訓練家腳底往上多高的地方")]
    public float lookHeight = 1f;
    [Tooltip("左右前後跟隨的平滑程度,越大越緊")]
    public float smooth = 5f;
    [Tooltip("上下跟隨的平滑程度,越小越穩(鏡頭不會跟著上下晃)")]
    public float heightSmooth = 1.5f;
    [Tooltip("勾選:依訓練家身高自動放大 Offset 和 Look Height(以 1.8 公尺身高為準)")]
    public bool scaleWithTrainerHeight = true;

    float followY;

    // 遇敵時改用指定的位置和看的點
    bool hasOverride;
    Vector3 overridePos, overrideLook, currentLook;

    public void SetOverride(Vector3 position, Vector3 lookAt)
    {
        hasOverride = true;
        overridePos = position;
        overrideLook = lookAt;
    }

    public void ClearOverride()
    {
        hasOverride = false;
    }

    void Start()
    {
        // 只跟訓練家,不要跟寶可夢
        if (target == null || target.GetComponentInParent<TrainerController>() == null)
        {
            var t = FindAnyObjectByType<TrainerController>();
            if (t != null) target = t.transform;
        }
        if (target == null) return;

        // 攝影機不能是訓練家或寶可夢的子物件
        if (transform.parent != null) transform.SetParent(null, true);

        if (scaleWithTrainerHeight)
        {
            var tc = target.GetComponent<TrainerController>();
            if (tc != null)
            {
                float k = Mathf.Max(0.1f, tc.Height / 1.8f);
                offset *= k;
                lookHeight *= k;
            }
        }
        followY = target.position.y;
        transform.position = target.position + offset;
        currentLook = target.position + Vector3.up * lookHeight;
        transform.LookAt(currentLook);
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (hasOverride)
        {
            float t = 1f - Mathf.Exp(-3f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, overridePos, t);
            currentLook = Vector3.Lerp(currentLook, overrideLook, t);
            transform.LookAt(currentLook);
            return;
        }
        followY = Mathf.Lerp(followY, target.position.y, 1f - Mathf.Exp(-heightSmooth * Time.deltaTime));
        Vector3 anchor = new Vector3(target.position.x, followY, target.position.z);

        Vector3 want = anchor + offset;
        transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        currentLook = Vector3.Lerp(currentLook, anchor + Vector3.up * lookHeight, 1f - Mathf.Exp(-6f * Time.deltaTime));
        transform.LookAt(currentLook);
    }
}
