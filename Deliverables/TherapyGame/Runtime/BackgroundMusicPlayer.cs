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
        private WellnessPlaylist _playlist;
        private bool _pausedTrackChanged;
        private bool _started;
        private double _expectedEndTime;
        private bool _userPaused;
        private AudioClip[] _tracks;
        public static BackgroundMusicPlayer Instance => _instance;
        public bool IsPaused => _userPaused;
        public bool IsPlaying => _source!=null&&_source.isPlaying;
        public float Elapsed => _source!=null&&_source.clip!=null?_source.time:0;
        public float Duration => _source!=null&&_source.clip!=null?_source.clip.length:0;
        public int TrackCount => _tracks!=null?_tracks.Length:0;
        public int TrackNumber => _source!=null&&_tracks!=null?Array.IndexOf(_tracks,_source.clip)+1:0;
        public string TrackTitle => _source!=null&&_source.clip!=null?CleanTitle(_source.clip.name):"Quiet room";
        public float Volume {get=>volume;set{volume=Mathf.Clamp01(value);if(_source!=null)_source.volume=volume;}}
        public void TogglePause()
        {
            if(_source==null||!_started)return;
            _userPaused=!_userPaused;
            if(_userPaused)_source.Pause();
            else
            {
                _expectedEndTime=AudioSettings.dspTime+Mathf.Max(0,Duration-Elapsed);
                if(_pausedTrackChanged){_source.Play();_pausedTrackChanged=false;}else _source.UnPause();
            }
        }
        public static string CleanTitle(string filename)
        {
            string title=filename.Replace("alex-morgan-","").Replace("andriig-soft-","").Replace("andriih-soft-","").Replace("apalonbeats-","");
            int dash=title.LastIndexOf('-');if(dash>=0&&int.TryParse(title.Substring(dash+1),out _))title=title.Substring(0,dash);
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title.Replace('-',' '));
        }

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

            _playlist = new WellnessPlaylist(tracks.Length,System.Environment.TickCount);
            _tracks=tracks;
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
            if (!_started || _userPaused || _source == null || _source.isPlaying || AudioListener.pause)
            {
                return;
            }

            if (AudioSettings.dspTime >= _expectedEndTime - .05d)
            {
                PlayNext();
            }
        }

        public void PlayNext()=>Skip(1);
        public void PlayPrevious()=>Skip(-1);
        private void Skip(int direction)
        {
            if(_playlist==null||_source==null||_tracks==null)return;
            int index=_playlist.Move(direction);if(index<0)return;
            AudioClip next = _tracks[index];
            _source.Stop();
            _source.clip = next;
            _source.time=0;
            _pausedTrackChanged=_userPaused;
            if(!_userPaused)_source.Play();
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
