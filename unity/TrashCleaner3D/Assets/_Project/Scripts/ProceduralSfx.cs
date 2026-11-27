using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 외부 오디오 파일 없이 코드로 즉석 생성하는 프로토타입 효과음 매니저 (손맛 확보용).
    /// </summary>
    public class ProceduralSfx : MonoBehaviour
    {
        private AudioSource _src;
        private AudioClip _popClip;
        private AudioClip _scrubClip;
        private AudioClip _chimeClip;
        private AudioClip _stealClip;
        private AudioClip _upgradeClip;
        private AudioClip _alertClip;
        private AudioClip _fanfareClip;
        private AudioClip _pourClip;
        private AudioClip _zoneClearClip;
        private AudioClip _bigBurstClip;

        // 도레미파솔: 연속 수거 시 한 칸씩 올라가는 음계 (C 장조 한 옥타브)
        private static readonly int[] ScaleSemitones = { 0, 2, 4, 5, 7, 9, 11, 12 };
        private float _lastNoteTime = -10f;

        private void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.volume = 0.35f;

            _popClip = MakeSweep(0.09f, 260f, 680f, 0.45f);
            _scrubClip = MakeNoiseBurst(0.11f, 0.25f);
            _chimeClip = MakeChord(0.28f, new[] { 587.33f, 880f, 1174.66f }, 0.35f);
            _stealClip = MakeSweep(0.18f, 520f, 310f, 0.4f);
            _upgradeClip = MakeSweep(0.16f, 420f, 920f, 0.45f);
            _alertClip = MakeSweep(0.26f, 820f, 440f, 0.55f);
            _fanfareClip = MakeChord(0.55f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.4f);
            _pourClip = MakePour(0.55f);
            _zoneClearClip = MakeChord(0.34f, new[] { 659.25f, 987.77f, 1318.51f }, 0.26f);
            _bigBurstClip = MakeBigBurst(0.42f);
        }

        // 연쇄 흡입·진공처럼 짧은 간격으로 수십 개가 들어올 때 소리가 겹쳐 찢어지지 않도록 최소 간격을 둔다.
        private float _lastPopTime = -10f;
        public void PlayPop(float pitch = 1f)
        {
            if (Time.time - _lastPopTime < 0.035f) return;
            _lastPopTime = Time.time;
            Play(_popClip, pitch);
        }

        /// <summary>
        /// 연속 수거 '도레미파솔' — step 0(도)부터 7(한 옥타브 위 도)까지 올라간다.
        /// 초당 수십 개를 빨아들이는 진공 단계에서도 기관총이 되지 않도록 최소 간격을 둔다.
        /// </summary>
        public void PlayNote(int step)
        {
            if (Time.time - _lastNoteTime < 0.045f) return;
            _lastNoteTime = Time.time;
            int idx = Mathf.Clamp(step, 0, ScaleSemitones.Length - 1);
            float pitch = 0.72f * Mathf.Pow(2f, ScaleSemitones[idx] / 12f);
            Play(_popClip, Mathf.Clamp(pitch, 0.35f, 2.9f));
        }

        /// <summary>트럭에 봉투를 와르르 쏟을 때의 낮고 잦아드는 잡음.</summary>
        public void PlayPour() { Play(_pourClip, Random.Range(0.94f, 1.06f)); }
        public void PlayScrub() { Play(_scrubClip, Random.Range(0.9f, 1.15f)); }
        public void PlayChime() { Play(_chimeClip, 1f); }
        public void PlaySteal() { Play(_stealClip, 1f); }
        public void PlayUpgrade() { Play(_upgradeClip, 1f); }
        public void PlayAlert() { Play(_alertClip, 1f); }
        public void PlayFanfare() { Play(_fanfareClip, 1f); }

        /// <summary>무더기 구역을 하나 비웠을 때의 가벼운 3화음 (도-솔-높은 미).</summary>
        public void PlayZoneClear() { Play(_zoneClearClip, Random.Range(0.98f, 1.04f)); }

        /// <summary>v1.6 큰 봉투가 터질 때의 경쾌한 "팡!" — 낮은 타격음 + 밝은 파열 잡음.</summary>
        public void PlayBigBurst() { Play(_bigBurstClip, Random.Range(0.96f, 1.06f)); }

        private void Play(AudioClip clip, float pitch)
        {
            if (_src == null || clip == null) return;
            _src.pitch = pitch;
            _src.PlayOneShot(clip);
        }

        private static AudioClip MakeSweep(float duration, float fStart, float fEnd, float amp)
        {
            int sr = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(sr * duration));
            float[] samples = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = Mathf.Lerp(fStart, fEnd, t);
                phase += 2f * Mathf.PI * freq / sr;
                float env = Mathf.Sin(Mathf.PI * t);
                samples[i] = Mathf.Sin(phase) * env * amp;
            }
            var clip = AudioClip.Create("sweep", len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip MakeChord(float duration, float[] freqs, float amp)
        {
            int sr = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(sr * duration));
            float[] samples = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float env = Mathf.Pow(1f - t, 1.6f);
                float v = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    v += Mathf.Sin(2f * Mathf.PI * freqs[k] * (i / (float)sr));
                }
                samples[i] = (v / freqs.Length) * env * amp;
            }
            var clip = AudioClip.Create("chord", len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip MakePour(float duration)
        {
            int sr = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(sr * duration));
            float[] samples = new float[len];
            float smooth = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float env = Mathf.Pow(1f - t, 1.35f);
                float noise = Random.Range(-1f, 1f);
                smooth = Mathf.Lerp(smooth, noise, 0.18f);   // 살짝 뭉갠 자갈 소리
                float rumble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 72f, t) * (i / (float)sr));
                samples[i] = (smooth * 0.85f + rumble * 0.30f) * env * 0.55f;
            }
            var clip = AudioClip.Create("pour", len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip MakeBigBurst(float duration)
        {
            int sr = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(sr * duration));
            float[] samples = new float[len];
            float phase = 0f;
            float noiseLP = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float time = i / (float)sr;

                // 1) 낮게 떨어지는 타격음 (150Hz → 52Hz), 0.16초 안에 빠르게 사라짐
                float thumpEnv = Mathf.Exp(-time * 16f);
                float thumpFreq = Mathf.Lerp(155f, 52f, Mathf.Clamp01(time / 0.16f));
                phase += 2f * Mathf.PI * thumpFreq / sr;
                float thump = Mathf.Sin(phase) * thumpEnv * 0.85f;

                // 2) 밝은 파열 잡음 (아주 짧은 크랙)
                float crackEnv = Mathf.Exp(-time * 42f);
                noiseLP = Mathf.Lerp(noiseLP, Random.Range(-1f, 1f), 0.55f);
                float crack = noiseLP * crackEnv * 0.55f;

                // 3) 반짝이는 고음 (2,100Hz) — '터졌다'는 신호
                float ding = Mathf.Sin(2f * Mathf.PI * 2100f * time) * Mathf.Exp(-time * 26f) * 0.18f;

                float tail = Mathf.Sin(2f * Mathf.PI * 340f * time) * Mathf.Exp(-time * 10f) * 0.14f;
                samples[i] = Mathf.Clamp((thump + crack + ding + tail) * (1f - t * 0.25f), -1f, 1f);
            }
            var clip = AudioClip.Create("bigburst", len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip MakeNoiseBurst(float duration, float amp)
        {
            int sr = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(sr * duration));
            float[] samples = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float env = Mathf.Sin(Mathf.PI * t);
                samples[i] = Random.Range(-1f, 1f) * env * amp;
            }
            var clip = AudioClip.Create("noise", len, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
