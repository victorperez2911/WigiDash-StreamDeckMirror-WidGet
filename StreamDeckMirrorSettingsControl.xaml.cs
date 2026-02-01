using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WigiDash.StreamDeckMirrorWidget
{
    /// <summary>
    /// Settings control for Stream Deck Mirror widget configuration
    /// </summary>
    public partial class StreamDeckMirrorSettingsControl : UserControl
    {
        private readonly StreamDeckMirrorWidgetInstance _instance;
        private List<StreamDeckWindowInfo> _currentWindows;
        private bool _isInitializing = true;

        public StreamDeckMirrorSettingsControl(StreamDeckMirrorWidgetInstance instance)
        {
            InitializeComponent();
            _instance = instance;

            // Load settings synchronously (fast)
            LoadCurrentSettings();

            // Defer device list refresh to avoid blocking UI
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                RefreshDeviceListAsync();
                _isInitializing = false;
            }));
        }

        private void RefreshDeviceListAsync()
        {
            try
            {
                RefreshDeviceList();
            }
            catch (Exception)
            {
                // Silently handle errors during async refresh
                StatusText.Text = "Erro ao carregar dispositivos.";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 100));
                DeviceComboBox.IsEnabled = false;
            }
        }

        private void LoadCurrentSettings()
        {
            // Refresh interval
            RefreshSlider.Value = _instance.GetRefreshInterval();
            UpdateRefreshText();

            // Hide window
            HideWindowCheckBox.IsChecked = _instance.GetHideOriginalWindow();

            // Show footer
            ShowFooterCheckBox.IsChecked = _instance.GetShowFooterBar();
            LongPressDurationPanel.Visibility = _instance.GetShowFooterBar()
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Long press duration
            LongPressSlider.Value = _instance.GetLongPressDuration();
            UpdateLongPressText();

            // Update state indicator
            UpdateStateIndicator();
        }

        private void RefreshDeviceList()
        {
            DeviceComboBox.Items.Clear();

            if (!StreamDeckWindowFinder.IsStreamDeckRunning())
            {
                StatusText.Text = "Stream Deck não detectado. Abra o aplicativo Elgato Stream Deck.";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(200, 150, 100));
                DeviceComboBox.IsEnabled = false;
                return;
            }

            _currentWindows = StreamDeckWindowFinder.FindAllStreamDeckWindows();

            if (_currentWindows.Count == 0)
            {
                StatusText.Text = "Nenhum Virtual Device encontrado.";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(200, 150, 100));
                DeviceComboBox.IsEnabled = false;
                return;
            }

            DeviceComboBox.IsEnabled = true;

            // Group by signature to detect duplicates
            var groups = StreamDeckWindowFinder.GroupBySignature(_currentWindows);
            bool hasDuplicates = groups.Any(g => g.Value.Count > 1);

            if (hasDuplicates)
            {
                StatusText.Text = $"{_currentWindows.Count} Virtual Devices encontrados (alguns idênticos)";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(200, 180, 100));
            }
            else
            {
                StatusText.Text = $"{_currentWindows.Count} Virtual Device(s) encontrado(s)";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 100));
            }

            // Add items to combo box
            int index = 0;
            foreach (var group in groups)
            {
                var windows = group.Value;
                for (int i = 0; i < windows.Count; i++)
                {
                    var window = windows[i];
                    string displayText = windows.Count > 1
                        ? window.GetDisplayTextWithIndex(i)
                        : window.DisplayText;

                    var item = new ComboBoxItem
                    {
                        Content = displayText,
                        Tag = new DeviceComboItem
                        {
                            WindowInfo = window,
                            Index = i
                        }
                    };

                    DeviceComboBox.Items.Add(item);

                    // Select if matches saved device
                    var saved = _instance.GetSavedDevice();
                    if (saved != null &&
                        window.Title == saved.Title &&
                        window.Width == saved.Width &&
                        window.Height == saved.Height &&
                        i == saved.Index)
                    {
                        DeviceComboBox.SelectedItem = item;
                    }

                    index++;
                }
            }

            // If nothing selected but there's only one device, select it
            if (DeviceComboBox.SelectedItem == null && DeviceComboBox.Items.Count == 1)
            {
                DeviceComboBox.SelectedIndex = 0;
            }
        }

        private void UpdateStateIndicator()
        {
            var state = _instance.GetCurrentState();
            Color color;
            string text;

            switch (state)
            {
                case WidgetState.Connected:
                    color = Color.FromRgb(100, 200, 100);
                    text = "Conectado";
                    if (_instance.IsWindowHidden())
                        text += " (janela oculta)";
                    break;
                case WidgetState.AppNotRunning:
                    color = Color.FromRgb(200, 100, 100);
                    text = "App não está rodando";
                    break;
                case WidgetState.DeviceNotFound:
                    color = Color.FromRgb(200, 150, 100);
                    text = "Device não encontrado";
                    break;
                case WidgetState.CaptureError:
                    color = Color.FromRgb(200, 100, 100);
                    text = "Erro de captura";
                    break;
                case WidgetState.NotConfigured:
                default:
                    color = Color.FromRgb(150, 150, 150);
                    text = "Não configurado";
                    break;
            }

            StateIndicator.Fill = new SolidColorBrush(color);
            StateText.Text = text;
        }

        private void UpdateRefreshText()
        {
            // Guard against null during XAML initialization
            if (RefreshText == null || RefreshSlider == null) return;

            int ms = (int)RefreshSlider.Value;
            int fps = 1000 / ms;
            RefreshText.Text = $"{ms}ms ({fps} FPS)";
        }

        private void UpdateLongPressText()
        {
            // Guard against null during XAML initialization
            if (LongPressText == null || LongPressSlider == null) return;

            LongPressText.Text = $"{(int)LongPressSlider.Value}ms";
        }

        #region Event Handlers

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDeviceList();
            UpdateStateIndicator();
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (DeviceComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is DeviceComboItem deviceItem)
            {
                _instance.SetDevice(deviceItem.WindowInfo, deviceItem.Index);
                UpdateStateIndicator();
            }
        }

        private void RefreshSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateRefreshText();

            if (_isInitializing) return;
            _instance.SetRefreshInterval((int)RefreshSlider.Value);
        }

        private void HideWindowCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _instance.SetHideOriginalWindow(HideWindowCheckBox.IsChecked == true);
            UpdateStateIndicator();
        }

        private void ShowFooterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            LongPressDurationPanel.Visibility = ShowFooterCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (_isInitializing) return;
            _instance.SetShowFooterBar(ShowFooterCheckBox.IsChecked == true);
        }

        private void LongPressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateLongPressText();

            if (_isInitializing) return;
            _instance.SetLongPressDuration((int)LongPressSlider.Value);
        }

        #endregion

        /// <summary>
        /// Helper class for combo box item data
        /// </summary>
        private class DeviceComboItem
        {
            public StreamDeckWindowInfo WindowInfo { get; set; }
            public int Index { get; set; }
        }
    }
}
