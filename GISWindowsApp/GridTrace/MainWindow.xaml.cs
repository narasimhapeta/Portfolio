using System.ComponentModel;
using System.Windows;
using GridTrace.Rendering;
using GridTrace.ViewModels;

namespace GridTrace;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadDevicesAsync();

        foreach (var device in _viewModel.Devices)
        {
            device.PropertyChanged += Device_PropertyChanged;
        }

        RedrawCanvas();
    }

    private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeviceViewModel.Fill))
        {
            RedrawCanvas();
        }
    }

    private void RedrawCanvas()
    {
        SchematicRenderer.Render(SchematicCanvas, _viewModel.Devices);
    }
}