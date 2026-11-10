using UnityEngine;

/// <summary>
/// Toca efeitos sonoros e música de fundo. Coloque um em cada cena.
/// Qualquer script pode chamar AudioManager.Play(clip) sem precisar de referência.
/// Se o clip for nulo (som ainda não definido), nada acontece — o jogo não quebra.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip music;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;
    [Tooltip("Pequena variação aleatória de tom para os sons não ficarem repetitivos.")]
    [SerializeField] private float pitchVariation = 0.08f;

    private void Awake()
    {
        instance = this;

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        if (music != null)
        {
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = music;
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null || instance == null) return;
        instance.sfxSource.pitch = 1f + Random.Range(-instance.pitchVariation, instance.pitchVariation);
        instance.sfxSource.PlayOneShot(clip, volume);
    }
}
