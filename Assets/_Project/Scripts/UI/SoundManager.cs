using UnityEngine;
using System.Collections.Generic;

namespace ChoNoi.UI
{
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager instance;
        public static SoundManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("SoundManager");
                    instance = go.AddComponent<SoundManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private AudioSource sfxSource;
        private Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        public void PlaySFX(string name)
        {
            AudioClip clip = GetClip(name);
            if (clip != null)
            {
                sfxSource.PlayOneShot(clip, AudioListener.volume);
            }
        }

        private AudioClip GetClip(string name)
        {
            if (clips.TryGetValue(name, out AudioClip clip))
            {
                return clip;
            }

            string wavName = "";
            switch (name.ToLower())
            {
                case "hover":
                    wavName = "DM-CGS-01";
                    break;
                case "click":
                    wavName = "DM-CGS-02";
                    break;
                case "back":
                case "close":
                    wavName = "DM-CGS-03";
                    break;
                case "pause":
                case "settings":
                    wavName = "DM-CGS-04";
                    break;
                case "cash":
                case "trade":
                case "success":
                    wavName = "DM-CGS-05";
                    break;
                case "error":
                    wavName = "DM-CGS-21";
                    break;
                default:
                    wavName = name;
                    break;
            }

            clip = Resources.Load<AudioClip>("Audio/" + wavName);
            if (clip == null)
            {
                clip = Resources.Load<AudioClip>(wavName);
            }
            
            if (clip != null)
            {
                clips[name] = clip;
            }
            else
            {
                Debug.LogWarning($"[SoundManager] Could not load audio clip: {name} (mapped to {wavName})");
            }
            
            return clip;
        }
    }
}
