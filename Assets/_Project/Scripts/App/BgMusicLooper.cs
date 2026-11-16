using System.Collections.Generic;
using UnityEngine;

namespace SlapMania.App
{
    public class BgMusicLooper : MonoBehaviour
    {
        [Header("Background Music")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private List<AudioClip> clips = new List<AudioClip>();

        private int currentClipIndex = -1;
        private int validClipCount;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;

            validClipCount = CountValidClips();
            if (validClipCount == 0)
            {
                Debug.LogWarning($"{nameof(BgMusicLooper)} on '{name}' has no valid audio clips assigned.", this);
                return;
            }

            audioSource.loop = validClipCount == 1;
            PlayNextClip();
        }

        private void Update()
        {
            if (validClipCount > 1 && audioSource != null && !audioSource.isPlaying)
                PlayNextClip();
        }

        private int CountValidClips()
        {
            if (clips == null)
                return 0;

            int count = 0;
            foreach (AudioClip clip in clips)
            {
                if (clip != null)
                    count++;
            }

            return count;
        }

        private void PlayNextClip()
        {
            if (audioSource == null || clips == null || clips.Count == 0)
                return;

            for (int i = 0; i < clips.Count; i++)
            {
                currentClipIndex = (currentClipIndex + 1) % clips.Count;
                AudioClip clip = clips[currentClipIndex];
                if (clip == null)
                    continue;

                audioSource.clip = clip;
                audioSource.Play();
                return;
            }
        }
    }
}
