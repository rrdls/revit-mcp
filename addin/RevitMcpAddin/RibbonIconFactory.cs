using System.Windows;
using System.Windows.Media;

namespace RevitMcpAddin;

internal static class RibbonIconFactory
{
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(88, 214, 141));
    private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(97, 182, 255));
    private static readonly Brush Yellow = new SolidColorBrush(Color.FromRgb(255, 207, 106));
    private static readonly Brush Text = new SolidColorBrush(Color.FromRgb(242, 245, 247));
    private static readonly Pen Stroke = new(Text, 2.3);

    public static ImageSource Create(string key)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(23, 29, 35)), null, new RectangleGeometry(new Rect(0, 0, 32, 32), 4, 4)));

        switch (key)
        {
            case "start":
                group.Children.Add(new GeometryDrawing(Accent, null, Geometry.Parse("M 12 8 L 24 16 L 12 24 Z")));
                break;
            case "stop":
                group.Children.Add(new GeometryDrawing(Yellow, null, new RectangleGeometry(new Rect(10, 10, 12, 12), 1, 1)));
                break;
            case "copy":
                group.Children.Add(new GeometryDrawing(null, Stroke, new RectangleGeometry(new Rect(9, 11, 12, 12), 1, 1)));
                group.Children.Add(new GeometryDrawing(null, new Pen(Blue, 2), new RectangleGeometry(new Rect(13, 7, 12, 12), 1, 1)));
                break;
            case "public":
                group.Children.Add(new GeometryDrawing(null, new Pen(Blue, 2), new EllipseGeometry(new Point(16, 16), 9, 9)));
                group.Children.Add(new GeometryDrawing(null, new Pen(Accent, 2), Geometry.Parse("M 7 16 L 25 16 M 16 7 C 11 12 11 20 16 25 M 16 7 C 21 12 21 20 16 25")));
                break;
            case "status":
                group.Children.Add(new GeometryDrawing(null, new Pen(Accent, 2.4), Geometry.Parse("M 7 17 L 12 22 L 25 9")));
                break;
            case "tools":
                group.Children.Add(new GeometryDrawing(null, Stroke, new RectangleGeometry(new Rect(8, 8, 16, 6), 1, 1)));
                group.Children.Add(new GeometryDrawing(null, new Pen(Blue, 2), new RectangleGeometry(new Rect(8, 18, 16, 6), 1, 1)));
                group.Children.Add(new GeometryDrawing(Accent, null, new EllipseGeometry(new Point(12, 11), 1.8, 1.8)));
                group.Children.Add(new GeometryDrawing(Accent, null, new EllipseGeometry(new Point(12, 21), 1.8, 1.8)));
                break;
            case "settings":
                group.Children.Add(new GeometryDrawing(null, Stroke, new EllipseGeometry(new Point(16, 16), 4.3, 4.3)));
                group.Children.Add(new GeometryDrawing(null, new Pen(Blue, 2), Geometry.Parse("M 16 5 L 16 9 M 16 23 L 16 27 M 5 16 L 9 16 M 23 16 L 27 16 M 8.2 8.2 L 11 11 M 21 21 L 23.8 23.8 M 23.8 8.2 L 21 11 M 11 21 L 8.2 23.8")));
                break;
            case "logs":
                group.Children.Add(new GeometryDrawing(null, Stroke, Geometry.Parse("M 7 10 L 14 10 L 16 13 L 25 13 L 25 24 L 7 24 Z")));
                group.Children.Add(new GeometryDrawing(Blue, null, new RectangleGeometry(new Rect(10, 16, 12, 2))));
                group.Children.Add(new GeometryDrawing(Blue, null, new RectangleGeometry(new Rect(10, 20, 8, 2))));
                break;
            default:
                group.Children.Add(new GeometryDrawing(Accent, null, new EllipseGeometry(new Point(16, 16), 8, 8)));
                break;
        }

        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
