using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Draws the Mousetrap icon: a box propped up on a stick with a computer mouse under
// it, its cable for a tail. It writes the .ico built into the exe, or a PNG of any
// size for the Store package. After changing the drawing:
//
//   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /r:System.Drawing.dll /out:%TEMP%\TrapIcon.exe assets\TrapIcon.cs
//   %TEMP%\TrapIcon.exe assets\mousetrap.ico
//   %TEMP%\TrapIcon.exe 150 packaging\Assets\Square150x150Logo.png     (and 44 and 50, see packaging\AppxManifest.xml)
public static class TrapIcon
{
    static readonly Color Orange = Color.FromArgb(255, 150, 0);
    static readonly Color Shade = Color.FromArgb(214, 112, 0);
    static readonly Color Wood = Color.FromArgb(176, 116, 52);
    static readonly Color Pale = Color.FromArgb(242, 242, 242);
    static readonly Color Line = Color.FromArgb(62, 58, 54);

    // The small sizes the notification area asks for at each zoom, and one large
    // drawing from which Windows scales everything else.
    static readonly int[] Sizes = { 16, 20, 24, 32, 256 };

    static int Main(string[] args)
    {
        int size;
        if (args.Length == 1) Write(args[0]);
        else if (args.Length == 2 && int.TryParse(args[0], out size))
            using (Bitmap drawing = Render(size)) drawing.Save(args[1], ImageFormat.Png);
        else
        {
            Console.WriteLine("Usage: TrapIcon <file.ico>   or   TrapIcon <size> <file.png>");
            return 1;
        }
        return 0;
    }

    public static void Write(string file)
    {
        byte[][] images = new byte[Sizes.Length][];
        for (int i = 0; i < Sizes.Length; i++)
            using (Bitmap drawing = Render(Sizes[i]))
                images[i] = Sizes[i] < 256 ? Uncompressed(drawing) : Png(drawing);

        using (BinaryWriter w = new BinaryWriter(File.Create(file)))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                // A size of 256 is written as 0.
                w.Write((byte)Sizes[i]);
                w.Write((byte)Sizes[i]);
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write((uint)images[i].Length);
                w.Write((uint)offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images) w.Write(image);
        }
    }

    public static Bitmap Render(int size)
    {
        // Drawn four times too big and reduced, which is kinder to thin lines than drawing small.
        int big = size * 4;
        using (Bitmap large = new Bitmap(big, big, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(large))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.ScaleTransform(big / 32f, big / 32f);
                // Small sizes get fatter lines and lose the fine detail.
                Draw(g, size <= 20 ? 1.55f : size <= 32 ? 1.2f : 1f, size > 24);
            }
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(large, new Rectangle(0, 0, size, size), 0, 0, big, big, GraphicsUnit.Pixel);
            }
            return result;
        }
    }

    // Everything is placed on a canvas 32 units across.
    static void Draw(Graphics g, float fat, bool detail)
    {
        // Lifted a little so that the drawing sits in the middle of the canvas.
        g.TranslateTransform(0.4f, -2.4f);

        // The cable, trailing out behind the mouse and past the stick.
        using (Pen cable = new Pen(Line, 1.05f * fat))
        {
            cable.StartCap = cable.EndCap = LineCap.Round;
            g.DrawBezier(cable, 10.6f, 25.6f, 6.6f, 27.6f, 5.2f, 22.6f, 1.6f, 24.6f);
        }

        // The stick holding the box up.
        using (Pen stick = new Pen(Wood, 2.5f * fat))
        {
            stick.StartCap = stick.EndCap = LineCap.Round;
            g.DrawLine(stick, 5.9f, 27.6f, 9.0f, 18.9f);
        }

        // The box: upside down, its right edge on the ground, its left edge in the air.
        GraphicsState upright = g.Save();
        g.TranslateTransform(23f, 28f);
        g.RotateTransform(33);
        using (GraphicsPath box = Rounded(new RectangleF(-17.5f, -13.5f, 17.5f, 13.5f), 2.3f))
        using (Brush fill = new SolidBrush(Orange)) g.FillPath(fill, box);
        // The darker band is the open mouth of the box.
        using (GraphicsPath mouth = Rounded(new RectangleF(-17.5f, -3.9f, 17.5f, 3.9f), 1.9f))
        using (Brush fill = new SolidBrush(Shade)) g.FillPath(fill, mouth);
        g.Restore(upright);

        // The computer mouse, seen from above, nose towards the back of the box.
        g.TranslateTransform(15.2f, 24.9f);
        g.RotateTransform(-4);
        using (GraphicsPath body = Rounded(new RectangleF(-5.2f, -3.1f, 10.4f, 6.2f), 3.05f))
        using (Brush fill = new SolidBrush(Pale))
        using (Pen edge = new Pen(Line, 0.95f * fat))
        {
            g.FillPath(fill, body);
            g.DrawPath(edge, body);
            if (detail)
            {
                // The two buttons.
                g.DrawLine(edge, 1.5f, -3.1f, 1.5f, 3.1f);
                g.DrawLine(edge, 1.5f, 0f, 5.2f, 0f);
            }
        }
    }

    static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static byte[] Png(Bitmap drawing)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            drawing.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }

    // The plain bitmap layout every program that reads icons understands: a header,
    // the pixels from the bottom row up, and a transparency mask left empty because
    // the pixels carry their own.
    static byte[] Uncompressed(Bitmap drawing)
    {
        int size = drawing.Width, maskRow = (size + 31) / 32 * 4;
        byte[] pixels = new byte[size * size * 4];
        BitmapData data = drawing.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        drawing.UnlockBits(data);

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(stream))
        {
            w.Write((uint)40);
            w.Write(size);
            w.Write(size * 2);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write((uint)0);
            w.Write((uint)(pixels.Length + maskRow * size));
            w.Write(0);
            w.Write(0);
            w.Write((uint)0);
            w.Write((uint)0);
            for (int row = size - 1; row >= 0; row--) w.Write(pixels, row * size * 4, size * 4);
            w.Write(new byte[maskRow * size]);
            w.Flush();
            return stream.ToArray();
        }
    }
}
