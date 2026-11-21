using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 掛在 TrainerRoot 上(跟 TrainerController、PokeballThrow 同一個物件)。
// 走近寶可夢 → 牠停下來 → 鏡頭切到訓練家背後 → 下方出現綠色選單:
//   戰鬥:派出已收服的寶可夢自動對打(有血量、攻擊力、傷害數字)
//        打贏 → 選「收服」或「放走」;打輸 → 野生寶可夢逃走,過一陣子回到原地
//   捕捉:直接丟精靈球
//   逃跑:離開,走遠一點再回來才會再遇到
// 不用做 Canvas,畫面全部用程式畫。
public class EncounterManager : MonoBehaviour
{
    [Header("鏡頭(以 1.8 公尺身高為準,會自動放大)")]
    [Tooltip("鏡頭在訓練家後面多遠")]
    public float camBack = 2.4f;
    [Tooltip("鏡頭往訓練家右邊偏多少(讓訓練家在畫面左邊)")]
    public float camSide = 1.2f;
    [Tooltip("鏡頭高度")]
    public float camHeight = 1.9f;

    [Header("選單顏色")]
    public Color panelColor = new Color(0.18f, 0.55f, 0.25f, 0.92f);
    public Color buttonColor = new Color(0.30f, 0.75f, 0.35f, 1f);

    [Header("捕捉")]
    [Tooltip("勾選:按「捕捉」後由你自己按住空白鍵蓄力、放開丟球(沒丟中可以再丟)。取消勾選:腳本自動丟球")]
    public bool manualCatch = true;

    [Header("戰鬥")]
    [Tooltip("打輸後,野生寶可夢幾秒後回到原本的地方")]
    public float respawnDelay = 10f;
    [Tooltip("每次攻擊之間的間隔(秒)")]
    public float turnDelay = 0.6f;

    enum Phase { None, Menu, ChooseMon, Battling, WinChoice, AimThrow, Throwing }
    Phase phase = Phase.None;

    TrainerController trainer;
    PokeballThrow thrower;
    CameraFollow cam;
    PokemonWander wild;
    PokemonWander mine;
    readonly List<PokemonWander> declined = new List<PokemonWander>();

    string panelText = "";
    string tempText = "";
    float tempTimer;

    class FloatText { public string text; public Vector3 pos; public float t; public Color color; }
    readonly List<FloatText> floats = new List<FloatText>();

    Quaternion wildRot, mineRot;

