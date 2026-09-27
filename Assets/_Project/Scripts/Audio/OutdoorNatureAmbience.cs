using UnityEngine;

namespace TheLastWatch.Audio
{
    [DisallowMultipleComponent]
    public sealed class OutdoorNatureAmbience : MonoBehaviour
    {
        private const string DayResourcePath = "TheLastWatch/OutdoorAmbience/nature-ambience-323729";
        private const string NightResourcePath = "TheLastWatch/OutdoorAmbience/night-ambience-17064";
        private static OutdoorNatureAmbience _instance;

        [SerializeField, Range(0f, 1f)] private float dayVolume = .16f;
        [SerializeField, Range(0f, 1f)] private float nightVolume = .16f;
        [SerializeField, Min(.01f)] private float fadeSpeed = .35f;

        private AudioSource _daySource;
        private AudioSource _nightSource;
        private Camera _listenerCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsurePlayerExists()
        {
            if (_instance == null)
            {
                new GameObject("Outdoor Nature Ambience").AddComponent<OutdoorNatureAmbience>();
            }
        }

        public static float OutdoorBlend(Vector3 position)
        {
            const float indoorHalfWidth = 3.6f;
            const float indoorHalfDepth = 3.1f;
            const float transitionDistance = 1.3f;
            const float doorwayLeadIn = .15f;

            float distanceBeyondRoom = Mathf.Max(
                Mathf.Abs(position.x) - indoorHalfWidth,
                Mathf.Abs(position.z) - indoorHalfDepth);
            float transition = Mathf.Clamp01(
                (distanceBeyondRoom + doorwayLeadIn) / transitionDistance);
            return Mathf.SmoothStep(0f, 1f, transition);
        }

        public static float DayBlend(float nightAmount)
        {
            return 1f - Mathf.Clamp01(nightAmount);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _daySource = CreateSource(DayResourcePath, "daytime");
            _nightSource = CreateSource(NightResourcePath, "nighttime");
            if (_daySource == null && _nightSource == null)
            {
                enabled = false;
                Destroy(gameObject);
            }
        }

        private AudioSource CreateSource(string resourcePath, string period)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"No {period} outdoor ambience was found at Resources/{resourcePath}.", this);
                return null;
            }

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 190;
            return source;
        }

        private void Start()
        {
            if (_daySource != null)
            {
                _daySource.Play();
            }

            if (_nightSource != null)
            {
                _nightSource.Play();
            }
        }

        private void Update()
        {
            if (_listenerCamera == null)
            {
                _listenerCamera = Camera.main;
            }

            float outdoors = _listenerCamera == null
                ? 0f
                : OutdoorBlend(_listenerCamera.transform.position);
            float nightAmount = RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Night")
                ? RenderSettings.skybox.GetFloat("_Night")
                : 0f;
            float day = DayBlend(nightAmount);

            UpdateSource(_daySource, dayVolume * outdoors * day);
            UpdateSource(_nightSource, nightVolume * outdoors * (1f - day));
        }

        private void UpdateSource(AudioSource source, float targetVolume)
        {
            if (source == null)
            {
                return;
            }

            source.volume = Mathf.MoveTowards(
                source.volume,
                targetVolume,
                fadeSpeed * Time.unscaledDeltaTime);

            if (!source.isPlaying && !AudioListener.pause)
            {
                source.Play();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
