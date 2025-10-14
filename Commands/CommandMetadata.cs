using System;
using System.Collections.Generic;

namespace QuadroAIPilot.Commands
{
    /// <summary>
    /// Komutun odak gereksinimini belirten enum.
    /// </summary>
    public enum CommandFocusType
    {
        /// <summary>
        /// Sistem genelinde çalışan, odak değişimi gerektirmeyen komutlar (örn: ses kontrolü).
        /// </summary>
        SystemWide,

        /// <summary>
        /// Aktif pencerede çalışması gereken komutlar (QuadroAIPilot dışındaki bir pencere).
        /// </summary>
        ActiveWindow,

        /// <summary>
        /// Özel bir uygulamaya özgü komutlar (örn: Outlook, Chrome).
        /// </summary>
        SpecificApp
    }

    /// <summary>
    /// Komutlar için meta veri sınıfı. Komut tanımları, odak gereksinimleri ve diğer 
    /// özellikler bu sınıfta saklanır.
    /// </summary>
    public class CommandMetadata
    {
        /// <summary>
        /// Komutun benzersiz tanımlayıcısı.
        /// </summary>
        public string CommandId { get; set; }

        /// <summary>
        /// Komutun kullanıcı dostu adı.
        /// </summary>
        public string CommandName { get; set; }

        /// <summary>
        /// Komutun tetikleyici ifadeleri. Örneğin: ["e-posta gönder", "e posta gönder", "mail gönder"]
        /// </summary>
        public List<string> CommandTriggers { get; set; } = new List<string>();

        /// <summary>
        /// Komutun odak gereksinimi türü.
        /// </summary>
        public CommandFocusType FocusType { get; set; } = CommandFocusType.ActiveWindow;

        /// <summary>
        /// Belirli bir uygulama için hedef uygulama adı.
        /// SpecificApp odak türü için gereklidir.
        /// </summary>
        public string TargetApplication { get; set; }

        /// <summary>
        /// Komut çalıştırma sonrası QuadroAIPilot'a dönülecek mi?
        /// </summary>
        public bool ReturnFocusAfterExecution { get; set; } = true;

        /// <summary>
        /// Odak değişiminden sonra bekleme süresi (ms cinsinden).
        /// </summary>
        public int DelayAfterFocusChange { get; set; } = 500;

        /// <summary>
        /// Klavye kısayolu veya tuş kombinasyonu (örn: Alt+F4, Ctrl+C).
        /// </summary>
        public string KeyCombination { get; set; }

        /// <summary>
        /// Alternatif klavye kısayolu (bazı uygulamalarda farklı kısayollar olabilir).
        /// </summary>
        public string AlternativeKeyCombination { get; set; }

        /// <summary>
        /// Komut açıklaması.
        /// </summary>
        public string Description { get; set; }
    }
}
