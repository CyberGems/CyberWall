using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;

namespace CyberWall.UI.Services;

public static class PromptSoundService
{
    private static readonly byte[] DefaultChimeWav = GenerateDefaultChime();
    private static DateTime _lastPlayTime = DateTime.MinValue;
    private static readonly object Lock = new();
    private static MediaPlayer? _mediaPlayer;

    public static void PlayPromptSound()
    {
        if (!App.Settings.PlaySoundOnPrompt)
            return;

        lock (Lock)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastPlayTime).TotalMilliseconds < 350)
                return;
            _lastPlayTime = now;
        }

        PlayInternal(App.Settings.CustomSoundPath);
    }

    public static void PreviewSound(string? customPath = null)
    {
        PlayInternal(customPath);
    }

    private static void PlayInternal(string? customPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            {
                var ext = Path.GetExtension(customPath).ToLowerInvariant();
                if (ext == ".wav")
                {
                    using var player = new SoundPlayer(customPath);
                    player.Play();
                    return;
                }

                // MP3 or other media supported by WPF MediaPlayer
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _mediaPlayer ??= new MediaPlayer();
                        _mediaPlayer.Open(new Uri(customPath, UriKind.Absolute));
                        _mediaPlayer.Play();
                    }
                    catch { }
                });
                return;
            }

            // Default generated soft chime
            using var ms = new MemoryStream(DefaultChimeWav);
            using var defaultPlayer = new SoundPlayer(ms);
            defaultPlayer.Play();
        }
        catch { }
    }

    private static byte[] GenerateDefaultChime()
    {
        const int sampleRate = 44100;
        const double duration = 0.35; // 350ms
        int numSamples = (int)(sampleRate * duration);
        var pcm = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;

            // Attack: 15ms gentle linear fade in
            double attack = Math.Min(1.0, t / 0.015);
            // Decay: smooth exponential decay
            double decay = Math.Exp(-7.5 * t);
            double envelope = attack * decay;

            // Harmonious soft chime (C6: 1046.5 Hz, G6: 1567.98 Hz, C5: 523.25 Hz)
            double sample = 0.55 * Math.Sin(2 * Math.PI * 1046.5 * t)
                          + 0.30 * Math.Sin(2 * Math.PI * 1567.98 * t)
                          + 0.15 * Math.Sin(2 * Math.PI * 523.25 * t);

            pcm[i] = (short)(sample * envelope * 22000);
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF chunk descriptor
        writer.Write("RIFF"u8);
        writer.Write(36 + numSamples * 2);
        writer.Write("WAVE"u8);

        // "fmt " sub-chunk
        writer.Write("fmt "u8);
        writer.Write(16); // PCM header size
        writer.Write((short)1); // 1 = PCM format
        writer.Write((short)1); // Mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // Byte rate (44100 * 2)
        writer.Write((short)2); // Block align
        writer.Write((short)16); // 16 bits

        // "data" sub-chunk
        writer.Write("data"u8);
        writer.Write(numSamples * 2);
        foreach (var s in pcm)
        {
            writer.Write(s);
        }

        return ms.ToArray();
    }
}
