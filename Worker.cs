using System.Diagnostics;
using System.Globalization;
using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace workerBinanceKaydet
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _httpClient;
        //private readonly string _apiUrl = "http://51.12.247.74:5001/get_analysis";
        private readonly string _apiUrl = "http://localhost:5001/get_analysis";
        //private readonly string _connectionString = "Server=winserversw\\SQLEXPRESS;Database=coinck;Trusted_Connection=True;TrustServerCertificate=True;";
        //private readonly string _connectionString = "Server=localhost;Database=Ocean;User Id=sa;Password=zeka7744;Trusted_Connection=True;TrustServerCertificate=True;";
                                                     //Server=192.168.1.2;Database=Ocean;User Id=sa;Password=zeka7744;Trusted_Connection=True;TrustServerCertificate=True;
        private List<string> shareList = new List<string>();

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using (var connection = new SqlConnection(Program._connectionString))
                {
                    await connection.OpenAsync();
                    
                    var sql = @$"select * 
                                from {Program._dbName}.dbo.SYMBOL 
                                where 1=1 
                                and Source = 'BIST' 
                                and Active = 1 ";

                    var command = new SqlCommand(sql, connection);
                    
                    

                    SqlDataReader sqlDataReader = command.ExecuteReader();

                    while (sqlDataReader.Read())
                    {
                        string share = sqlDataReader.GetString(3).Trim();
                        shareList.Add(share);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Veritabanına kayıt sırasında hata: {ex.Message}");
                throw;
            }
            while (1 == 1)
            {
                var now = DateTime.Now;
                var minute = now.Minute;
                var second = now.Second;
                if (second != 0)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }
                await Task.Delay(1000, stoppingToken);

                string dateString = now.Year.ToString() + "-" + (100 + now.Month).ToString().Substring(1) + "-" + (100 + now.Day).ToString().Substring(1) + " 09:09:00,000";
                DateTime dtStart = DateTime.ParseExact(dateString, "yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture);

                dateString = dateString = now.Year.ToString() + "-" + (100 + now.Month).ToString().Substring(1) + "-" + (100 + now.Day).ToString().Substring(1) + " 18:36:00,000";
                DateTime dtEnd = DateTime.ParseExact(dateString, "yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture);

                if (now < dtStart || now > dtEnd)
                {
                    await Task.Delay(100, stoppingToken);
                    Console.WriteLine(now.ToString() + " Timed out!!!");
                    continue;
                }

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    
                    _= GetAnalysis("BTCUSDT.P", "1m", false);
                    //_= GetAnalysis("ATOMUSDT.P", "1m");

                    foreach(string share in shareList)
                    {
                        _= GetAnalysis(share, "1m", true);
                    }

                    if(minute == 15 || minute == 30 || minute == 45 || minute == 0)
                    {
                        _= GetAnalysis("BTCUSDT.P", "15m", false); 
                        //_= GetAnalysis("ATOMUSDT.P", "15m");
                        foreach(string share in shareList)
                        {
                            _= GetAnalysis(share, "15m", true);
                        }
                    }
                }
            }
        }

        private async Task<TradingViewAnalysis> GetAnalysis(string symbol, string interval, bool bist)
        {
            try
            {
                var queryString = "";
                if (bist)
                    queryString = $"?symbol={symbol}&screener=TURKEY&exchange=BIST&interval={interval}";
                else
                    queryString = $"?symbol={symbol}&screener=CRYPTO&exchange=BINANCE&interval={interval}";

                var response = await _httpClient.GetAsync(_apiUrl + queryString);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var analysis = JsonSerializer.Deserialize<TradingViewAnalysis>(content);
                    await SaveToDatabase(analysis, interval, symbol);
                    return analysis;    
                }
                
                _logger.LogError($"API çağrısı başarısız: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"API çağrısı sırasında hata: {ex.Message}");
                return null;
            }
        }

        private async Task SaveToDatabase(TradingViewAnalysis analysis, string interval, string symbol)
        {
            try
            {
                using (var connection = new SqlConnection(Program._connectionString))
                {
                    await connection.OpenAsync();
                    
                    var sql = $@"INSERT INTO {Program._dbName}.dbo.TradingViewAnalysis 
                              (CloseValue, Symbol, IndicatorsJson, OscillatorsJson, SummaryJson, AnalysisTime, CreatedAt, Interval)
                              VALUES 
                              (@CloseValue, @Symbol, @IndicatorsJson, @OscillatorsJson, @SummaryJson, @AnalysisTime, @CreatedAt, @Interval)";

                    var command = new SqlCommand(sql, connection);
                    
                    command.Parameters.AddWithValue("@CloseValue", analysis.close_value);
                    command.Parameters.AddWithValue("@IndicatorsJson", JsonSerializer.Serialize(analysis.indicators));
                    command.Parameters.AddWithValue("@OscillatorsJson", JsonSerializer.Serialize(analysis.oscillators));
                    command.Parameters.AddWithValue("@SummaryJson", JsonSerializer.Serialize(analysis.summary));
                    command.Parameters.AddWithValue("@AnalysisTime", DateTime.Parse(analysis.time));
                    command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@Interval", interval);
                    command.Parameters.AddWithValue("@Symbol", symbol);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Veritabanına kayıt sırasında hata: {ex.Message}");
                throw;
            }
        }

        public class TradingViewAnalysis
        {
            public decimal close_value { get; set; }
            public Indicators indicators { get; set; }
            public Oscillators oscillators { get; set; }
            public Summary summary { get; set; }
            public string time { get; set; }
        }

        public class Indicators
        {
            public decimal ADX { get; set; }
            [JsonPropertyName("ADX+DI")]
            public decimal ADXDI { get; set; }
            [JsonPropertyName("ADX-DI")]
            public decimal ADXDI_Negative { get; set; }
            public decimal AO { get; set; }
            [JsonPropertyName("BB.lower")]
            public decimal BBLower { get; set; }
            [JsonPropertyName("BB.upper")]
            public decimal BBUpper { get; set; }
            public decimal BBPower { get; set; }
            // Diğer indikatörler eklenebilir
        }

        public class Oscillators
        {
            public int BUY { get; set; }
            public Dictionary<string, string> COMPUTE { get; set; }
            public int NEUTRAL { get; set; }
            public string RECOMMENDATION { get; set; }
            public int SELL { get; set; }
        }

        public class Summary
        {
            public int BUY { get; set; }
            public int NEUTRAL { get; set; }
            public string RECOMMENDATION { get; set; }
            public int SELL { get; set; }
        }


            
    }    
}