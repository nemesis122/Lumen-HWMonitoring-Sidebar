using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Lumen.App;

/// <summary>Places the existing illustration's components on the reference layout. No values are baked into it.</summary>
public sealed class AioIsometricArtwork : Control
{
    private readonly Bitmap artwork;
    public int FanCount { get; init; } = 5;
    public bool ShowCpu { get; init; } = true;
    public bool ShowGpu { get; init; } = true;
    public bool ShowRam { get; init; } = true;
    public bool ShowDisk { get; init; } = true;
    public AioIsometricArtwork(Bitmap artwork)=>this.artwork=artwork;
    public override void Render(DrawingContext c)
    {
        // Coordinates use the illustration's reference space; the decoded file can have a different pixel size.
        double sx=artwork.PixelSize.Width/984d,sy=artwork.PixelSize.Height/1640d;
        if(ShowCpu)Piece(new Rect(20,105,420,315),new Rect(4,42,174,132),new[]{new Point(239,105),new Point(440,236),new Point(440,324),new Point(225,421),new Point(20,315),new Point(20,243)});
        if(ShowGpu)Piece(new Rect(20,335,585,585),new Rect(5,174,181,180),new[]{new Point(545,335),new Point(605,384),new Point(605,694),new Point(72,920),new Point(20,886),new Point(20,622)});
        if(ShowRam)Piece(new Rect(30,842,475,369),new Rect(6,344,146,116),new[]{new Point(443,842),new Point(480,865),new Point(480,1080),new Point(130,1207),new Point(30,1145),new Point(30,1104),new Point(60,1090),new Point(60,1005)});
        if(ShowDisk)Piece(new Rect(244,1120,370,215),new Rect(125,417,122,70));
        for(int i=0;i<Math.Min(5,FanCount);i++)Piece(new Rect(25+i*184.4,1362,184.4,168),new Rect(i*66.8,526,66.8,61));
        void Piece(Rect source,Rect target,Point[]? silhouette=null)
        {
            var pixels=new Rect(source.X*sx,source.Y*sy,source.Width*sx,source.Height*sy);
            if(silhouette==null){c.DrawImage(artwork,pixels,target);return;}
            var clip=new StreamGeometry();using(var p=clip.Open())
            {
                Point Map(Point q)=>new(target.X+(q.X-source.X)*target.Width/source.Width,target.Y+(q.Y-source.Y)*target.Height/source.Height);
                p.BeginFigure(Map(silhouette[0]),true);foreach(var q in silhouette.Skip(1))p.LineTo(Map(q));p.EndFigure(true);
            }
            using(c.PushGeometryClip(clip))c.DrawImage(artwork,pixels,target);
        }
    }
}
