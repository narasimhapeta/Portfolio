using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GridTrace.ViewModels;

namespace GridTrace.Rendering;

public static class SchematicRenderer
{
    public static void Render(Canvas canvas, IEnumerable<DeviceViewModel> devices)
    {
        canvas.Children.Clear();

        var deviceList = devices.ToList();
        var lookup = deviceList.ToDictionary(d => d.Id);

        foreach (var device in deviceList)
        {
            if (device.ParentId.HasValue && lookup.TryGetValue(device.ParentId.Value, out var parent))
            {
                var line = new Line
                {
                    X1 = parent.PosX,
                    Y1 = parent.PosY,
                    X2 = device.PosX,
                    Y2 = device.PosY,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1.5
                };
                canvas.Children.Add(line);
            }
        }

        foreach (var device in deviceList)
        {
            double size = device.DeviceType switch
            {
                "SUBSTATION" => 36,
                "FEEDER" => 24,
                "POLE" => 16,
                "TRANSFORMER" => 20,
                "METER" => 12,
                _ => 14
            };

            Shape shape = device.DeviceType == "SUBSTATION"
                ? new Rectangle { Width = size, Height = size * 0.75 }
                : new Ellipse { Width = size, Height = size };

            shape.Fill = device.Fill;
            shape.Stroke = Brushes.Black;
            shape.StrokeThickness = 1;
            shape.ToolTip = device.DisplayLabel;

            Canvas.SetLeft(shape, device.PosX - shape.Width / 2);
            Canvas.SetTop(shape, device.PosY - shape.Height / 2);
            canvas.Children.Add(shape);
        }
    }
}