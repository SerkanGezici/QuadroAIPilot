using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using QuadroAIPilot.Models;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Kullanıcı dostu hata mesajları ve çözüm önerileri sağlar
    /// </summary>
    public static class ErrorFeedbackService
    {
        private static readonly Dictionary<string, ErrorSuggestion> _errorMappings = new()
        {
            // Haber alma hataları
            {
                "WebContentService", new ErrorSuggestion
                {
                    UserMessage = "Haberler alınırken bağlantı sorunu yaşandı.",
                    VoiceMessage = "Üzgünüm, şu anda haberlere erişemiyorum. İnternet bağlantınızı kontrol edip tekrar deneyin.",
                    Suggestions = new[]
                    {
                        "İnternet bağlantınızı kontrol edin",
                        "Birkaç dakika sonra tekrar deneyin",
                        "Farklı bir haber kategorisi isteyin"
                    }
                }
            },
            
            // Intent algılama hataları
            {
                "IntentDetection", new ErrorSuggestion
                {
                    UserMessage = "Komutunuz anlaşılamadı.",
                    VoiceMessage = "Üzgünüm, ne yapmak istediğinizi anlayamadım. Lütfen daha açık bir şekilde söyleyebilir misiniz?",
                    Suggestions = new[]
                    {
                        "Daha basit ve açık komutlar kullanın",
                        "'Haberler' veya 'Son dakika' gibi net kelimeler söyleyin",
                        "'Türkiye nedir' gibi direkt sorular sorun"
                    }
                }
            },
            
            // Uygulama açma hataları
            {
                "ApplicationLaunch", new ErrorSuggestion
                {
                    UserMessage = "Uygulama açılamadı.",
                    VoiceMessage = "Üzgünüm, istediğiniz uygulamayı açamadım. Uygulama kurulu olmayabilir.",
                    Suggestions = new[]
                    {
                        "Uygulama adını doğru söylediğinizden emin olun",
                        "Uygulamanın bilgisayarınızda kurulu olduğunu kontrol edin",
                        "Farklı bir uygulama adı deneyin"
                    }
                }
            },
            
            // Dosya arama hataları
            {
                "FileSearch", new ErrorSuggestion
                {
                    UserMessage = "Dosya bulunamadı.",
                    VoiceMessage = "Aradığınız dosya bulunamadı. Dosya adını tekrar kontrol edin.",
                    Suggestions = new[]
                    {
                        "Dosya adının doğru yazıldığından emin olun",
                        "Dosyanın silinmediğini kontrol edin",
                        "Farklı klasörlerde arama yapın"
                    }
                }
            },
            
            // Email hataları
            {
                "EmailOperation", new ErrorSuggestion
                {
                    UserMessage = "Email işlemi gerçekleştirilemedi.",
                    VoiceMessage = "Email işlemi sırasında bir sorun oluştu. Outlook açık olduğundan emin olun.",
                    Suggestions = new[]
                    {
                        "Outlook uygulamasının açık olduğunu kontrol edin",
                        "Email hesabınızın aktif olduğunu kontrol edin",
                        "İnternet bağlantınızı kontrol edin"
                    }
                }
            },
            
            // Twitter/X trend hataları
            {
                "TwitterTrends", new ErrorSuggestion
                {
                    UserMessage = "Twitter gündem konuları alınamadı.",
                    VoiceMessage = "Şu anda Twitter gündem konularına erişemiyorum. Daha sonra tekrar deneyin.",
                    Suggestions = new[]
                    {
                        "İnternet bağlantınızı kontrol edin",
                        "Birkaç dakika sonra tekrar deneyin",
                        "Genel haberler isteyebilirsiniz"
                    }
                }
            },
            
            // Wikipedia kaldırıldı - artık araştırmalar AI modu üzerinden yapılıyor
        };

        /// <summary>
        /// Hata türüne göre kullanıcı dostu mesaj ve öneriler döndürür
        /// </summary>
        public static ErrorSuggestion GetErrorSuggestion(string errorType, Exception? exception = null)
        {
            // Özel hata tipi varsa kullan
            if (_errorMappings.TryGetValue(errorType, out var suggestion))
            {
                return suggestion;
            }

            // Exception tipine göre hata türü belirle
            if (exception != null)
            {
                return GetSuggestionFromException(exception);
            }

            // Genel hata mesajı
            return new ErrorSuggestion
            {
                UserMessage = "Beklenmeyen bir hata oluştu.",
                VoiceMessage = "Üzgünüm, bir sorun oluştu. Lütfen tekrar deneyin.",
                Suggestions = new[]
                {
                    "Komutu tekrar söyleyin",
                    "Farklı bir komut deneyin",
                    "Uygulamayı yeniden başlatın"
                }
            };
        }

        /// <summary>
        /// Exception tipinden hata önerisi oluşturur
        /// </summary>
        private static ErrorSuggestion GetSuggestionFromException(Exception exception)
        {
            return exception switch
            {
                System.Net.Http.HttpRequestException => new ErrorSuggestion
                {
                    UserMessage = "İnternet bağlantısı sorunu.",
                    VoiceMessage = "İnternet bağlantınızda sorun var. Bağlantınızı kontrol edip tekrar deneyin.",
                    Suggestions = new[]
                    {
                        "İnternet bağlantınızı kontrol edin",
                        "WiFi veya ethernet kablonuzu kontrol edin",
                        "Birkaç dakika sonra tekrar deneyin"
                    }
                },
                
                TimeoutException => new ErrorSuggestion
                {
                    UserMessage = "İşlem zaman aşımına uğradı.",
                    VoiceMessage = "İşlem çok uzun sürdü ve zaman aşımına uğradı. Tekrar deneyin.",
                    Suggestions = new[]
                    {
                        "İnternet bağlantınızın hızını kontrol edin",
                        "Daha sonra tekrar deneyin",
                        "Farklı bir komut deneyin"
                    }
                },
                
                UnauthorizedAccessException => new ErrorSuggestion
                {
                    UserMessage = "Yetki sorunu.",
                    VoiceMessage = "Bu işlem için yeterli yetkiniz yok. Yönetici olarak çalıştırın.",
                    Suggestions = new[]
                    {
                        "Uygulamayı yönetici olarak çalıştırın",
                        "Dosya izinlerini kontrol edin",
                        "Farklı bir dosya veya klasör deneyin"
                    }
                },
                
                System.IO.FileNotFoundException => new ErrorSuggestion
                {
                    UserMessage = "Dosya bulunamadı.",
                    VoiceMessage = "Aradığınız dosya bulunamadı. Dosya adını kontrol edin.",
                    Suggestions = new[]
                    {
                        "Dosya adını doğru yazdığınızdan emin olun",
                        "Dosyanın silinmediğini kontrol edin",
                        "Farklı klasörlerde arama yapın"
                    }
                },
                
                ArgumentException => new ErrorSuggestion
                {
                    UserMessage = "Geçersiz komut.",
                    VoiceMessage = "Komutunuzda bir hata var. Lütfen tekrar deneyin.",
                    Suggestions = new[]
                    {
                        "Komut formatını kontrol edin",
                        "Daha basit komutlar kullanın",
                        "Örnek komutları deneyin"
                    }
                },
                
                _ => new ErrorSuggestion
                {
                    UserMessage = "Beklenmeyen hata oluştu.",
                    VoiceMessage = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.",
                    Suggestions = new[]
                    {
                        "Komutu tekrar söyleyin",
                        "Uygulamayı yeniden başlatın",
                        "Farklı bir komut deneyin"
                    }
                }
            };
        }

        /// <summary>
        /// Hata raporlama ve logging
        /// </summary>
        public static void LogError(string errorType, Exception exception, string userCommand = "")
        {
            try
            {
                Debug.WriteLine($"[ErrorFeedbackService] Error Type: {errorType}");
                Debug.WriteLine($"[ErrorFeedbackService] User Command: {userCommand}");
                Debug.WriteLine($"[ErrorFeedbackService] Exception: {exception.Message}");
                Debug.WriteLine($"[ErrorFeedbackService] Stack Trace: {exception.StackTrace}");
                
                // Future: Bu bilgileri telemetri servisine gönderebiliriz
                // TelemetryService.TrackError(errorType, exception, userCommand);
            }
            catch
            {
                // Logging hatası sessizce geçilir
            }
        }

        /// <summary>
        /// Komut önerileri getirir
        /// </summary>
        public static string[] GetCommandSuggestions(string failedCommand = "")
        {
            var suggestions = new List<string>
            {
                "Haberler",
                "Son dakika haberler",
                "Twitter gündem",
                "Teknoloji haberleri",
                "Türkiye nedir",
                "Outlook aç",
                "Word aç",
                "Dosya ara",
                "Mail oku"
            };

            // Failed komuta benzer öneriler ekle
            if (!string.IsNullOrEmpty(failedCommand))
            {
                var lowerCommand = failedCommand.ToLowerInvariant();
                
                if (lowerCommand.Contains("haber"))
                {
                    suggestions.Insert(0, "En son haberler");
                    suggestions.Insert(1, "Bugünkü haberler");
                }
                else if (lowerCommand.Contains("twitter") || lowerCommand.Contains("gündem"))
                {
                    suggestions.Insert(0, "Twitter gündem");
                    suggestions.Insert(1, "X trendler");
                }
                else if (lowerCommand.Contains("nedir") || lowerCommand.Contains("kimdir"))
                {
                    suggestions.Insert(0, "Wikipedia'da ara");
                    suggestions.Insert(1, "Bilgi ara");
                }
            }

            return suggestions.Take(6).ToArray();
        }
    }

    /// <summary>
    /// Hata önerisi modeli
    /// </summary>
    public class ErrorSuggestion
    {
        public string UserMessage { get; set; } = "";
        public string VoiceMessage { get; set; } = "";
        public string[] Suggestions { get; set; } = Array.Empty<string>();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}