#if WINRT
using System;
using System.IO;
using System.Threading.Tasks;
using Lumen.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Lumen.App;

public sealed class WindowsMedia : IMediaSource
{
	private GlobalSystemMediaTransportControlsSessionManager? manager;

	private volatile MediaInfo? current;

	private byte[]? cover;

	private string? coverKey;

	public WindowsMedia()
	{
		Start();
	}

	private async Task Start()
	{
		try
		{
			manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
		}
		catch
		{
			return;
		}
		while (true)
		{
			try
			{
				current = await ReadAsync();
			}
			catch
			{
				current = null;
			}
			await Task.Delay(1000);
		}
	}

	private async Task<MediaInfo?> ReadAsync()
	{
		GlobalSystemMediaTransportControlsSession session = manager?.GetCurrentSession();
		if (session == null)
		{
			return null;
		}
		GlobalSystemMediaTransportControlsSessionMediaProperties props = await session.TryGetMediaPropertiesAsync();
		if (props == null || string.IsNullOrWhiteSpace(props.Title))
		{
			return null;
		}
		GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = session.GetPlaybackInfo();
		GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
		bool playing = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
		string text = props.Title + "|" + props.Artist + "|" + props.AlbumTitle;
		if (text != coverKey)
		{
			coverKey = text;
			cover = null;
			if (props.Thumbnail != null)
			{
				try
				{
					using IRandomAccessStreamWithContentType stream = await props.Thumbnail.OpenReadAsync();
					using MemoryStream copy = new MemoryStream();
					await stream.AsStreamForRead().CopyToAsync(copy);
					cover = copy.ToArray();
				}
				catch
				{
				}
			}
		}
		double? durationSeconds = ((timeline.EndTime > TimeSpan.Zero) ? new double?((timeline.EndTime - timeline.StartTime).TotalSeconds) : ((double?)null));
		double? positionSeconds = null;
		if (durationSeconds.HasValue)
		{
			positionSeconds = Math.Clamp((timeline.Position + (playing ? (DateTimeOffset.Now - timeline.LastUpdatedTime) : TimeSpan.Zero)).TotalSeconds, 0.0, durationSeconds.Value);
		}
		return new MediaInfo(props.Title, props.Artist, AppName(session.SourceAppUserModelId), playing, positionSeconds, durationSeconds, cover);
	}

	private static string AppName(string id)
	{
		string text = id;
		int num = text.LastIndexOf('!');
		if (num >= 0)
		{
			string text2 = text;
			int num2 = num + 1;
			text = text2.Substring(num2, text2.Length - num2);
		}
		if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			text = text2.Substring(0, text2.Length - 4);
		}
		text = text.ToLowerInvariant() switch
		{
			"msedge" => "Edge", 
			"chrome" => "Chrome", 
			"firefox" => "Firefox", 
			"app" => "Media Player", 
			_ => text, 
		};
		if (text.Length <= 0)
		{
			return text;
		}
		ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(char.ToUpperInvariant(text[0]));
		string text3 = text;
		return string.Concat(readOnlySpan, text3.Substring(1, text3.Length - 1));
	}

	public MediaInfo? Read()
	{
		return current;
	}

	public void Send(string command)
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = manager?.GetCurrentSession();
		if (!(globalSystemMediaTransportControlsSession == null))
		{
			switch (command)
			{
			case "toggle":
				globalSystemMediaTransportControlsSession.TryTogglePlayPauseAsync().AsTask();
				break;
			case "next":
				globalSystemMediaTransportControlsSession.TrySkipNextAsync().AsTask();
				break;
			case "previous":
				globalSystemMediaTransportControlsSession.TrySkipPreviousAsync().AsTask();
				break;
			default:
				_ = Task.CompletedTask;
				break;
			}
		}
	}
}

#else
namespace Lumen.App;
public sealed class WindowsMedia : IMediaSource { public Lumen.Core.MediaInfo? Read() => null; public void Send(string command) { } }
#endif

