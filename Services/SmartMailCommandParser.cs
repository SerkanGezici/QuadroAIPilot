using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Natural language mail komutlarını ayrıştırır
    /// Örnek: "serkan geziciye gönderdiğim son maili oku"
    /// </summary>
    public class SmartMailCommandParser
    {
        public class ParsedMailCommand
        {
            public string PersonName { get; set; } = "";
            public string DomainName { get; set; } = "";
            public string Subject { get; set; } = "";
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public bool? IsSentMail { get; set; }
            public bool? IsUnread { get; set; }
            public string Action { get; set; } = ""; // "oku", "göster", "listele"
            public bool IsValid { get; set; }
            public string OriginalCommand { get; set; } = "";
        }

        /// <summary>
        /// Ana parsing methodu
        /// </summary>
        public ParsedMailCommand ParseCommand(string command)
        {
            var result = new ParsedMailCommand
            {
                OriginalCommand = command
            };

            try
            {
                command = command.ToLowerInvariant().Trim();

                // Domain çıkarma (günaydan gelen, amazondan gelen)
                result.DomainName = ExtractDomain(command);
                
                // Kişi ismi çıkarma - SADECE domain bulunamadıysa
                // Çünkü "günaydan gelen" hem domain hem person olabilir
                if (string.IsNullOrEmpty(result.DomainName))
                {
                    result.PersonName = ExtractPersonName(command);
                }
                
                // Konu çıkarma
                result.Subject = ExtractSubject(command);
                
                // Tarih aralığı çıkarma
                ExtractDateRange(command, result);
                
                // Mail yönü (gönderilmiş/gelen)
                result.IsSentMail = ExtractMailDirection(command);
                
                // Okunma durumu
                result.IsUnread = ExtractReadStatus(command);
                
                // Aksiyon türü
                result.Action = ExtractAction(command);

                // Validasyon
                result.IsValid = ValidateCommand(result);

                return result;
            }
            catch (Exception)
            {
                result.IsValid = false;
                return result;
            }
        }

        /// <summary>
        /// Komutun smart mail komutu olup olmadığını kontrol eder
        /// </summary>
        public bool IsSmartMailCommand(string command)
        {
            command = command.ToLowerInvariant();
            
            // Smart mail command patterns
            var patterns = new[]
            {
                @"\w+('ye|'e|'a)\s+(gönderdiğim|gönderilen)",  // "serkan'a gönderdiğim"
                @"\w+(ye|e|a)\s+(gönderdiğim|gönderilen)",     // "serkana gönderdiğim" (apostrofsuz)
                @"\w+('den|'dan|'ten|'tan)\s+(gelen)",         // "amazon'dan gelen"
                @"\w+(dan|den|tan|ten)\s+(gelen)",             // "amazondan gelen" (apostrofsuz)
                @"\w+(dan|den|tan|ten)\s+gelen\s+son\s+(mail|e\s*posta)",  // "amazondan gelen son mail"
                @"(son|geçen|bu|dün|bugün).*(mail|maili)",     // "son maili", "geçen hafta maili"
                @"(konulu|konu)\s+mail",                       // "fiyat teklifi konulu mail"
                @"okunmamış.*(mail|maili)"                     // "okunmamış maili"
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(command, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        #region Private Helper Methods

        private string ExtractPersonName(string command)
        {
            // Pattern: "serkan gezici'ye" veya "serkan geziciye gönderdiğim"
            var patterns = new[]
            {
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ\s]+)('ye|'e|'a)\s+(gönderdiğim|gönderilen)",     // apostroflu
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ\s]+)(ye|e|a)\s+(gönderdiğim|gönderilen)",        // apostrofsuz
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ\s]+)('den|'dan|'ten|'tan)\s+(gelen)",            // apostroflu
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ\s]+)(dan|den|tan|ten)\s+(gelen)"                 // apostrofsuz
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(command, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return "";
        }

        private string ExtractDomain(string command)
        {
            // Pattern: "amazon'dan gelen", "günaydan gelen" veya "microsoft'tan gelen"
            // Not: Domain yerine person name de olabilir (günay, şükrü, çağrı gibi)
            var patterns = new[]
            {
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ]+)('den|'dan|'ten|'tan)\s+(gelen)",  // apostroflu
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ]+)(dan|den|tan|ten)\s+(gelen)"       // apostrofsuz
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(command, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var extractedValue = match.Groups[1].Value.Trim();
                    return extractedValue;
                }
            }

            return "";
        }

        private string ExtractSubject(string command)
        {
            // Pattern: "fiyat teklifi konulu" veya "toplantı konu"
            var patterns = new[]
            {
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ\s]+)\s+(konulu|konu)\s+mail",
                @"([a-zA-ZğĞüÜşŞıİöÖçÇ\s]+)\s+(hakkında|ile\s+ilgili)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(command, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return "";
        }

        private void ExtractDateRange(string command, ParsedMailCommand result)
        {
            var now = DateTime.Now;

            if (command.Contains("dün"))
            {
                result.StartDate = now.Date.AddDays(-1);
                result.EndDate = now.Date.AddDays(-1).AddHours(23).AddMinutes(59);
            }
            else if (command.Contains("bugün"))
            {
                result.StartDate = now.Date;
                result.EndDate = now.Date.AddHours(23).AddMinutes(59);
            }
            else if (command.Contains("geçen hafta"))
            {
                var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek - 7);
                result.StartDate = startOfWeek;
                result.EndDate = startOfWeek.AddDays(6).AddHours(23).AddMinutes(59);
            }
            else if (command.Contains("bu hafta"))
            {
                var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
                result.StartDate = startOfWeek;
                result.EndDate = now;
            }
            else if (command.Contains("geçen ay"))
            {
                var firstDayOfLastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                result.StartDate = firstDayOfLastMonth;
                result.EndDate = firstDayOfLastMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59);
            }
            else if (command.Contains("bu ay"))
            {
                result.StartDate = new DateTime(now.Year, now.Month, 1);
                result.EndDate = now;
            }
            else if (command.Contains("son"))
            {
                // "son mail" - en son 30 gün
                result.StartDate = now.AddDays(-30);
                result.EndDate = now;
            }
        }

        private bool? ExtractMailDirection(string command)
        {
            if (command.Contains("gönderdiğim") || command.Contains("gönderilen"))
            {
                return true; // Sent mail
            }
            
            if (command.Contains("gelen") || command.Contains("aldığım"))
            {
                return false; // Received mail
            }

            return null; // Both directions
        }

        private bool? ExtractReadStatus(string command)
        {
            if (command.Contains("okunmamış"))
            {
                return true; // true = want unread mails
            }
            
            if (command.Contains("okunmuş"))
            {
                return false; // false = want read mails
            }

            return null; // Both read/unread
        }

        private string ExtractAction(string command)
        {
            if (command.Contains("oku"))
                return "oku";
            
            if (command.Contains("göster") || command.Contains("listele"))
                return "göster";
                
            if (command.Contains("bul") || command.Contains("ara"))
                return "bul";

            return "oku"; // Default action
        }

        private bool ValidateCommand(ParsedMailCommand command)
        {
            // En az bir filtre kriteri olmalı
            return !string.IsNullOrEmpty(command.PersonName) ||
                   !string.IsNullOrEmpty(command.DomainName) ||
                   !string.IsNullOrEmpty(command.Subject) ||
                   command.StartDate.HasValue ||
                   command.IsSentMail.HasValue ||
                   command.IsUnread.HasValue;
        }

        #endregion
    }
}