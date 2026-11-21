using System.Collections;
using UnityEngine;

// 不用手動掛!PokeballThrow 丟球時會自動加到球上。
// 打中寶可夢 → 閃光 → 寶可夢縮進球裡 → 球掉到地上搖 3 下 → 星星特效 → 收服成功。
// 打到地面 → 1.5 秒後消失。
public class PokeballProjectile : MonoBehaviour
{
    PokeballThrow owner;
    Rigidbody rb;
    bool resolved;      // 已經抓到或已判定沒中
    float life;
    float ballRadius = 0.12f;

    public void Init(PokeballThrow thrower)
    {
        owner = thrower;
        rb = GetComponent<Rigidbody>();
        if (GroundHelper.TryGetBounds(gameObject, out Bounds b))
            ballRadius = Mathf.Max(0.02f, Mathf.Max(b.extents.x, b.extents.y, b.extents.z));
    }

    void Update()
    {
        if (resolved) return;
        life += Time.deltaTime;
        if (life > 6f || transform.position.y < -100f)
        {
            resolved = true;
            StartCoroutine(MissRoutine(0f));
        }
    }

    void OnCollisionEnter(Collision c) { Hit(c.collider); }
    void OnTriggerEnter(Collider c) { Hit(c); }

    void Hit(Collider c)
    {
        if (resolved) return;
        if (c.GetComponentInParent<TrainerController>() != null) return;

        var p = c.GetComponentInParent<PokemonWander>();
        if (owner.debugLog)
            Debug.Log("[Pokeball] 球碰到 " + c.name + (p != null ? "(寶可夢 " + p.DisplayName + ")" : "") +
                      ",鎖定目標 " + (owner.lockedTarget != null ? owner.lockedTarget.DisplayName : "無"));
        if (p != null && owner.lockedTarget != null && p != owner.lockedTarget) return; // 旁邊別隻,忽略
        if (p != null)
        {
            if (p.CurrentState == PokemonWander.State.BeingCaught ||
                p.CurrentState == PokemonWander.State.Captured) return;
            resolved = true;
            StartCoroutine(CaptureRoutine(p));
            return;
        }

        // 打到地面或其他東西 → 沒中
        resolved = true;
        StartCoroutine(MissRoutine(1.5f));
    }

    IEnumerator CaptureRoutine(PokemonWander p)
    {
        p.BeginCapture();

        // 1. 球停在空中、往後彈一點
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        Vector3 hitPos = transform.position;
        Vector3 back = owner.transform.position - p.transform.position; back.y = 0f;
        back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.zero;
        Vector3 hover = hitPos + back * ballRadius * 4f + Vector3.up * ballRadius * 4f;

        Quaternion faceCam = FaceCameraRotation();
        Quaternion startRot = transform.rotation;
        yield return Lerp(0.25f, t =>
        {
            transform.position = Vector3.Lerp(hitPos, hover, Ease(t));
            transform.rotation = Quaternion.Slerp(startRot, faceCam, t);
        });

        // 2. 閃光 + 寶可夢縮進球裡
        float scale = Mathf.Max(0.3f, p.Height);
        CaptureEffect.CaptureFlash(p.AimPoint, p.effectColor, scale);

        Vector3 pStartPos = p.transform.position;
        Vector3 pStartScale = p.transform.localScale;
        Vector3 pivotToCenter = p.AimPoint - pStartPos;
        yield return Lerp(0.5f, t =>
        {
            float e = Ease(t);
            p.transform.localScale = Vector3.Lerp(pStartScale, Vector3.zero, e);
            // 讓身體中心往球移動
            Vector3 center = Vector3.Lerp(pStartPos + pivotToCenter, hover, e);
            p.transform.position = center - pivotToCenter * (1f - e);
        });
        p.gameObject.SetActive(false);
        p.transform.localScale = pStartScale;
        CaptureEffect.Puff(hover, Color.white, ballRadius * 6f);

        // 3. 球掉到地上
        float groundY = GroundHelper.GroundY(hover, transform) + ballRadius;
        Vector3 landPos = new Vector3(hover.x, groundY, hover.z);
        yield return Lerp(0.3f, t =>
        {
            transform.position = Vector3.Lerp(hover, landPos, t * t);
        });
        CaptureEffect.Puff(landPos, new Color(0.8f, 0.75f, 0.6f), ballRadius * 5f);

        // 4. 搖 3 下
        Vector3 axis = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.forward;
        axis.Normalize();
        Quaternion baseRot = transform.rotation;
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.35f);
            yield return Lerp(0.45f, t =>
            {
                float angle = Mathf.Sin(t * Mathf.PI * 2f) * 25f;
                transform.rotation = Quaternion.AngleAxis(angle, axis) * baseRot;
            });
            transform.rotation = baseRot;
        }

        // 5. 收服成功!星星特效
        yield return new WaitForSeconds(0.3f);
        CaptureEffect.Success(landPos + Vector3.up * ballRadius, p.effectColor, scale);
        p.MarkCaptured();
        owner.OnCaptured(p, landPos);

        yield return new WaitForSeconds(1.5f);
        CaptureEffect.Puff(transform.position, Color.white, ballRadius * 6f);
        owner.OnBallFinished();
        Destroy(gameObject);
    }

    IEnumerator MissRoutine(float wait)
    {
        if (wait > 0f) yield return new WaitForSeconds(wait);
        CaptureEffect.Puff(transform.position, Color.white, ballRadius * 6f);
        owner.OnMissed();
        owner.OnBallFinished();
        Destroy(gameObject);
    }

    Quaternion FaceCameraRotation()
    {
        if (Camera.main == null) return transform.rotation;
        Vector3 toCam = Camera.main.transform.position - transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) return transform.rotation;
        return Quaternion.LookRotation(toCam, Vector3.up);
    }

    static float Ease(float t) { return t * t * (3f - 2f * t); }

    static IEnumerator Lerp(float duration, System.Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }
}
