using Lumen.Core;
#if WINRT
using Windows.Media.Control;
#endif

namespace Lumen.App;

public interface IMediaSource
{
    MediaInfo? Read();
    void Send(string command);
}

#if WINRT
// "Now playing" from the Windows media controls: Spotify, browsers, the Media Player and
// anything else that shows up in the Windows volume flyout.
public sealed class WindowsMedia : IMediaSource
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private volatile MediaInfo? current;
    private byte[]? cover;
    private string? coverKey;

    public WindowsMedia() => _ = Start();

    private async Task Start()
    {
        try { manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); }
        catch { return; }
        while (true)
        {
            try { current = await ReadAsync(); } catch { current = null; }
            await Task.Delay(1000);
        }
    }

    private async Task<MediaInfo?> ReadAsync()
    {
        var session = manager?.GetCurrentSession();
        if (session == null) return null;
        var props = await session.TryGetMediaPropertiesAsync();
        if (props == null || string.IsNullOrWhiteSpace(props.Title)) return null;
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        bool playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        string key = props.Title + "|" + props.Artist + "|" + props.AlbumTitle;
        if (key != coverKey)
        {
            coverKey = key;
            cover = null;
            if (props.Thumbnail != null)
            {
                try
                {
                    using var stream = await props.Thumbnail.OpenReadAsync();
                    using var copy = new MemoryStream();
                    await stream.AsStreamForRead().CopyToAsync(copy);
                    cover = copy.ToArray();
                }
                catch { }
            }
        }
        double? duration = timeline.EndTime > TimeSpan.Zero ? (timeline.EndTime - timeline.StartTime).TotalSeconds : null;
        double? position = null;
        if (duration != null)
        {
            // The position is only reported now and then; while playing, advance it ourselves.
            var pos = timeline.Position + (playing ? DateTimeOffset.Now - timeline.LastUpdatedTime : TimeSpan.Zero);
            position = Math.Clamp(pos.TotalSeconds, 0, duration.Value);
        }
        return new MediaInfo(props.Title, props.Artist, AppName(session.SourceAppUserModelId), playing, position, duration, cover);
    }

    // "Spotify.exe" → "Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" → "Spotify", "msedge" → "Edge".
    private static string AppName(string id)
    {
        string name = id;
        int bang = name.LastIndexOf('!');
        if (bang >= 0) name = name[(bang + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        name = name.ToLowerInvariant() switch { "msedge" => "Edge", "chrome" => "Chrome", "firefox" => "Firefox", "app" => "Media Player", _ => name };
        return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name;
    }

    public MediaInfo? Read() => current;

    public void Send(string command)
    {
        var session = manager?.GetCurrentSession();
        if (session == null) return;
        _ = command switch
        {
            "toggle" => session.TryTogglePlayPauseAsync().AsTask(),
            "next" => session.TrySkipNextAsync().AsTask(),
            "previous" => session.TrySkipPreviousAsync().AsTask(),
            _ => Task.CompletedTask
        };
    }
}
#endif
