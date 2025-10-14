using System;
using System.Collections.Generic;

namespace QuadroAIPilot.Models.AI
{
    /// <summary>
    /// Kullanıcı davranış profili - öğrenme sistemi için
    /// </summary>
    public class UserProfile
    {
        /// <summary>
        /// Profil ID
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Profil oluşturma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Son güncelleme tarihi
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// Komut kullanım sıklığı
        /// Key: komut metni, Value: kullanım sayısı
        /// </summary>
        public Dictionary<string, int> CommandFrequency { get; set; }
        
        /// <summary>
        /// Saat bazlı komut pattern'leri
        /// Key: saat aralığı (örn: "09:00-10:00"), Value: o saatte kullanılan komutlar
        /// </summary>
        public Dictionary<string, List<string>> TimeBasedPatterns { get; set; }
        
        /// <summary>
        /// Kullanıcının özel komut eşlemeleri
        /// Key: kullanıcı komutu, Value: sistem komutu
        /// Örnek: {"m": "mail aç", "exc": "excel aç"}
        /// </summary>
        public Dictionary<string, string> CustomMappings { get; set; }
        
        /// <summary>
        /// Başarılı komut zincirleri (ardışık komutlar)
        /// Örnek: ["outlook aç", "yeni mail"] sıklıkla beraber kullanılır
        /// </summary>
        public List<CommandSequence> CommandSequences { get; set; }
        
        /// <summary>
        /// Hatalı komutlar ve düzeltmeleri
        /// Key: hatalı komut, Value: düzeltilmiş komut
        /// </summary>
        public Dictionary<string, string> ErrorCorrections { get; set; }
        
        /// <summary>
        /// Favori uygulamalar (sıklıkla açılan)
        /// </summary>
        public List<string> FavoriteApplications { get; set; }
        
        /// <summary>
        /// Toplam komut sayısı
        /// </summary>
        public int TotalCommands { get; set; }
        
        /// <summary>
        /// Başarı oranı (%)
        /// </summary>
        public double SuccessRate { get; set; }
        
        public UserProfile()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.Now;
            LastUpdated = DateTime.Now;
            CommandFrequency = new Dictionary<string, int>();
            TimeBasedPatterns = new Dictionary<string, List<string>>();
            CustomMappings = new Dictionary<string, string>();
            CommandSequences = new List<CommandSequence>();
            ErrorCorrections = new Dictionary<string, string>();
            FavoriteApplications = new List<string>();
        }
    }
    
    /// <summary>
    /// Ardışık komut dizisi
    /// </summary>
    public class CommandSequence
    {
        public List<string> Commands { get; set; }
        public int Frequency { get; set; }
        public TimeSpan AverageTimeBetween { get; set; }
        
        public CommandSequence()
        {
            Commands = new List<string>();
        }
    }
}