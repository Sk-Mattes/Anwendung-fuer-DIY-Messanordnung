using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using System.Diagnostics;

namespace test1
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public SeriesCollection temp { get; set; }
        public SeriesCollection light { get; set; }
        public SeriesCollection ec { get; set; }

        private SerialPort serialPort;
        private DispatcherTimer timer;
        private Stopwatch stopwatch = new Stopwatch(); // Stopwatch für Zeitmessung
        private bool isPaused = false; // Variable zum Überprüfen, ob die Aufnahme pausiert ist

        private double _minX = 0;
        public double minX
        {
            get { return _minX; }
            set
            {
                _minX = value;
                OnPropertyChanged(nameof(minX));
            }
        }

        private double _maxX = 60;
        public double maxX
        {
            get { return _maxX; }
            set
            {
                _maxX = value;
                OnPropertyChanged(nameof(maxX));
            }
        }

        private double _maxYTemp = 100;
        public double maxYTemp
        {
            get { return _maxYTemp; }
            set
            {
                _maxYTemp = value;
                OnPropertyChanged(nameof(maxYTemp));
            }
        }

        private double _maxYLight = 100;
        public double maxYLight
        {
            get { return _maxYLight; }
            set
            {
                _maxYLight = value;
                OnPropertyChanged(nameof(maxYLight));
            }
        }

        private double _maxYEc = 100;
        public double MaxYEc
        {
            get { return _maxYEc; }
            set
            {
                _maxYEc = value;
                OnPropertyChanged(nameof(MaxYEc));
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Initialisiere Daten für die Liniendiagramme
            temp = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Temperatur in Grad C°",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = System.Windows.Media.Brushes.Red // Farbe der Linie
                }
            };

            light = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Licht in Lux",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = System.Windows.Media.Brushes.Green // Farbe der Linie
                }
            };

            ec = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Ec",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = System.Windows.Media.Brushes.Blue // Farbe der Linie
                }
            };

            DataContext = this;

            // Initialisiere die serielle Schnittstelle
            serialPort = new SerialPort("COM5", 115200);
            serialPort.DataReceived += SerialPort_DataReceived;

            // Timer für regelmäßige UI-Updates
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += Timer_Tick;

            // Setze die X-Achse auf 60 Sekunden von Anfang an
            minX = 0;
            maxX = 60;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    serialPort.Open();
                }
                if (isPaused)
                {
                    stopwatch.Start(); // Stopwatch fortsetzen
                    isPaused = false;
                }
                else
                {
                    stopwatch.Restart(); // Stopwatch neu starten
                }
                timer.Start();
                OutputTextBox.AppendText("Serielle Schnittstelle gestartet.\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Öffnen der Schnittstelle: {ex.Message}");
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
                OutputTextBox.AppendText("Serielle Schnittstelle gestoppt.\n");
            }
            stopwatch.Stop(); // Stopwatch stoppen
            timer.Stop();
            isPaused = true; // Aufnahme pausieren
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            temp[0].Values.Clear();
            light[0].Values.Clear();
            ec[0].Values.Clear();
            stopwatch.Reset(); // Stopwatch zurücksetzen
            minX = 0;
            maxX = 60;
            OutputTextBox.AppendText("Graphen zurückgesetzt.\n");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveDialog saveDialog = new SaveDialog();
            saveDialog.Owner = this;
            if (saveDialog.ShowDialog() == true)
            {
                string fileName = saveDialog.FileName;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    MessageBox.Show("Bitte geben Sie einen Dateinamen ein.");
                    return;
                }

                string dateTimeSuffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_saves");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                string fullPath = Path.Combine(directoryPath, $"{fileName}_{dateTimeSuffix}.csv");

                using (StreamWriter writer = new StreamWriter(fullPath))
                {
                    writer.WriteLine("Zeit (Sekunden);Temperatur in Grad °C;Licht in Lux;EC");
                    for (int i = 0; i < temp[0].Values.Count; i++)
                    {
                        string line = $"{i};{temp[0].Values[i]};{light[0].Values[i]};{ec[0].Values[i]}";
                        writer.WriteLine(line);
                    }
                }

                OutputTextBox.AppendText($"Daten in Datei gespeichert: {fullPath}\n");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (isPaused) return; // Datenaufnahme pausieren

            try
            {
                string data = serialPort.ReadLine();

                Dispatcher.Invoke(() =>
                {
                    OutputTextBox.AppendText($"Empfangene Daten: {data}\n");
                    OutputTextBox.ScrollToEnd();

                    if (data.StartsWith("data"))
                    {
                        string[] parts = data.Substring(5).Split(';');

                        if (parts.Length >= 3)
                        {
                            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double tempValue) &&
                                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lightValue) &&
                                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double ecValue))
                            {
                                double currentTime = stopwatch.Elapsed.TotalSeconds; // Verstrichene Zeit in Sekunden

                                UpdateLineSeries((ChartValues<double>)temp[0].Values, tempValue, currentTime, 1);
                                UpdateLineSeries((ChartValues<double>)light[0].Values, lightValue, currentTime, 2);
                                UpdateLineSeries((ChartValues<double>)ec[0].Values, ecValue, currentTime, 3);
                            }
                            else
                            {
                                OutputTextBox.AppendText("Fehler beim Parsen der Daten.\n");
                            }
                        }
                        else
                        {
                            OutputTextBox.AppendText("Ungültiges Datenformat.\n");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    OutputTextBox.AppendText($"Fehler beim Lesen der Daten: {ex.Message}\n");
                    OutputTextBox.ScrollToEnd();
                });
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            updateXAxis();
        }

        private void updateXAxis()
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            maxX = Math.Max(60, currentTime);
            minX = Math.Max(0, maxX - 60); // Zeige die letzten 60 Sekunden
        }

        private void UpdateLineSeries(ChartValues<double> values, double newValue, double currentTime, int chartNumber)
        {
            values.Add(newValue);

            // Entfernen Sie alte Datenpunkte, die außerhalb des Zeitfensters liegen
            while (values.Count > 0 && currentTime - (values.Count - 1) > 60)
            {
                values.RemoveAt(0);
            }

            UpdateYAxisMaximum(chartNumber);
        }

        private void UpdateYAxisMaximum(int chartNumber)
        {
            double maxValue;
            switch (chartNumber)
            {
                case 1:
                    maxValue = ((ChartValues<double>)temp[0].Values).DefaultIfEmpty(0).Max();
                    maxYTemp = Math.Ceiling(maxValue + 10);
                    OnPropertyChanged(nameof(maxYTemp));
                    break;
                case 2:
                    maxValue = ((ChartValues<double>)light[0].Values).DefaultIfEmpty(0).Max();
                    maxYLight = Math.Ceiling(maxValue + 10);
                    OnPropertyChanged(nameof(maxYLight));
                    break;
                case 3:
                    maxValue = ((ChartValues<double>)ec[0].Values).DefaultIfEmpty(0).Max();
                    MaxYEc = Math.Ceiling(maxValue + 10);
                    OnPropertyChanged(nameof(MaxYEc));
                    break;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

