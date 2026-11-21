using UnityEngine;

// 不用掛在任何物件上!用程式直接產生 ParticleSystem 粒子特效(不需要做 Prefab)。
// Built-in 和 URP 專案都能顯示。
public static class CaptureEffect
{
    static Material material;

    static Material GetMaterial()
    {
        if (material != null) return material;

        // 程式畫一張「中間亮、外面透明」的圓點貼圖
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float dy = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        material = new Material(sh);
        material.mainTexture = tex;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
        return material;
    }

    // 通用:在 pos 爆出一團粒子
    public static ParticleSystem Burst(Vector3 pos, Color colorA, Color colorB, int count,
        float speed, float size, float lifetime, float gravity, float shapeRadius)
    {
        var go = new GameObject("CaptureFX");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(10, count * 2);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.001f, shapeRadius);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = GetMaterial();

        ps.Play();
        Object.Destroy(go, lifetime + 1f);
        return ps;
    }

    // 球打中寶可夢時的白色閃光 + 彩色光點(scale = 寶可夢身高)
    public static void CaptureFlash(Vector3 pos, Color color, float scale)
    {
        Burst(pos, Color.white, new Color(1f, 1f, 1f, 0.8f), 1, 0f, scale * 2.5f, 0.35f, 0f, 0.01f); // 大閃光
        Burst(pos, Color.white, color, 40, scale * 3f, scale * 0.25f, 0.8f, 0f, scale * 0.3f);
        Burst(pos, new Color(1f, 0.3f, 0.3f), Color.white, 25, scale * 1.5f, scale * 0.15f, 1.0f, -0.2f, scale * 0.5f);
    }

    // 收服成功的星星
    public static void Success(Vector3 pos, Color color, float scale)
    {
        Burst(pos, Color.yellow, color, 60, scale * 3f, scale * 0.2f, 1.4f, 0.6f, scale * 0.1f);
        Burst(pos, Color.white, new Color(1f, 1f, 0.6f), 30, scale * 1.5f, scale * 0.12f, 1.8f, -0.3f, scale * 0.2f);
        Burst(pos, Color.white, Color.white, 1, 0f, scale * 1.5f, 0.3f, 0f, 0.01f);
    }

    // 小煙霧(球落地、搖晃、消失時)
    public static void Puff(Vector3 pos, Color color, float size)
    {
        Burst(pos, color, new Color(color.r, color.g, color.b, 0.5f), 15, size * 1.5f, size * 0.4f, 0.6f, 0f, size * 0.2f);
    }
}
