using System;
using System.IO;
using UnityEngine;

namespace Deprem.Audio
{
    /// <summary>
    /// Kodla ses üretimi (hazır ses dosyası yokken; lisans ve beyan derdi yok). 44,1 kHz mono, 16 bit WAV.
    /// Gürültü, filtre, zarf, basit yankı yardımcıları; seslerin tarifleri AudioSetup'ta.
    /// Her üretim sabit tohumla yapılır: aynı kod aynı dosyayı verir, her kurulumda git farkı çıkmaz.
    /// </summary>
    internal sealed class SoundSynth
    {
        public const int Rate = 44100;
        private System.Random random;

        public SoundSynth(int seed) => random = new System.Random(seed);

        public float Rand() => (float)random.NextDouble();
        public float Range(float a, float b) => a + (b - a) * Rand();
        public float Noise() => Rand() * 2f - 1f;

        public static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];
        public static int Samples(float seconds) => Mathf.RoundToInt(seconds * Rate);

        // ------------------------------------------------------------------ Kaynaklar

        public float[] White(float seconds)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++) b[i] = Noise();
            return b;
        }

        /// <summary>Kahverengi gürültü (derin uğultu): beyaz gürültünün sızdıran toplamı</summary>
        public float[] Brown(float seconds)
        {
            var b = Buffer(seconds);
            float v = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                v = (v + Noise() * 0.02f) * 0.998f;
                b[i] = v * 6f;
            }
            return b;
        }

        /// <summary>Frekansı zamanla değişen sinüs (Hz fonksiyonu t saniyeye göre)</summary>
        public static float[] Sine(float seconds, Func<float, float> hz, float phase = 0f)
        {
            var b = Buffer(seconds);
            double p = phase;
            for (int i = 0; i < b.Length; i++)
            {
                p += 2.0 * Math.PI * hz(i / (float)Rate) / Rate;
                b[i] = (float)Math.Sin(p);
            }
            return b;
        }

        /// <summary>Çan / zil: uyumsuz kısmilerden, her biri kendi hızında söner</summary>
        public static float[] Bell(float seconds, float baseHz, float[] ratios, float[] gains, float decay)
        {
            var b = Buffer(seconds);
            for (int k = 0; k < ratios.Length; k++)
            {
                double w = 2.0 * Math.PI * baseHz * ratios[k] / Rate;
                float d = decay * (1f + k * 0.6f);
                for (int i = 0; i < b.Length; i++)
                    b[i] += gains[k] * (float)Math.Sin(w * i) * Mathf.Exp(-d * i / (float)Rate);
            }
            return b;
        }

        /// <summary>Yumuşak piyano / pad notası: harmonikler, yavaş atak, üstel sönüm</summary>
        public static float[] Note(float seconds, float hz, float attack, float decay, float brightness = 0.5f)
        {
            var b = Buffer(seconds);
            for (int h = 1; h <= 6; h++)
            {
                float gain = Mathf.Pow(brightness, h - 1) / h;
                double w = 2.0 * Math.PI * hz * h * (1f + 0.0007f * h * h) / Rate;
                float d = decay * (1f + 0.5f * (h - 1));
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / Mathf.Max(0.001f, attack)) * Mathf.Exp(-d * t);
                    b[i] += gain * env * (float)Math.Sin(w * i);
                }
            }
            return b;
        }

        // ------------------------------------------------------------------ Filtreler (yerinde)

        public static float[] LowPass(float[] b, float hz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate);
            float y = 0f;
            for (int i = 0; i < b.Length; i++) b[i] = y += a * (b[i] - y);
            return b;
        }

        public static float[] HighPass(float[] b, float hz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate);
            float low = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                low += a * (b[i] - low);
                b[i] -= low;
            }
            return b;
        }

        /// <summary>Rezonanslı bant geçiren (biquad, sabit frekans)</summary>
        public static float[] BandPass(float[] b, float hz, float q) => BandPass(b, _ => hz, q);

        /// <summary>Frekansı zamanla kayan bant geçiren (gıcırtı, süpürme)</summary>
        public static float[] BandPass(float[] b, Func<float, float> hz, float q)
        {
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float w0 = 2f * Mathf.PI * Mathf.Clamp(hz(i / (float)Rate), 20f, Rate * 0.45f) / Rate;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float a0 = 1f + alpha;
                float b0 = alpha / a0, b2 = -alpha / a0;
                float a1 = -2f * Mathf.Cos(w0) / a0, a2 = (1f - alpha) / a0;
                float x = b[i];
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                b[i] = y;
            }
            return b;
        }

        // ------------------------------------------------------------------ Zarf ve karışım

        /// <summary>Atak, tutma, üstel sönüm</summary>
        public static float[] Envelope(float[] b, float attack, float hold, float decayPerSecond)
        {
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = t < attack ? t / Mathf.Max(0.0001f, attack)
                    : t < attack + hold ? 1f
                    : Mathf.Exp(-decayPerSecond * (t - attack - hold));
                b[i] *= env;
            }
            return b;
        }

        /// <summary>Zamana bağlı kazanç</summary>
        public static float[] Shape(float[] b, Func<float, float> gain)
        {
            for (int i = 0; i < b.Length; i++) b[i] *= gain(i / (float)Rate);
            return b;
        }

        /// <summary>Sonu ve başı kısa yumuşatma: tık sesi olmasın</summary>
        public static float[] Fade(float[] b, float fadeIn = 0.003f, float fadeOut = 0.01f)
        {
            int nIn = Mathf.Min(b.Length, Samples(fadeIn)), nOut = Mathf.Min(b.Length, Samples(fadeOut));
            for (int i = 0; i < nIn; i++) b[i] *= i / (float)nIn;
            for (int i = 0; i < nOut; i++) b[b.Length - 1 - i] *= i / (float)nOut;
            return b;
        }

        public static float[] Scale(float[] b, float gain)
        {
            for (int i = 0; i < b.Length; i++) b[i] *= gain;
            return b;
        }

        /// <summary>src'yi dst'ye saniye kaydırmasıyla ekler (taşan kısım kesilir)</summary>
        public static float[] Mix(float[] dst, float[] src, float atSeconds = 0f, float gain = 1f)
        {
            int offset = Samples(atSeconds);
            for (int i = 0; i < src.Length && offset + i < dst.Length; i++)
                if (offset + i >= 0) dst[offset + i] += src[i] * gain;
            return dst;
        }

        /// <summary>Tepe değeri verilen seviyeye getirir</summary>
        public static float[] Normalize(float[] b, float peak = 0.9f)
        {
            float max = 0f;
            foreach (var v in b) max = Mathf.Max(max, Mathf.Abs(v));
            return max > 0f ? Scale(b, peak / max) : b;
        }

        /// <summary>Yumuşak doyum: patlamasın, gövdeli dursun</summary>
        public static float[] Saturate(float[] b, float drive)
        {
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * drive) / norm;
            return b;
        }

        /// <summary>
        /// Döngüye girecek ses: sondaki crossfade saniyesi başa bindirilir, ses o kadar kısalır; döngü noktasında tık
        /// ve boşluk olmaz.
        /// </summary>
        public static float[] MakeLoop(float[] b, float crossfade)
        {
            int n = Mathf.Min(Samples(crossfade), b.Length / 2);
            var loop = new float[b.Length - n];
            Array.Copy(b, loop, loop.Length);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                // Eşit güçte geçiş
                loop[i] = loop[i] * Mathf.Sin(t * Mathf.PI * 0.5f) + b[loop.Length + i] * Mathf.Cos(t * Mathf.PI * 0.5f);
            }
            return loop;
        }

        /// <summary>Basit Schroeder yankısı (4 tarak + 2 tüm geçiren). wet 0..1</summary>
        public static float[] Reverb(float[] dry, float seconds, float wet, float roomSize = 0.8f)
        {
            int[] combs = { 1557, 1617, 1491, 1422 };
            int[] allpasses = { 225, 556 };
            var outBuf = new float[dry.Length + Samples(seconds)];
            var wetBuf = new float[outBuf.Length];
            foreach (int delay in combs)
            {
                var line = new float[delay];
                int idx = 0;
                float filter = 0f;
                for (int i = 0; i < outBuf.Length; i++)
                {
                    float input = i < dry.Length ? dry[i] : 0f;
                    float y = line[idx];
                    filter = y * 0.75f + filter * 0.25f; // yüksekleri söndür
                    line[idx] = input + filter * roomSize;
                    idx = (idx + 1) % delay;
                    wetBuf[i] += y * 0.25f;
                }
            }
            foreach (int delay in allpasses)
            {
                var line = new float[delay];
                int idx = 0;
                for (int i = 0; i < wetBuf.Length; i++)
                {
                    float buffered = line[idx];
                    float input = wetBuf[i];
                    line[idx] = input + buffered * 0.5f;
                    wetBuf[i] = buffered - input;
                    idx = (idx + 1) % delay;
                }
            }
            for (int i = 0; i < outBuf.Length; i++)
                outBuf[i] = (i < dry.Length ? dry[i] * (1f - wet * 0.5f) : 0f) + wetBuf[i] * wet;
            // Kuyruğun sonu yumuşak bitsin
            return Fade(outBuf, 0f, Mathf.Min(seconds, 0.5f));
        }

        // ------------------------------------------------------------------ WAV

        public static void WriteWav(string path, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1); // PCM
            w.Write((short)1); // mono
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples)
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}