    void Awake()
    {
        trainer = GetComponent<TrainerController>();
        thrower = GetComponent<PokeballThrow>();
    }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraFollow>();
        if (thrower != null) thrower.BallFinished += OnBallFinished;
    }

    void OnDestroy()
    {
        if (thrower != null) thrower.BallFinished -= OnBallFinished;
    }

    float K => trainer != null ? Mathf.Max(0.1f, trainer.Height / 1.8f) : 1f;

    void Update()
    {
        if (tempTimer > 0f) tempTimer -= Time.deltaTime;
        for (int i = floats.Count - 1; i >= 0; i--)
        {
            floats[i].t += Time.deltaTime;
            if (floats[i].t > 1.2f) floats.RemoveAt(i);
        }

        // 走遠了才可以再遇到同一隻
        for (int i = declined.Count - 1; i >= 0; i--)
        {
            var p = declined[i];
            if (p == null || !p.gameObject.activeInHierarchy) { declined.RemoveAt(i); continue; }
            if (FlatDist(p.transform.position, transform.position) > p.stopDistance * 1.5f) declined.RemoveAt(i);
        }

        if (phase == Phase.AimThrow)
        {
            UpdateAim();
            return;
        }
        if (phase != Phase.None || thrower == null || thrower.IsBusy) return;

        foreach (var p in PokemonWander.All)
        {
            if (p == null || !p.isActiveAndEnabled || !p.IsCatchable || declined.Contains(p)) continue;
            StartEncounter(p);
            break;
        }
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ===== 遇敵 =====
    void StartEncounter(PokemonWander p)
    {
        wild = p;
        thrower.lockedTarget = p;
        wild.EnterBattle();
        wildRot = wild.transform.rotation;
        trainer.lockedByEncounter = true;
        thrower.inputEnabled = false;
        trainer.FaceTowards(wild.transform.position);
        wild.FaceInstant(transform.position);
        wildRot = wild.transform.rotation;
        SetBattleCamera();
        panelText = "野生的 " + wild.DisplayName + " 出現了!要怎麼做?";
        phase = Phase.Menu;
    }

    void SetBattleCamera()
    {
        if (cam == null || wild == null) return;
        Vector3 T = transform.position;
        Vector3 W = wild.transform.position;
        Vector3 dir = W - T; dir.y = 0f;
        float dist = dir.magnitude;
        dir = dist > 0.001f ? dir / dist : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        float k = K;
        Vector3 pos = T - dir * camBack * k + right * camSide * k + Vector3.up * camHeight * k;
        Vector3 look = T + dir * dist * 0.55f + Vector3.up * 0.8f * k;
        cam.SetOverride(pos, look);
    }

    void EndEncounter(bool addDeclined)
    {
        if (cam != null) cam.ClearOverride();
        trainer.lockedByEncounter = false;
        thrower.inputEnabled = true;
        thrower.lockedTarget = null;
        if (addDeclined && wild != null && !declined.Contains(wild)) declined.Add(wild);
        wild = null;
        mine = null;
        phase = Phase.None;
    }

    void ShowTemp(string text, float seconds = 2f)
    {
        tempText = text;
        tempTimer = seconds;
    }

    // ===== 按鈕 =====
    void OnFight()
    {
        if (thrower.captured.Count == 0)
        {
            ShowTemp("你還沒有寶可夢!先「捕捉」一隻吧");
            return;
        }
        panelText = "要派誰出場?";
        phase = Phase.ChooseMon;
    }

    // 自己丟球:按住空白鍵蓄力、放開丟出,Esc 回選單
    void UpdateAim()
    {
        if (thrower.IsBusy)
        {
            thrower.inputEnabled = false;
            thrower.hintPower = -1f;
            phase = Phase.Throwing;
            return;
        }
        if (wild != null)
        {
            trainer.FaceTowards(wild.transform.position);
            float dist = FlatDist(wild.transform.position, transform.position) / K;
            float range = Mathf.Max(0.01f, thrower.MaxThrowDistance - thrower.MinThrowDistance);
            thrower.hintPower = Mathf.Clamp01((dist - thrower.MinThrowDistance) / range);
        }
        if (GameInput.CancelPressed()) BackToMenu("野生的 " + (wild != null ? wild.DisplayName : "") + " 還在!要怎麼做?");
    }

    void BackToMenu(string text)
    {
        thrower.inputEnabled = false;
        thrower.hintPower = -1f;
        panelText = text;
        phase = Phase.Menu;
    }

    void OnCatch()
    {
        if (manualCatch)
        {
            thrower.inputEnabled = true;
            phase = Phase.AimThrow;
            return;
        }
        if (thrower.ThrowAt(wild))
        {
            panelText = "";
            phase = Phase.Throwing;
        }
        else ShowTemp("現在還不能丟球");
    }

    void OnRun()
    {
        wild.ExitBattle();
        EndEncounter(true);
    }

    void OnBallFinished()
    {
        if (phase != Phase.Throwing) return;
        bool caught = wild != null && wild.CurrentState == PokemonWander.State.Captured;
        if (caught) { thrower.hintPower = -1f; EndEncounter(false); return; }
        if (manualCatch && wild != null)
        {
            BackToMenu("沒丟中!要再試一次嗎?");
            return;
        }
        if (wild != null) wild.ExitBattle();
        EndEncounter(true);
    }

    // ===== 戰鬥 =====
    IEnumerator BattleRoutine(PokemonWander chosen)
    {
        phase = Phase.Battling;
        mine = chosen;

        Vector3 T = transform.position;
        Vector3 W = wild.transform.position;
        Vector3 spawn = Vector3.Lerp(T, W, 0.4f);

        panelText = "去吧!" + mine.DisplayName + "!";
        CaptureEffect.Puff(spawn + Vector3.up * 0.5f * K, Color.white, 0.6f * K);
        mine.SendOut(spawn, W);
        mineRot = mine.transform.rotation;
        Vector3 fullScale = mine.transform.localScale;
        yield return Lerp(0.3f, t => mine.transform.localScale = fullScale * t);
        mine.transform.localScale = fullScale;

        wild.FaceInstant(mine.transform.position);
        wildRot = wild.transform.rotation;
        wild.hp = wild.maxHP;
        mine.hp = mine.maxHP;
        yield return new WaitForSeconds(0.6f);

        PokemonWander a = mine, d = wild;
        for (int turn = 0; turn < 20 && mine.hp > 0 && wild.hp > 0; turn++)
        {
            yield return Attack(a, d);
            if (d.hp <= 0) break;
            var tmp = a; a = d; d = tmp;
            yield return new WaitForSeconds(turnDelay);
        }

        if (wild.hp <= 0)
        {
            yield return Faint(wild);
            panelText = wild.DisplayName + " 倒下了!你贏了!要收服牠嗎?";
            phase = Phase.WinChoice;
        }
        else
        {
            yield return Faint(mine);
            panelText = mine.DisplayName + " 倒下了…野生的 " + wild.DisplayName + " 逃走了!";
            yield return new WaitForSeconds(1f);
            RecallMine();
            StartCoroutine(FleeRoutine(wild));
            yield return new WaitForSeconds(1.2f);
            EndEncounter(false);
        }
    }

    IEnumerator Attack(PokemonWander a, PokemonWander d)
    {
        panelText = a.DisplayName + " 的攻擊!";
        Vector3 start = a.transform.position;
        Vector3 hitPos = Vector3.Lerp(start, d.transform.position, 0.6f);
        yield return Lerp(0.18f, t => a.PlaceAt(Vector3.Lerp(start, hitPos, t)));

        int dmg = Mathf.Max(1, Mathf.RoundToInt(a.attack * Random.Range(0.7f, 1.3f)));
        d.hp = Mathf.Max(0, d.hp - dmg);
        CaptureEffect.Burst(d.AimPoint, Color.white, new Color(1f, 0.4f, 0.2f), 20, 3f * K, 0.25f * K, 0.5f, 0f, 0.2f * K);
        floats.Add(new FloatText { text = "-" + dmg, pos = d.AimPoint + Vector3.up * d.Height * 0.6f, t = 0f, color = new Color(1f, 0.3f, 0.2f) });

        Vector3 dPos = d.transform.position;
        Vector3 side = Vector3.Cross(Vector3.up, (dPos - start).normalized);
        yield return Lerp(0.25f, t =>
        {
            a.PlaceAt(Vector3.Lerp(hitPos, start, t));
            d.PlaceAt(dPos + side * Mathf.Sin(t * Mathf.PI * 6f) * 0.08f * K * (1f - t));
        });
        a.PlaceAt(start);
        d.PlaceAt(dPos);
    }

    IEnumerator Faint(PokemonWander p)
    {
        Quaternion from = p.transform.rotation;
        Quaternion to = Quaternion.AngleAxis(75f, p.transform.right) * from;
        yield return Lerp(0.4f, t => p.transform.rotation = Quaternion.Slerp(from, to, t));
    }

    void RecallMine()
    {
        if (mine == null) return;
        CaptureEffect.Puff(mine.AimPoint, Color.white, 0.6f * K);
        mine.transform.rotation = mineRot;
        mine.ReturnToBall();
    }

    void OnWinCatch()
    {
        RecallMine();
        wild.transform.rotation = wildRot;
        OnCatch();
    }

    void OnWinRelease()
    {
        RecallMine();
        wild.transform.rotation = wildRot;
        wild.ExitBattle();
        EndEncounter(true);
    }

    IEnumerator FleeRoutine(PokemonWander p)
    {
        Vector3 away = p.transform.position - transform.position; away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
        p.FaceInstant(p.transform.position + away);
        float speed = p.moveSpeed * 3f;
        float t = 0f;
        while (t < 2f && p != null && p.gameObject.activeInHierarchy)
        {
            t += Time.deltaTime;
            p.PlaceAt(p.transform.position + away * speed * Time.deltaTime);
            yield return null;
        }
        if (p == null) yield break;
        Vector3 s = p.transform.localScale;
        yield return Lerp(0.3f, k => p.transform.localScale = s * (1f - k));
        p.gameObject.SetActive(false);
        p.transform.localScale = s;
        yield return new WaitForSeconds(respawnDelay);
        p.Respawn();
    }

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

    // ===== 畫面 =====
    Texture2D panelTex, buttonTex, buttonHoverTex, barBgTex;
    GUIStyle textStyle, buttonStyle, smallStyle, floatStyle;

    static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void BuildStyles(float s)
    {
        if (panelTex == null)
        {
            panelTex = MakeTex(panelColor);
            buttonTex = MakeTex(buttonColor);
            buttonHoverTex = MakeTex(Color.Lerp(buttonColor, Color.white, 0.25f));
            barBgTex = MakeTex(new Color(0f, 0f, 0f, 0.6f));
        }
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), fontStyle = FontStyle.Bold, wordWrap = true };
        textStyle.normal.textColor = Color.white;
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        smallStyle.normal.textColor = Color.white;
        floatStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(34 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold };
        buttonStyle.normal.background = buttonTex;
        buttonStyle.hover.background = buttonHoverTex;
        buttonStyle.active.background = buttonHoverTex;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.yellow;
    }

    void OnGUI()
    {
        if (phase == Phase.None) return;
        float s = Screen.height / 720f;
        BuildStyles(s);

        // 血量條
        if (wild != null && wild.gameObject.activeInHierarchy) DrawStatus(wild, s, "野生 ");
        if (mine != null && mine.gameObject.activeInHierarchy) DrawStatus(mine, s, "我方 ");

        // 傷害數字
        if (Camera.main != null)
        {
            foreach (var f in floats)
            {
                Vector3 sp = Camera.main.WorldToScreenPoint(f.pos + Vector3.up * f.t * 0.8f * K);
                if (sp.z < 0f) continue;
                var c = f.color; c.a = 1f - Mathf.Clamp01(f.t / 1.2f);
                floatStyle.normal.textColor = c;
                GUI.Label(new Rect(sp.x - 80 * s, Screen.height - sp.y - 25 * s, 160 * s, 50 * s), f.text, floatStyle);
            }
        }

        if (phase == Phase.Throwing) return;

        if (phase == Phase.AimThrow)
        {
            float ah = 56 * s;
            GUI.DrawTexture(new Rect(Screen.width * 0.04f, Screen.height - ah - Screen.height * 0.03f, Screen.width * 0.92f, ah), panelTex);
            var line = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(22 * s) };
            GUI.Label(new Rect(Screen.width * 0.04f + 20 * s, Screen.height - ah - Screen.height * 0.03f + 10 * s, Screen.width * 0.9f, ah),
                "按住空白鍵蓄力,力道對準白線,放開就丟球(Esc 回選單)", line);
            return;
        }

        // 綠色選單
        float px = Screen.width * 0.04f, pw = Screen.width * 0.92f;
        float ph = Screen.height * 0.24f, py = Screen.height - ph - Screen.height * 0.03f;
        GUI.DrawTexture(new Rect(px, py, pw, ph), panelTex);

        string text = tempTimer > 0f ? tempText : panelText;
        GUI.Label(new Rect(px + 20 * s, py + 12 * s, pw * 0.55f, ph - 24 * s), text, textStyle);

        float bw = Mathf.Min(170 * s, pw * 0.13f), bh = 52 * s, gap = 12 * s;
        float bx = px + pw - 20 * s - bw;
        float by = py + 14 * s;

        switch (phase)
        {
            case Phase.Menu:
                if (Btn(bx - (bw + gap) * 2, by, bw, bh, "戰鬥")) OnFight();
                if (Btn(bx - (bw + gap), by, bw, bh, "捕捉")) OnCatch();
                if (Btn(bx, by, bw, bh, "逃跑")) OnRun();
                break;

            case Phase.ChooseMon:
                {
                    var list = thrower.captured;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list[i];
                        float x = bx - (bw + gap) * (list.Count - i);
                        if (Btn(x, by, bw, bh, p.DisplayName)) { StartCoroutine(BattleRoutine(p)); return; }
                        GUI.Label(new Rect(x, by + bh + 4 * s, bw, 24 * s), "HP " + p.maxHP + " 攻擊 " + p.attack, smallStyle);
                    }
                    if (Btn(bx, by, bw, bh, "返回")) { panelText = "野生的 " + wild.DisplayName + " 出現了!要怎麼做?"; phase = Phase.Menu; }
                }
                break;

            case Phase.WinChoice:
                if (Btn(bx - (bw + gap), by, bw, bh, "收服")) OnWinCatch();
                if (Btn(bx, by, bw, bh, "放走")) OnWinRelease();
                break;
        }
    }

    bool Btn(float x, float y, float w, float h, string label)
    {
        return GUI.Button(new Rect(x, y, w, h), label, buttonStyle);
    }

    void DrawStatus(PokemonWander p, float s, string prefix)
    {
        if (Camera.main == null) return;
        Vector3 top = p.AimPoint + Vector3.up * p.Height * 0.75f;
        Vector3 sp = Camera.main.WorldToScreenPoint(top);
        if (sp.z < 0f) return;
        float w = 190 * s, h = 62 * s;
        float x = sp.x - w * 0.5f, y = Screen.height - sp.y - h;

        GUI.DrawTexture(new Rect(x, y, w, h), barBgTex);
        GUI.Label(new Rect(x, y + 2 * s, w, 20 * s), prefix + p.DisplayName, smallStyle);

        float ratio = p.maxHP > 0 ? Mathf.Clamp01((float)p.hp / p.maxHP) : 0f;
        if (phase == Phase.Menu || phase == Phase.ChooseMon) ratio = 1f;
        int shownHp = (phase == Phase.Menu || phase == Phase.ChooseMon) ? p.maxHP : p.hp;
        var old = GUI.color;
        GUI.color = Color.Lerp(Color.red, Color.green, ratio);
        GUI.DrawTexture(new Rect(x + 8 * s, y + 24 * s, (w - 16 * s) * ratio, 10 * s), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(new Rect(x, y + 36 * s, w, 22 * s), "HP " + shownHp + "/" + p.maxHP + "   攻擊力 " + p.attack, smallStyle);
    }
}
