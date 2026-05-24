using Microsoft.Maui.Graphics;
using SynthSharp.Core.Audio;

namespace SynthSharp.App;

/// <summary>
/// Draws a sample's waveform with overlay markers for the configured trim and loop regions.
/// Use with <see cref="GraphicsView"/> in the pad editor.
/// </summary>
public sealed class SampleWaveformDrawable : IDrawable
{
    // Resolved at draw time; set by MainPage before calling Invalidate().
    /// <summary>The sample to visualise; null renders an "No sample loaded" placeholder.</summary>
    public Sample? Sample { get; set; }

    /// <summary>First frame of the trimmed playback region (inclusive).</summary>
    public int TrimStartFrame { get; set; }

    /// <summary>Last frame of the trimmed playback region (exclusive); 0 means "to end of sample".</summary>
    public int TrimEndFrame { get; set; }

    /// <summary>Whether sample looping is enabled.</summary>
    public bool LoopEnabled { get; set; }

    /// <summary>Loop start in trimmed-region frames (relative to TrimStartFrame).</summary>
    public int LoopStartFrame { get; set; }

    /// <summary>Loop end in trimmed-region frames (relative to TrimStartFrame); 0 means "to end of trimmed region".</summary>
    public int LoopEndFrame { get; set; }

    private static readonly Color BackgroundFill = Color.FromArgb("#F0F0F0");
    private static readonly Color CentreLineColor = Color.FromArgb("#C0C0C0");
    private static readonly Color WaveformFill = Color.FromArgb("#555555");
    private static readonly Color TrimShading = Color.FromArgb("#FFE5B4"); // light peach
    private static readonly Color TrimLine = Color.FromArgb("#FF8C00");    // dark orange
    private static readonly Color LoopShading = Color.FromArgb("#5096FFFF"); // ~31% alpha blue
    private static readonly Color LoopLine = Color.FromArgb("#1E66FF");    // strong blue
    private static readonly Color PlaceholderText = Color.FromArgb("#808080");

    /// <inheritdoc/>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 1. Background fill.
        canvas.FillColor = BackgroundFill;
        canvas.FillRectangle(dirtyRect);

        if (Sample is null || Sample.Metadata.FrameCount == 0)
        {
            canvas.FontColor = PlaceholderText;
            canvas.FontSize = 12;
            canvas.DrawString(
                "No sample loaded",
                dirtyRect,
                Microsoft.Maui.Graphics.HorizontalAlignment.Center,
                VerticalAlignment.Center);
            return;
        }

        var frameCount = Sample.Metadata.FrameCount;
        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        var midY = dirtyRect.Y + height / 2f;

        // Effective trim range in source-frame coordinates.
        var effectiveTrimStart = Math.Clamp(TrimStartFrame, 0, frameCount);
        var effectiveTrimEnd = TrimEndFrame > 0
            ? Math.Clamp(TrimEndFrame, 0, frameCount)
            : frameCount;

        // Defensive: ensure at least a 1-frame range to avoid division artefacts.
        if (effectiveTrimEnd <= effectiveTrimStart)
        {
            effectiveTrimEnd = Math.Min(effectiveTrimStart + 1, frameCount);
        }

        // Effective loop range mapped to source-frame coordinates.
        // Loop bounds are stored relative to the trimmed region (Task 40 contract).
        var trimmedLength = effectiveTrimEnd - effectiveTrimStart;
        var loopStartInSource = 0;
        var loopEndInSource = 0;
        if (LoopEnabled)
        {
            loopStartInSource = effectiveTrimStart + Math.Clamp(LoopStartFrame, 0, trimmedLength);
            loopEndInSource = LoopEndFrame > 0
                ? effectiveTrimStart + Math.Clamp(LoopEndFrame, 0, trimmedLength)
                : effectiveTrimEnd;

            // Clip loop region to remain within trim bounds.
            loopStartInSource = Math.Clamp(loopStartInSource, effectiveTrimStart, effectiveTrimEnd);
            loopEndInSource = Math.Clamp(loopEndInSource, effectiveTrimStart, effectiveTrimEnd);
        }

        // Helper: source frame → x-coordinate within dirtyRect.
        float FrameToX(int frame) => dirtyRect.X + frame / (float)frameCount * width;

        // 3. Trim region shading (light orange/peach).
        if (effectiveTrimStart > 0 || effectiveTrimEnd < frameCount)
        {
            var trimX = FrameToX(effectiveTrimStart);
            var trimW = FrameToX(effectiveTrimEnd) - trimX;
            canvas.FillColor = TrimShading;
            canvas.FillRectangle(trimX, dirtyRect.Y, trimW, height);
        }

        // 4. Loop region shading (semi-transparent blue).
        if (LoopEnabled && loopEndInSource > loopStartInSource)
        {
            var loopX = FrameToX(loopStartInSource);
            var loopW = FrameToX(loopEndInSource) - loopX;
            canvas.FillColor = LoopShading;
            canvas.FillRectangle(loopX, dirtyRect.Y, loopW, height);
        }

        // 2. Centre line (drawn after shading so it's always visible).
        canvas.StrokeColor = CentreLineColor;
        canvas.StrokeSize = 1;
        canvas.DrawLine(dirtyRect.X, midY, dirtyRect.X + width, midY);

        // 5. Waveform: one vertical line per pixel column, peak-per-pixel decimation.
        //    Multi-channel samples are downmixed to mono by averaging channels per frame.
        var pixelCount = Math.Max(1, (int)Math.Floor(width));
        var framesPerPixel = Math.Max(1, frameCount / pixelCount);
        var channelCount = Sample.Metadata.ChannelCount;

        canvas.StrokeColor = WaveformFill;
        canvas.StrokeSize = 1;

        for (var px = 0; px < pixelCount; px++)
        {
            var startFrame = px * framesPerPixel;
            var endFrame = Math.Min(frameCount, startFrame + framesPerPixel);
            var peak = 0f;

            for (var f = startFrame; f < endFrame; f++)
            {
                var sum = 0f;
                for (var c = 0; c < channelCount; c++)
                {
                    sum += Sample.Channels[c][f];
                }

                var avg = sum / channelCount;
                var absAvg = Math.Abs(avg);
                if (absAvg > peak)
                {
                    peak = absAvg;
                }
            }

            var x = dirtyRect.X + px;
            var halfH = peak * (height / 2f);
            canvas.DrawLine(x, midY - halfH, x, midY + halfH);
        }

        // 6. Trim boundary lines (orange, 1px).
        canvas.StrokeSize = 1;
        canvas.StrokeColor = TrimLine;
        if (effectiveTrimStart > 0)
        {
            var x = FrameToX(effectiveTrimStart);
            canvas.DrawLine(x, dirtyRect.Y, x, dirtyRect.Y + height);
        }

        if (effectiveTrimEnd < frameCount)
        {
            var x = FrameToX(effectiveTrimEnd);
            canvas.DrawLine(x, dirtyRect.Y, x, dirtyRect.Y + height);
        }

        // 7. Loop boundary lines (blue, 1px).
        if (LoopEnabled)
        {
            canvas.StrokeColor = LoopLine;
            var lsX = FrameToX(loopStartInSource);
            canvas.DrawLine(lsX, dirtyRect.Y, lsX, dirtyRect.Y + height);

            var leX = FrameToX(loopEndInSource);
            canvas.DrawLine(leX, dirtyRect.Y, leX, dirtyRect.Y + height);
        }
    }
}
