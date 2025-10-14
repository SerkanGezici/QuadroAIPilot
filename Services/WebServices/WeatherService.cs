using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;
using QuadroAIPilot.Services;
using HtmlAgilityPack;
using System.Text.Json;
using System.Net;
using System.Text;
using System.Linq;

namespace QuadroAIPilot.Services.WebServices
{
    /// <summary>
    /// Hava durumu bilgilerini web scraping ile alır ve cache'ler
    /// </summary>
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private WeatherData _cachedWeather;
        private DateTime _lastUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(2);
        
        // Basit ve güvenilir web servisleri
        private const string WTTR_IN_URL = "https://wttr.in/Istanbul?format=j1";
        private const string MGM_API_URL = "https://servis.mgm.gov.tr/web/tahminler/gunluk?istno=17064"; // İstanbul istasyon kodu
        private const string MGM_XML_URL = "https://www.mgm.gov.tr/FTPDATA/analiz/sonSOA.xml"; // MGM XML verisi
        
        public WeatherService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/html, */*");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr-TR,tr;q=0.9,en;q=0.8");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Hava durumu verilerini getirir (cache'lenmiş veya yeni)
        /// </summary>
        public async Task<WeatherData> GetWeatherAsync()
        {
            try
            {
                // Cache kontrol et
                if (_cachedWeather != null && DateTime.Now - _lastUpdate < _cacheExpiry)
                {
                    LoggingService.LogWarning($"[WeatherService] Cache'den veri döndürülüyor");
                    return _cachedWeather;
                }

                // 1. Önce MGM API'den dene - En güvenilir kaynak
                var weatherData = await TryGetFromMGMApi();
                if (weatherData != null)
                {
                    _cachedWeather = weatherData;
                    _lastUpdate = DateTime.Now;
                    LoggingService.LogWarning($"[WeatherService] MGM API'den başarıyla veri alındı: {weatherData.Temperature}°C");
                    return weatherData;
                }

                // 2. MGM başarısızsa Open-Meteo API'den dene
                weatherData = await TryGetFromOpenMeteo();
                if (weatherData != null)
                {
                    _cachedWeather = weatherData;
                    _lastUpdate = DateTime.Now;
                    LoggingService.LogWarning($"[WeatherService] Open-Meteo'dan başarıyla veri alındı: {weatherData.Temperature}°C");
                    return weatherData;
                }

                // 3. Open-Meteo başarısızsa wttr.in'den dene
                weatherData = await TryGetFromWttrIn();
                if (weatherData != null)
                {
                    _cachedWeather = weatherData;
                    _lastUpdate = DateTime.Now;
                    LoggingService.LogWarning($"[WeatherService] wttr.in'den başarıyla veri alındı: {weatherData.Temperature}°C");
                    return weatherData;
                }

                // Son çare: cache'deki veriyi döndür veya mock data
                if (_cachedWeather != null)
                {
                    LoggingService.LogWarning("[WeatherService] Eski cache verisi kullanılıyor");
                    return _cachedWeather;
                }

                LoggingService.LogWarning("[WeatherService] Tüm kaynaklar başarısız, mock data döndürülüyor");
                return GetMockWeatherData();
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[WeatherService] Hava durumu alınamadı: {ex.Message}");
                return _cachedWeather ?? GetMockWeatherData();
            }
        }

        /// <summary>
        /// wttr.in'den JSON formatında hava durumu al
        /// </summary>
        private async Task<WeatherData> TryGetFromWttrIn()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(WTTR_IN_URL);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                // Güncel hava durumu
                if (!root.TryGetProperty("current_condition", out var currentConditions) ||
                    currentConditions.GetArrayLength() == 0)
                {
                    return null;
                }

                var current = currentConditions[0];
                
                // Sıcaklık
                if (!current.TryGetProperty("temp_C", out var tempElement))
                    return null;
                
                var temperature = double.Parse(tempElement.GetString(), CultureInfo.InvariantCulture);
                
                // Açıklama
                var description = "Bilinmiyor";
                if (current.TryGetProperty("lang_tr", out var langTr) &&
                    langTr.GetArrayLength() > 0 &&
                    langTr[0].TryGetProperty("value", out var descElement))
                {
                    description = descElement.GetString() ?? "Bilinmiyor";
                }
                
                // Diğer değerler
                var humidity = current.TryGetProperty("humidity", out var humElement) ? 
                    int.Parse(humElement.GetString()) : 60;
                    
                var feelsLike = current.TryGetProperty("FeelsLikeC", out var feelsElement) ? 
                    double.Parse(feelsElement.GetString(), CultureInfo.InvariantCulture) : temperature;
                    
                var windSpeed = current.TryGetProperty("windspeedKmph", out var windElement) ? 
                    double.Parse(windElement.GetString(), CultureInfo.InvariantCulture) : 10;

                return new WeatherData
                {
                    Temperature = temperature,
                    Description = description,
                    IconCode = GetIconCodeFromDescription(description),
                    Humidity = humidity,
                    FeelsLike = feelsLike,
                    WindSpeed = windSpeed,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[WeatherService] wttr.in'den veri alınamadı: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// MGM XML verisinden hava durumu al
        /// </summary>
        private async Task<WeatherData> TryGetFromMGMApi()
        {
            try
            {
                // Önce JSON API'yi dene
                try
                {
                    var jsonResponse = await _httpClient.GetStringAsync(MGM_API_URL);
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var root = doc.RootElement;

                    if (root.GetArrayLength() > 0)
                    {
                        var today = root[0];
                        
                        var maxTemp = today.TryGetProperty("maksimumSicaklik", out var maxElement) ? 
                            maxElement.GetDouble() : 20;
                        var minTemp = today.TryGetProperty("minimumSicaklik", out var minElement) ? 
                            minElement.GetDouble() : 15;
                        var temperature = (maxTemp + minTemp) / 2;
                        
                        var description = today.TryGetProperty("hadiseKodu", out var hadiseElement) ? 
                            GetDescriptionFromHadiseKodu(hadiseElement.GetString()) : "Bilinmiyor";
                        
                        var humidity = today.TryGetProperty("maksimumNem", out var nemElement) ? 
                            nemElement.GetInt32() : 60;
                        
                        var windSpeed = today.TryGetProperty("maksimumRuzgarHizi", out var windElement) ? 
                            windElement.GetDouble() : 10;

                        LoggingService.LogWarning($"[WeatherService] MGM JSON API'den veri alındı: {temperature}°C");
                        
                        return new WeatherData
                        {
                            Temperature = temperature,
                            Description = description,
                            IconCode = GetIconCodeFromDescription(description),
                            Humidity = humidity,
                            FeelsLike = temperature,
                            WindSpeed = windSpeed,
                            LastUpdated = DateTime.Now,
                            Source = "MGM API"
                        };
                    }
                }
                catch
                {
                    // JSON API başarısız olursa XML'i dene
                }

                // XML verisini dene
                var xmlResponse = await _httpClient.GetStringAsync(MGM_XML_URL);
                var xdoc = System.Xml.Linq.XDocument.Parse(xmlResponse);
                
                var istanbulElement = xdoc.Descendants("sehirler")
                    .FirstOrDefault(s => 
                        s.Element("ili")?.Value?.ToUpper(new CultureInfo("tr-TR")) == "İSTANBUL" ||
                        s.Element("Merkez")?.Value?.ToUpper(new CultureInfo("tr-TR")) == "İSTANBUL");

                if (istanbulElement != null)
                {
                    var durum = istanbulElement.Element("Durum")?.Value ?? "Bilinmiyor";
                    var maksimum = istanbulElement.Element("Mak")?.Value;
                    
                    if (double.TryParse(maksimum, out double maxTemp))
                    {
                        LoggingService.LogWarning($"[WeatherService] MGM XML'den veri alındı: {maxTemp}°C, {durum}");
                        
                        return new WeatherData
                        {
                            Temperature = maxTemp,
                            Description = durum,
                            IconCode = GetIconCodeFromDescription(durum),
                            Humidity = EstimateHumidityFromDescription(durum),
                            FeelsLike = maxTemp - 2,
                            WindSpeed = EstimateWindSpeedFromDescription(durum),
                            LastUpdated = DateTime.Now,
                            Source = "MGM XML"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[WeatherService] MGM verisi alınamadı: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Open-Meteo API'den hava durumu al
        /// </summary>
        private async Task<WeatherData> TryGetFromOpenMeteo()
        {
            try
            {
                // İstanbul koordinatları
                var lat = 41.0082;
                var lon = 28.9784;
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&current_weather=true&temperature_unit=celsius";
                
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("current_weather", out var current))
                {
                    var weatherCode = current.GetProperty("weathercode").GetInt32();
                    
                    var weather = new WeatherData
                    {
                        Temperature = current.GetProperty("temperature").GetDouble(),
                        Description = MapWeatherCode(weatherCode),
                        IconCode = weatherCode.ToString().PadLeft(2, '0') + "d",
                        Humidity = 60, // Open-Meteo temel API'de nem yok
                        FeelsLike = current.GetProperty("temperature").GetDouble() - 2,
                        WindSpeed = current.GetProperty("windspeed").GetDouble(),
                        LastUpdated = DateTime.Now,
                        Source = "Open-Meteo"
                    };
                    
                    return weather;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"[WeatherService] Open-Meteo API başarısız: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Open-Meteo weather code'ları Türkçe açıklamaya çevir
        /// </summary>
        private string MapWeatherCode(int code)
        {
            return code switch
            {
                0 => "Açık",
                1 or 2 => "Az bulutlu",
                3 => "Parçalı bulutlu",
                45 or 48 => "Sisli",
                51 or 53 or 55 => "Hafif yağmurlu",
                61 or 63 or 65 => "Yağmurlu",
                71 or 73 or 75 => "Kar yağışlı",
                77 => "Karla karışık yağmurlu",
                80 or 81 or 82 => "Sağanak yağışlı",
                85 or 86 => "Yoğun kar yağışlı",
                95 => "Gökgürültülü fırtına",
                96 or 99 => "Dolu",
                _ => "Değişken"
            };
        }

        /// <summary>
        /// MGM hadise kodundan açıklama üret
        /// </summary>
        private string GetDescriptionFromHadiseKodu(string kod)
        {
            return kod switch
            {
                "A" => "Açık",
                "AB" => "Az bulutlu",
                "PB" => "Parçalı bulutlu",
                "CB" => "Çok bulutlu",
                "K" => "Kapalı",
                "Y" => "Yağmurlu",
                "KY" => "Karla karışık yağmurlu",
                "KAR" => "Kar yağışlı",
                "DY" => "Dolu",
                "GSY" => "Gökgürültülü sağanak yağışlı",
                "SIS" => "Sisli",
                _ => "Değişken"
            };
        }

        /// <summary>
        /// Hava durumu açıklamasından icon kodu üret
        /// </summary>
        private string GetIconCodeFromDescription(string description)
        {
            description = description.ToLowerInvariant();
            
            if (description.Contains("açık") || description.Contains("güneşli"))
                return "01d";
            else if (description.Contains("az bulutlu") || description.Contains("parçalı"))
                return "02d";
            else if (description.Contains("bulutlu") || description.Contains("kapalı"))
                return "03d";
            else if (description.Contains("yağmur") || description.Contains("yağış"))
                return "10d";
            else if (description.Contains("gökgürültü") || description.Contains("sağanak"))
                return "11d";
            else if (description.Contains("kar"))
                return "13d";
            else if (description.Contains("sis") || description.Contains("pus"))
                return "50d";
            else
                return "01d";
        }

        /// <summary>
        /// Mock hava durumu verisi döndür
        /// </summary>
        private WeatherData GetMockWeatherData()
        {
            var random = new Random();
            var temps = new[] { 18, 20, 22, 15, 25, 19, 21 };
            var descriptions = new[] { "Açık", "Parçalı bulutlu", "Bulutlu", "Hafif yağmurlu" };
            var icons = new[] { "01d", "02d", "03d", "10d" };

            var temp = temps[random.Next(temps.Length)];
            var descIndex = random.Next(descriptions.Length);

            return new WeatherData
            {
                Temperature = temp,
                Description = descriptions[descIndex],
                IconCode = icons[descIndex],
                Humidity = random.Next(40, 80),
                FeelsLike = temp + random.Next(-2, 3),
                WindSpeed = random.Next(0, 15),
                LastUpdated = DateTime.Now
            };
        }

        /// <summary>
        /// Açıklamadan tahmini nem değeri üret
        /// </summary>
        private int EstimateHumidityFromDescription(string description)
        {
            var descLower = description.ToLower(new CultureInfo("tr-TR"));
            
            if (descLower.Contains("yağmur") || descLower.Contains("sağanak"))
                return 85;
            else if (descLower.Contains("sisli") || descLower.Contains("puslu"))
                return 95;
            else if (descLower.Contains("kapalı"))
                return 75;
            else if (descLower.Contains("bulutlu"))
                return 65;
            else if (descLower.Contains("açık"))
                return 50;
            else
                return 60;
        }

        /// <summary>
        /// Açıklamadan tahmini rüzgar hızı üret
        /// </summary>
        private double EstimateWindSpeedFromDescription(string description)
        {
            var descLower = description.ToLower(new CultureInfo("tr-TR"));
            
            if (descLower.Contains("fırtına"))
                return 50;
            else if (descLower.Contains("rüzgar"))
                return 25;
            else if (descLower.Contains("sağanak"))
                return 20;
            else
                return 10;
        }

        /// <summary>
        /// Hava durumu ikonuna göre emoji döndür
        /// </summary>
        public static string GetWeatherEmoji(string iconCode)
        {
            return iconCode switch
            {
                "01d" or "01n" => "☀️",
                "02d" or "02n" => "⛅",
                "03d" or "03n" => "☁️",
                "04d" or "04n" => "☁️",
                "09d" or "09n" => "🌧️",
                "10d" or "10n" => "🌦️",
                "11d" or "11n" => "⛈️",
                "13d" or "13n" => "❄️",
                "50d" or "50n" => "🌫️",
                _ => "🌤️"
            };
        }
    }

    /// <summary>
    /// Hava durumu verisi
    /// </summary>
    public class WeatherData
    {
        public double Temperature { get; set; }
        public string Description { get; set; }
        public string IconCode { get; set; }
        public int Humidity { get; set; }
        public double FeelsLike { get; set; }
        public double WindSpeed { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Source { get; set; } // Veri kaynağı
    }
}