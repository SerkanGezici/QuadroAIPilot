using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Kullanıcının yazım stilini öğrenen ve cevap önerileri sunan servis
    /// </summary>
    public class ResponseLearningService
    {
        private static ResponseLearningService _instance;
        private static readonly object _lock = new object();
        
        private readonly string _dataFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuadroAIPilot", "response_patterns.json");
        private UserWritingProfile _userProfile;
        
        public static ResponseLearningService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new ResponseLearningService();
                    }
                }
                return _instance;
            }
        }
        
        public class UserWritingProfile
        {
            public List<string> CommonPhrases { get; set; } = new List<string>();
            public List<string> Greetings { get; set; } = new List<string>();
            public List<string> Closings { get; set; } = new List<string>();
            public List<string> TurkishHonorific { get; set; } = new List<string>(); // Bey/Hanım kullanımı
            public string PreferredTone { get; set; } = "Professional"; // Professional, Casual, Formal
            public bool UsesShortSentences { get; set; } = true;
            public bool UsesTurkishFormality { get; set; } = true;
            public Dictionary<string, int> WordFrequency { get; set; } = new Dictionary<string, int>();
            public DateTime LastUpdated { get; set; } = DateTime.Now;
        }
        
        private ResponseLearningService()
        {
            LoadUserProfile();
        }
        
        /// <summary>
        /// Kullanıcının verdiği cevabı analiz eder ve profili günceller
        /// </summary>
        public async Task LearnFromUserResponse(string userResponse, string originalEmailSubject = "")
        {
            try
            {
                Debug.WriteLine($"[ResponseLearning] Kullanıcı yanıtı öğreniliyor: {userResponse}");
                
                // Yazım tarzı analizi
                AnalyzeWritingStyle(userResponse);
                
                // Kelime sıklığı analizi
                AnalyzeWordFrequency(userResponse);
                
                // Ton analizi
                AnalyzeTone(userResponse);
                
                // Türkçe formalite analizi
                AnalyzeTurkishFormality(userResponse);
                
                _userProfile.LastUpdated = DateTime.Now;
                
                await SaveUserProfile();
                
                Debug.WriteLine("[ResponseLearning] Kullanıcı profili güncellendi");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResponseLearning] Öğrenme hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Öğrenilen stile göre cevap önerisi oluşturur
        /// </summary>
        public string GenerateResponseSuggestion(string userInput, string senderName, string emailSubject)
        {
            try
            {
                Debug.WriteLine($"[ResponseLearning] Cevap önerisi oluşturuluyor...");
                
                var response = "";
                
                // Selamlama
                response += GetPersonalizedGreeting(senderName);
                response += "\n\n";
                
                // Ana içerik (kullanıcının söylediklerini işle)
                response += ProcessUserContent(userInput);
                response += "\n\n";
                
                // Kapanış
                response += GetPersonalizedClosing();
                
                Debug.WriteLine($"[ResponseLearning] Önerilen cevap: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResponseLearning] Cevap önerisi hatası: {ex.Message}");
                return userInput; // Fallback olarak kullanıcının girdiğini döndür
            }
        }
        
        /// <summary>
        /// Kullanıcının profil özetini döndürür
        /// </summary>
        public string GetUserStyleSummary()
        {
            var summary = "📝 Yazım Stili Profili:\n";
            summary += $"• Ton: {_userProfile.PreferredTone}\n";
            summary += $"• Kısa cümleler: {(_userProfile.UsesShortSentences ? "Evet" : "Hayır")}\n";
            summary += $"• Türkçe formalite: {(_userProfile.UsesTurkishFormality ? "Evet" : "Hayır")}\n";
            summary += $"• En çok kullanılan kelimeler: {string.Join(", ", _userProfile.WordFrequency.OrderByDescending(w => w.Value).Take(5).Select(w => w.Key))}\n";
            summary += $"• Son güncelleme: {_userProfile.LastUpdated:dd.MM.yyyy HH:mm}";
            
            return summary;
        }
        
        private void AnalyzeWritingStyle(string text)
        {
            // Cümle uzunluğu analizi
            var sentences = text.Split('.', '!', '?').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            double avgSentenceLength = sentences.Length > 0 ? sentences.Average(s => s.Split(' ').Length) : 0;
            _userProfile.UsesShortSentences = avgSentenceLength < 10;
            
            // Ortak ifadeler
            var commonPhrases = new[] { "saygılarımla", "iyi günler", "teşekkürler", "rica ederim" };
            foreach (var phrase in commonPhrases)
            {
                if (text.ToLowerInvariant().Contains(phrase) && !_userProfile.CommonPhrases.Contains(phrase))
                {
                    _userProfile.CommonPhrases.Add(phrase);
                }
            }
        }
        
        private void AnalyzeWordFrequency(string text)
        {
            var words = text.ToLowerInvariant()
                .Split(' ', '.', ',', '!', '?', ';', ':')
                .Where(w => w.Length > 2)
                .ToArray();
                
            foreach (var word in words)
            {
                if (_userProfile.WordFrequency.ContainsKey(word))
                    _userProfile.WordFrequency[word]++;
                else
                    _userProfile.WordFrequency[word] = 1;
            }
        }
        
        private void AnalyzeTone(string text)
        {
            var lowerText = text.ToLowerInvariant();
            
            var formalWords = new[] { "saygılarımla", "sayın", "arz ederim", "rica ederim" };
            var casualWords = new[] { "merhaba", "selam", "naber", "görüşürüz" };
            
            int formalScore = formalWords.Count(word => lowerText.Contains(word));
            int casualScore = casualWords.Count(word => lowerText.Contains(word));
            
            if (formalScore > casualScore)
                _userProfile.PreferredTone = "Formal";
            else if (casualScore > formalScore)
                _userProfile.PreferredTone = "Casual";
            else
                _userProfile.PreferredTone = "Professional";
        }
        
        private void AnalyzeTurkishFormality(string text)
        {
            var turkishHonorifics = new[] { " bey", " hanım", " efendi" };
            var hasHonorific = turkishHonorifics.Any(h => text.ToLowerInvariant().Contains(h));
            
            if (hasHonorific)
            {
                _userProfile.UsesTurkishFormality = true;
                
                // Hangi honorific'leri kullandığını kaydet
                foreach (var honorific in turkishHonorifics)
                {
                    if (text.ToLowerInvariant().Contains(honorific) && !_userProfile.TurkishHonorific.Contains(honorific.Trim()))
                    {
                        _userProfile.TurkishHonorific.Add(honorific.Trim());
                    }
                }
            }
        }
        
        private string GetPersonalizedGreeting(string senderName)
        {
            string greeting = "";
            
            if (_userProfile.UsesTurkishFormality && !string.IsNullOrEmpty(senderName))
            {
                // İsimden cinsiyet tahmin etmeye çalış (basit)
                var femaleNames = new[] { "ayşe", "fatma", "zeynep", "elif", "seda" };
                var isFemale = femaleNames.Any(name => senderName.ToLowerInvariant().Contains(name));
                
                if (_userProfile.TurkishHonorific.Contains("hanım") && isFemale)
                    greeting = $"Sayın {senderName} Hanım,";
                else if (_userProfile.TurkishHonorific.Contains("bey") && !isFemale)
                    greeting = $"Sayın {senderName} Bey,";
                else
                    greeting = $"Sayın {senderName},";
            }
            else if (_userProfile.PreferredTone == "Formal")
            {
                greeting = $"Sayın {senderName},";
            }
            else if (_userProfile.PreferredTone == "Casual")
            {
                greeting = $"Merhaba {senderName},";
            }
            else
            {
                greeting = $"Sayın {senderName},";
            }
            
            return greeting;
        }
        
        private string ProcessUserContent(string userInput)
        {
            // Kullanıcının söylediklerini profesyonel bir dile çevir
            var content = userInput;
            
            // Kısa cümleler tercih ediyorsa, uzun cümleleri böl
            if (_userProfile.UsesShortSentences)
            {
                // Basit noktalama ekle
                content = content.Replace(" ve ", ". ");
                content = content.Replace(" ayrıca ", ". Ayrıca ");
            }
            
            // Kullanıcının sık kullandığı kelimeleri dahil et
            var topWords = _userProfile.WordFrequency.OrderByDescending(w => w.Value).Take(3).Select(w => w.Key);
            
            return content;
        }
        
        private string GetPersonalizedClosing()
        {
            if (_userProfile.CommonPhrases.Contains("saygılarımla"))
                return "Saygılarımla,";
            else if (_userProfile.PreferredTone == "Casual")
                return "İyi günler,";
            else if (_userProfile.PreferredTone == "Formal")
                return "Saygılarımızla,";
            else
                return "Saygılarımla,";
        }
        
        private void LoadUserProfile()
        {
            try
            {
                if (File.Exists(_dataFile))
                {
                    var json = File.ReadAllText(_dataFile);
                    _userProfile = JsonSerializer.Deserialize<UserWritingProfile>(json) ?? new UserWritingProfile();
                    Debug.WriteLine("[ResponseLearning] Kullanıcı profili yüklendi");
                }
                else
                {
                    _userProfile = new UserWritingProfile();
                    // Debug.WriteLine("[ResponseLearning] Yeni kullanıcı profili oluşturuldu");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResponseLearning] Profil yükleme hatası: {ex.Message}");
                _userProfile = new UserWritingProfile();
            }
        }
        
        private async Task SaveUserProfile()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_dataFile));
                var json = JsonSerializer.Serialize(_userProfile, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_dataFile, json);
                Debug.WriteLine("[ResponseLearning] Kullanıcı profili kaydedildi");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResponseLearning] Profil kaydetme hatası: {ex.Message}");
            }
        }
    }
}