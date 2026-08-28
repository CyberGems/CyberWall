using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WpfColor = System.Windows.Media.Color;

namespace CyberWall.UI.Services;

public interface IModalAttentionWindow
{
    void TriggerAttention();
}

public static class ModalAttentionHelper
{
    public static void Trigger(
        FrameworkElement window,
        Border outerBorder,
        ScaleTransform windowScale,
        DropShadowEffect windowGlow,
        ref DateTime lastAttentionTime)
    {
        if (!window.Dispatcher.CheckAccess())
        {
            window.Dispatcher.BeginInvoke(() =>
            {
                if (window is IModalAttentionWindow maw)
                {
                    maw.TriggerAttention();
                }
            });
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - lastAttentionTime).TotalMilliseconds < 250)
            return;
        lastAttentionTime = now;

        try
        {
            // 1. Subtle scale pulse (1.0 -> 1.014 -> 1.0)
            var scaleAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(300),
                FillBehavior = FillBehavior.Stop
            };
            scaleAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.014, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90)), new KeySpline(0.2, 0.8, 0.4, 1.0)));
            scaleAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)), new KeySpline(0.4, 0.0, 0.2, 1.0)));

            windowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            windowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            // 2. Soft Accent Glow Flash
            var accentBrush = window.TryFindResource("AccentBrush") as SolidColorBrush;
            if (accentBrush != null)
            {
                windowGlow.Color = accentBrush.Color;
            }

            var glowAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(380),
                FillBehavior = FillBehavior.Stop
            };
            glowAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
            glowAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)), new KeySpline(0.4, 0.0, 0.2, 1.0)));
            windowGlow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnim);

            // 3. Border accent flash
            if (accentBrush != null)
            {
                var originalBrush = window.TryFindResource("BorderBrush") as SolidColorBrush ?? new SolidColorBrush(WpfColor.FromArgb(40, 255, 255, 255));
                var animBrush = new SolidColorBrush(originalBrush.Color);
                outerBorder.BorderBrush = animBrush;

                var borderAnim = new ColorAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(380)
                };
                borderAnim.KeyFrames.Add(new LinearColorKeyFrame(accentBrush.Color, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
                borderAnim.KeyFrames.Add(new SplineColorKeyFrame(originalBrush.Color, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)), new KeySpline(0.4, 0.0, 0.2, 1.0)));

                borderAnim.Completed += (_, _) =>
                {
                    outerBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                };

                animBrush.BeginAnimation(SolidColorBrush.ColorProperty, borderAnim);
            }
        }
        catch { }
    }
}
