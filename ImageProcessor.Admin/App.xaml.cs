using System;
using System.Windows;
using System.Windows.Threading;

namespace ImageProcessor.Admin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // グローバル例外ハンドラを設定
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"エラーが発生しました:\n\n{e.Exception.GetType().Name}\n{e.Exception.Message}\n\nスタックトレース:\n{e.Exception.StackTrace}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"致命的なエラーが発生しました:\n\n{exception?.GetType().Name}\n{exception?.Message}\n\nスタックトレース:\n{exception?.StackTrace}",
                "致命的エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
