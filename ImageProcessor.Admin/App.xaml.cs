using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ImageProcessor.Admin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private bool _isDarkTheme = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            // グローバル例外ハンドラを設定
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // 保存されたテーマ設定を読み込み
            LoadThemeSettings();

            base.OnStartup(e);
        }

        private void LoadThemeSettings()
        {
            try
            {
                // App.config からテーマ設定を読み込む
                var isDark = ImageProcessor.Admin.Properties.Settings.Default.IsDarkTheme;
                if (isDark)
                {
                    ApplyDarkTheme();
                }
            }
            catch (Exception ex)
            {
                // 設定の読み込みに失敗した場合はデフォルト（ライトテーマ）を使用
                System.Diagnostics.Trace.TraceWarning($"Failed to load theme settings: {ex.Message}");
            }
        }

        public void ToggleTheme()
        {
            _isDarkTheme = !_isDarkTheme;

            if (_isDarkTheme)
            {
                ApplyDarkTheme();
            }
            else
            {
                ApplyLightTheme();
            }

            // 設定を保存（エラーが発生しても続行）
            try
            {
                ImageProcessor.Admin.Properties.Settings.Default.IsDarkTheme = _isDarkTheme;
                ImageProcessor.Admin.Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                // 設定の保存に失敗した場合はログに記録するだけで続行
                System.Diagnostics.Trace.TraceWarning($"Failed to save theme settings: {ex.Message}");
            }
        }

        private void ApplyDarkTheme()
        {
            _isDarkTheme = true;

            // ダークテーマのカラーパレット
            UpdateResource("FluentAccentBrush", "#0078D4");
            UpdateResource("FluentAccentLightBrush", "#1890D4");
            UpdateResource("FluentBackgroundBrush", "#1E1E1E");
            UpdateResource("FluentCardBrush", "#2D2D2D");
            UpdateResource("FluentBorderBrush", "#3F3F3F");
            UpdateResource("FluentTextBrush", "#FFFFFF");
            UpdateResource("FluentTextSecondaryBrush", "#B4B4B4");
            UpdateResource("FluentSuccessBrush", "#6CCB5F");
            UpdateResource("FluentWarningBrush", "#FFA500");
            UpdateResource("FluentHoverBrush", "#3F3F3F");
            UpdateResource("FluentMetricBackgroundBrush", "#3A3A3A");

            // MahApps.Metro のダークテーマを適用
            var theme = Resources.MergedDictionaries[0];
            theme.MergedDictionaries.Clear();
            theme.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Dark.Blue.xaml")
            });
        }

        private void ApplyLightTheme()
        {
            _isDarkTheme = false;

            // ライトテーマのカラーパレット
            UpdateResource("FluentAccentBrush", "#0078D4");
            UpdateResource("FluentAccentLightBrush", "#1890D4");
            UpdateResource("FluentBackgroundBrush", "#F3F3F3");
            UpdateResource("FluentCardBrush", "#FFFFFF");
            UpdateResource("FluentBorderBrush", "#E1E1E1");
            UpdateResource("FluentTextBrush", "#1F1F1F");
            UpdateResource("FluentTextSecondaryBrush", "#605E5C");
            UpdateResource("FluentSuccessBrush", "#107C10");
            UpdateResource("FluentWarningBrush", "#FFA500");
            UpdateResource("FluentHoverBrush", "#F0F8FF");
            UpdateResource("FluentMetricBackgroundBrush", "#F0F0F0");

            // MahApps.Metro のライトテーマを適用
            var theme = Resources.MergedDictionaries[0];
            theme.MergedDictionaries.Clear();
            theme.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Blue.xaml")
            });
        }

        private void UpdateResource(string key, string colorHex)
        {
            if (Resources.Contains(key))
            {
                Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            }
        }

        public bool IsDarkTheme => _isDarkTheme;

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
