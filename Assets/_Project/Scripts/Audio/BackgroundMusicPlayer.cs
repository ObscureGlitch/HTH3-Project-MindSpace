using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastWatch.Audio
{
    public sealed class ShuffleBag<T>
    {
        private readonly T[] _items;
        private readonly List<T> _remaining;
        private readonly System.Random _random;
        private bool _hasLastDrawn;
        private T _lastDrawn;

        public ShuffleBag(IEnumerable<T> items)
            : this(items, new System.Random())
        {
        }

        public ShuffleBag(IEnumerable<T> items, int seed)
            : this(items, new System.Random(seed))
        {
        }

        private ShuffleBag(IEnumerable<T> items, System.Random random)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            _items = new List<T>(items).ToArray();
            if (_items.Length == 0)
            {
                throw new ArgumentException("A shuffle bag needs at least one item.", nameof(items));
            }

            _remaining = new List<T>(_items.Length);
            _random = random;
        }

        public T Draw()
        {
            if (_remaining.Count == 0)
            {
                Refill();
            }

            T next = _remaining[0];
            _remaining.RemoveAt(0);
            _lastDrawn = next;
            _hasLastDrawn = true;
            return next;
        }

        private void Refill()
        {
            _remaining.AddRange(_items);
            for (int index = _remaining.Count - 1; index > 0; index--)
            {
                int swapIndex = _random.Next(index + 1);
                (_remaining[index], _remaining[swapIndex]) = (_remaining[swapIndex], _remaining[index]);
            }

            if (!_hasLastDrawn || _remaining.Count < 2 ||
                !EqualityComparer<T>.Default.Equals(_remaining[0], _lastDrawn))
            {
                return;
            }

            int replacementIndex = -1;
            int candidatesSeen = 0;
            for (int index = 1; index < _remaining.Count; index++)
            {
                if (EqualityComparer<T>.Default.Equals(_remaining[index], _lastDrawn))
                {
                    continue;
                }

                candidatesSeen++;
                if (_random.Next(candidatesSeen) == 0)
                {
                    replacementIndex = index;
                }
            }

            if (replacementIndex >= 0)
            {
                (_remaining[0], _remaining[replacementIndex]) =
                    (_remaining[replacementIndex], _remaining[0]);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class BackgroundMusicPlayer : MonoBehaviour
    {
        private const string ResourcePath = "TheLastWatch/BackgroundMusic";
        private static BackgroundMusicPlayer _instance;

        [SerializeField, Range(0f, 1f)] private float volume = .10f;

        private AudioSource _source;
        private ShuffleBag<AudioClip> _playlist;
        private bool _started;
        private double _expectedEndTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsurePlayerExists()
        {
            if (_instance != null)
            {
                return;
            }

            new GameObject("Background Music").AddComponent<BackgroundMusicPlayer>();
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

            AudioClip[] tracks = Resources.LoadAll<AudioClip>(ResourcePath);
            Array.Sort(tracks, (left, right) =>
                StringComparer.Ordinal.Compare(left.name, right.name));

            if (tracks.Length == 0)
            {
                Debug.LogWarning($"No background music was found in Resources/{ResourcePath}.", this);
                enabled = false;
                Destroy(gameObject);
                return;
            }

            _playlist = new ShuffleBag<AudioClip>(tracks);
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.volume = volume;
            _source.priority = 200;
        }

        private void Start()
        {
            PlayNext();
        }

        private void Update()
        {
            if (!_started || _source == null || _source.isPlaying || AudioListener.pause)
            {
                return;
            }

            if (AudioSettings.dspTime >= _expectedEndTime - .05d)
            {
                PlayNext();
            }
        }

        private void PlayNext()
        {
            AudioClip next = _playlist.Draw();
            _source.clip = next;
            _source.Play();
            _started = true;
            _expectedEndTime = AudioSettings.dspTime + next.length;
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
