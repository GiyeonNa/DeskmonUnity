using System.Collections.Generic;
using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 합성 효과음. audio.js 이식 - 원본이 Web Audio로 즉석 합성했듯이
    /// 여기서도 에셋 없이 코드로 클립을 만든다 (포팅계획 §6의 "소형 wav 대체"보다
    /// 원본 정체성에 가깝고, 오디오 파일 관리 자체가 사라진다).
    ///
    /// 원본 tone()의 구조를 그대로 옮겼다:
    ///   진동자(sine/triangle/square) + 지수 주파수 슬라이드
    ///   + 지수 엔벨로프 (12ms 어택 -> 지속시간 동안 감쇠)
    /// 효과음 하나 = tone 1~3개의 시차 합성. 수치는 audio.js와 동일.
    ///
    /// 클립은 처음 쓸 때 한 번 베이크해 캐시한다 - 가장 긴 것이 0.5초 미만이라
    /// 베이크 비용은 체감되지 않는다.
    /// </summary>
    public static class Sfx
    {
        const int SAMPLE_RATE = 44100;

        enum Wave { Sine, Triangle, Square }

        struct Tone
        {
            public float freq, dur, vol, delay, slide;   // slide 0 = 슬라이드 없음
            public Wave wave;

            public Tone(float freq, float dur, Wave wave, float vol,
                        float delay = 0f, float slide = 0f)
            {
                this.freq = freq; this.dur = dur; this.wave = wave;
                this.vol = vol; this.delay = delay; this.slide = slide;
            }
        }

        static readonly Dictionary<string, AudioClip> _cache
            = new Dictionary<string, AudioClip>();
        static AudioSource _source;

        // ── 공개 API - audio.js의 SFX.*와 1:1 ──

        /// <summary>포획 성공. 상승 스윕 + 높은 종결음.</summary>
        public static void CatchIt() => Play("catchIt", new[]
        {
            new Tone(420f, 0.12f, Wave.Triangle, 0.16f, 0f, 880f),
            new Tone(1250f, 0.09f, Wave.Sine, 0.10f, 0.07f),
        });

        /// <summary>각인 시작 / 공 던지기. 짧은 저음 틱.</summary>
        public static void Grab() => Play("grab", new[]
        {
            new Tone(310f, 0.05f, Wave.Square, 0.045f),
        });

        /// <summary>판정 실패. 하강음.</summary>
        public static void Escape() => Play("escape", new[]
        {
            new Tone(520f, 0.14f, Wave.Triangle, 0.09f, 0f, 250f),
        });

        /// <summary>쓰다듬기 / 문양 성공. 밝은 상승음.</summary>
        public static void Pet() => Play("pet", new[]
        {
            new Tone(900f, 0.08f, Wave.Sine, 0.10f, 0f, 1150f),
        });

        /// <summary>샤이니 / 전설 등장. 3음 아르페지오.</summary>
        public static void Shiny() => Play("shiny", new[]
        {
            new Tone(880f, 0.10f, Wave.Sine, 0.09f),
            new Tone(1108f, 0.10f, Wave.Sine, 0.09f, 0.09f),
            new Tone(1318f, 0.18f, Wave.Sine, 0.11f, 0.18f),
        });

        /// <summary>진화. 장3화음 상승.</summary>
        public static void Evolve() => Play("evolve", new[]
        {
            new Tone(523f, 0.12f, Wave.Triangle, 0.10f),
            new Tone(659f, 0.12f, Wave.Triangle, 0.10f, 0.10f),
            new Tone(784f, 0.22f, Wave.Triangle, 0.12f, 0.20f),
        });

        /// <summary>야생 출현. 짧은 상승 알림.</summary>
        public static void Appear() => Play("appear", new[]
        {
            new Tone(640f, 0.09f, Wave.Sine, 0.06f, 0f, 780f),
        });

        /// <summary>해금 / 마일스톤. 옥타브 팡파르.</summary>
        public static void Unlock() => Play("unlock", new[]
        {
            new Tone(523f, 0.10f, Wave.Triangle, 0.10f),
            new Tone(784f, 0.10f, Wave.Triangle, 0.10f, 0.10f),
            new Tone(1046f, 0.24f, Wave.Triangle, 0.12f, 0.20f),
        });

        // ── 합성 ──

        static void Play(string name, Tone[] tones)
        {
            EnsureSource();
            if (_source == null) return;

            if (!_cache.TryGetValue(name, out var clip))
            {
                clip = Bake(name, tones);
                _cache[name] = clip;
            }
            if (clip != null) _source.PlayOneShot(clip);
        }

        static void EnsureSource()
        {
            if (_source != null) return;

            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;      // 데스크탑 오버레이에 3D 음향은 의미가 없다
            _source.playOnAwake = false;
        }

        static AudioClip Bake(string name, Tone[] tones)
        {
            float length = 0f;
            foreach (var t in tones) length = Mathf.Max(length, t.delay + t.dur);
            length += 0.05f;   // 꼬리 여유 (원본의 stop 시점과 같다)

            int samples = Mathf.CeilToInt(length * SAMPLE_RATE);
            var data = new float[samples];

            foreach (var t in tones) Render(data, t);

            var clip = AudioClip.Create("sfx_" + name, samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>tone 하나를 버퍼에 가산 합성. audio.js tone()의 재현.</summary>
        static void Render(float[] data, Tone t)
        {
            int start = Mathf.FloorToInt(t.delay * SAMPLE_RATE);
            int count = Mathf.FloorToInt(t.dur * SAMPLE_RATE);
            const float ATTACK = 0.012f;
            const float FLOOR = 0.0001f;

            // 지수 슬라이드는 위상을 샘플마다 적분한다 - 닫힌식보다 단순하고
            // 0.2초짜리 소리에서 오차가 들릴 일이 없다.
            double phase = 0.0;
            float slideRatio = t.slide > 0f ? t.slide / t.freq : 1f;

            for (int i = 0; i < count; i++)
            {
                int at = start + i;
                if (at >= data.Length) break;

                float p = i / (float)count;                        // 0..1 진행
                float freq = t.freq * Mathf.Pow(slideRatio, p);    // 지수 슬라이드
                phase += 2.0 * System.Math.PI * freq / SAMPLE_RATE;

                float sec = i / (float)SAMPLE_RATE;
                float env = sec < ATTACK
                    ? FLOOR * Mathf.Pow(t.vol / FLOOR, sec / ATTACK)              // 지수 어택
                    : t.vol * Mathf.Pow(FLOOR / t.vol,
                        (sec - ATTACK) / Mathf.Max(0.001f, t.dur - ATTACK));      // 지수 감쇠

                float s = Mathf.Sin((float)phase);
                float wave = t.wave == Wave.Sine ? s
                           : t.wave == Wave.Triangle ? Mathf.Asin(s) * (2f / Mathf.PI)
                           : Mathf.Sign(s);

                data[at] += wave * env;
            }
        }
    }
}
