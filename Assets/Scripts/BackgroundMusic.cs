using UnityEngine;

// 掛在任何物件上(例如空物件 BGM)。把音樂拖進 Music 欄位,按 Play 就會循環播放。
// 不用自己加 Audio Source,腳本會自動處理。
public class BackgroundMusic : MonoBehaviour
{
    [Tooltip("背景音樂(mp3 / ogg / wav)")]
    public AudioClip music;
    [Range(0f, 1f)]
    [Tooltip("音量,0.3 不會蓋掉丟球和收服音效")]
    public float volume = 0.3f;

    void Start()
    {
        if (music == null)
        {
            Debug.LogWarning("[BackgroundMusic] Music 欄位是空的,請把音樂拖進去");
            return;
        }

        AudioSource src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.clip = music;
        src.loop = true;
        src.volume = volume;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D 音效,不會因為距離變小聲
        src.Play();
    }
}
