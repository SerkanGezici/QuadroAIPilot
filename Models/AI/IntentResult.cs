using System;
using System.Collections.Generic;

namespace QuadroAIPilot.Models.AI
{
    /// <summary>
    /// AI tarafından analiz edilen komutun sonucu
    /// </summary>
    public class IntentResult
    {
        /// <summary>
        /// Algılanan niyet
        /// </summary>
        public Intent Intent { get; set; }
        
        /// <summary>
        /// Güven skoru (0.0 - 1.0)
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// Orijinal kullanıcı komutu
        /// </summary>
        public string OriginalText { get; set; }
        
        /// <summary>
        /// İşlenmiş/normalize edilmiş komut
        /// </summary>
        public string ProcessedText { get; set; }
        
        /// <summary>
        /// Çıkarılan varlıklar (entities)
        /// Örnek: {"app": "outlook", "action": "aç"}
        /// </summary>
        public Dictionary<string, string> Entities { get; set; }
        
        /// <summary>
        /// Alternatif niyetler (düşük confidence durumunda)
        /// </summary>
        public List<(Intent Intent, double Confidence)> Alternatives { get; set; }
        
        /// <summary>
        /// İşlem süresi (ms)
        /// </summary>
        public long ProcessingTime { get; set; }
        
        public IntentResult()
        {
            Entities = new Dictionary<string, string>();
            Alternatives = new List<(Intent, double)>();
        }
        
        /// <summary>
        /// Sonucun başarılı olup olmadığını kontrol eder
        /// </summary>
        public bool IsSuccessful => Intent.Type != IntentType.Unknown && Confidence > 0.5;
        
        /// <summary>
        /// Debug için string representation
        /// </summary>
        public override string ToString()
        {
            return $"Intent: {Intent.Name} ({Confidence:P}), Entities: {string.Join(", ", Entities)}";
        }
    }
}