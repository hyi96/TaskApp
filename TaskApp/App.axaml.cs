using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TaskApp.Services;
using TaskApp.ViewModels;
using TaskApp.Views;

namespace TaskApp
{
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
                var storageService = new StorageService();
                var viewModel = new MainWindowViewModel(storageService);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel
                };

                desktop.Startup += async (s, e) =>
                {
                    await viewModel.LoadDataAsync();
                };

                var isClosing = false;
                desktop.MainWindow.Closing += async (s, e) =>
                {
                    if (!isClosing)
                    {
                        // Cancel the close to perform async save
                        e.Cancel = true;
                        try
                        {
                            await viewModel.SaveDataAsync();
                        }
                        finally
                        {
                            isClosing = true;
                            // Re-initiate close
                            desktop.MainWindow.Close();
                        }
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}