using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Vrs.App.ViewModels;
using Vrs.App.Views;

namespace Vrs.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            var startupCancellation = new CancellationTokenSource();
            desktop.ShutdownRequested += (_, _) => startupCancellation.Cancel();
            Dispatcher.UIThread.Post(async () => await viewModel.InitializeAsync(startupCancellation.Token));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
