using System;
using System.IO;
using System.Media;

namespace CrosshairRoulette
{
    // Roulette ticks and the final "clack", synthesized into in-memory WAVs.
    sealed class Sound
    {
        const int Rate = 44100;
        public bool Enabled = true;
        double volume = -1;
        byte[][] ticks;
        byte[] land;
        readonly Random rnd = new Random();
        readonly SoundPlayer player = new SoundPlayer();

        public void SetVolume(double v)
        {
            if (Math.Abs(v - volume) < 0.001) return;
            volume = v;
            ticks = new byte[5][];
            for (int i = 0; i < ticks.Length; i++) ticks[i] = Wav(Tick(i / 4.0), v);
            land = Wav(Land(), v);
        }

        public void PlayTick(double p) => Play(ticks?[(int)Math.Round(Math.Max(0, Math.Min(1, p)) * 4)]);
        public void PlayLand() => Play(land);

        void Play(byte[] wav)
        {
            if (!Enabled || volume <= 0 || wav == null) return;
            try
            {
                player.Stream = new MemoryStream(wav);
                player.Play(); // async; a new sound simply replaces the last tick
            }
            catch { }
        }

        float[] Tick(double p)
        {
            var s = new float[(int)(Rate * 0.035)];
            double f = 1200 + p * 700;
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate;
                double env = Math.Exp(-t * 140);
                double noise = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t * 260);
                double square = Math.Sign(Math.Sin(2 * Math.PI * f * t)) * 0.25;
                s[i] = (float)((noise * 0.6 + square) * env);
            }
            return s;
        }

        float[] Land()
        {
            var s = new float[(int)(Rate * 0.55)];
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate;
                double f = 170 * Math.Pow(60.0 / 170, Math.Min(1, t / 0.14));
                phase += 2 * Math.PI * f / Rate;
                double thump = Math.Sin(phase) * Math.Exp(-t * 22) * 0.8;
                double click = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t * 90) * 0.5;
                double bell = t > 0.05 ? (Math.Sin(2 * Math.PI * 1568 * (t - 0.05)) * 0.22 + Math.Sin(2 * Math.PI * 2093 * (t - 0.05)) * 0.14) * Math.Exp(-(t - 0.05) * 7) : 0;
                s[i] = (float)(thump + click + bell);
            }
            return s;
        }

        static byte[] Wav(float[] samples, double volume)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int dataLen = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataLen);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataLen);
                foreach (var x in samples) w.Write((short)(Math.Max(-1, Math.Min(1, x * volume)) * 32000));
                return ms.ToArray();
            }
        }
    }
}
