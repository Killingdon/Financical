using DocumentFormat.OpenXml.Drawing.Charts;
using LiveCharts.Definitions.Charts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PieChart = DocumentFormat.OpenXml.Drawing.Charts.PieChart;
using SkiaSharp.Views.WPF;
using LiveCharts.Definitions.Series;
using LiveCharts;


namespace Financical
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string currencyApiUrl = "http://api.exchangeratesapi.io/v1/latest?access_key=29acabc3ecfa09ae2c37538b582e8f78";
        private string connectionString = "Server=DESKTOP-IUGPTQJ\\SHOP;Database=Financial;Trusted_Connection=True;TrustServerCertificate=True";
        public int Needs = 1;
        public int _Wants = 1;
        public int _Savings = 1;
        public ObservableCollection<string> CurrencyRates { get; set; } = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;


            LoadCurrencyRates();
            InitializeCurrencyUpdateTimer();

            #region Standart Diagram
            var normalSeries = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "Needs",
                    Values = new ChartValues<int> { 50 }, // ChartValues<double> вместо { Needs }
                    Fill = new SolidColorBrush(Colors.Blue)
                },
                new PieSeries
                {
                    Title = "Wants",
                    Values = new ChartValues<int> { 30 },
                    Fill = new SolidColorBrush(Colors.Black)
                },
                new PieSeries
                {
                    Title = "Savings",
                    Values = new ChartValues<int> { 20 },
                    Fill = new SolidColorBrush(Colors.Green)
                }
            };
            normalDiagram.Series = normalSeries;
            #endregion
        }

        #region First Field
        private void AddFinances(object sender, RoutedEventArgs e)
        {
            string amount = AmountInput.Text;
            string needs = NeedsInput.Text;
            string savings = SavingsInput.Text;
            string wants = WantsInput.Text;
            string currency = (CurrencySelector.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(amount) || string.IsNullOrWhiteSpace(needs) || string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(savings) || string.IsNullOrWhiteSpace(wants))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(amount, out int amo) || !int.TryParse(needs, out int needAmount) || !int.TryParse(savings, out int savingsAmount) || !int.TryParse(wants, out int Wants))
            {
                MessageBox.Show("Введите корректные числа для суммы, потребностей и сбережений.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Needs = needAmount;
            _Savings = savingsAmount;
            _Wants = Wants;

            Diagrame();
            try
            {
                // Подключение к базе данных
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // SQL-запрос для вставки данных в таблицу Financial
                    string query = "INSERT INTO Finan (AmountMoney, Needs, Wants, Savings) " +
                                   "VALUES (@AmountMoney, @Needs, @Wants, @Savings)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Передача параметров в SQL-запрос
                        command.Parameters.AddWithValue("@AmountMoney", amo);
                        command.Parameters.AddWithValue("@Needs", needAmount);
                        command.Parameters.AddWithValue("Wants", Wants);
                        command.Parameters.AddWithValue("@Savings", savingsAmount);

                        // Выполнение запроса
                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Финансовая запись успешно добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            ClearInputs(); // Очистка полей после успешной записи
                        }
                        else
                        {
                            MessageBox.Show("Не удалось добавить запись в базу данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ClearInputs()
        {
            AmountInput.Text = string.Empty;
            NeedsInput.Text = string.Empty;
            WantsInput.Text = string.Empty;
            SavingsInput.Text = string.Empty;
            CurrencySelector.SelectedIndex = -1;
        }
        #endregion

        #region Second Field
        private async void LoadCurrencyRates()
        {
            try
            {
                // Получение данных из API
                var rates = await GetCurrencyRates();

                //Отоброжение данных в ListBox
                CurrencyRates.Clear();
                foreach (var rate in rates)
                {
                    CurrencyRates.Add($"{rate.Key}: {rate.Value:F2}");
                    
                }   
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки валют: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        private async Task<Dictionary<string, decimal>> GetCurrencyRates()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Отправка запроса
                    var response = await client.GetAsync(currencyApiUrl);

                    // Проверка на успешность ответа
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception("Не удалось загрузить курсы валют.");
                    }

                    // Чтение тела ответа
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Парсинг JSON ответа
                    var json = JsonDocument.Parse(responseBody);
                    var rates = new Dictionary<string, decimal>();

                    // Извлечение курсов валют
                    foreach (var property in json.RootElement.GetProperty("rates").EnumerateObject())
                    {
                        rates[property.Name] = property.Value.GetDecimal();
                    }
                    return rates;
                }
                catch (Exception ex)
                {
                    // Обработка ошибок
                    throw new Exception("Ошибка при запросе данных о курсах валют: " + ex.Message);
                }
            }
        }

        private void InitializeCurrencyUpdateTimer()
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };

            timer.Tick += (s, e) => LoadCurrencyRates();
            timer.Start();
        }
        #endregion

        public void Diagrame()
        {
            var pieSeries = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "Needs",
                    Values = new ChartValues<int> { Needs }, // ChartValues<double> вместо { Needs }
                    Fill = new SolidColorBrush(Colors.Blue)
                },
                new PieSeries
                {
                    Title = "Wants",
                    Values = new ChartValues<int> { _Wants },
                    Fill = new SolidColorBrush(Colors.Black)
                },
                new PieSeries
                {
                    Title = "Savings",
                    Values = new ChartValues<int> { _Savings },
                    Fill = new SolidColorBrush(Colors.Green)
                }
            };

            // Присваиваем данные круговой диаграмме
            pieChart.Series = pieSeries;
        }
    }
}


