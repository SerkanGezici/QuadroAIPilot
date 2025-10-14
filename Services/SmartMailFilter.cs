using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// SmartMailCommandParser sonuçlarına göre mailleri filtreler
    /// Multi-criteria filtering ve fuzzy matching desteği
    /// </summary>
    public class SmartMailFilter
    {
        /// <summary>
        /// Parsed command'a göre mail listesini filtreler
        /// </summary>
        public List<RealOutlookReader.RealEmailInfo> FilterMails(
            List<RealOutlookReader.RealEmailInfo> allMails, 
            SmartMailCommandParser.ParsedMailCommand command)
        {
            try
            {
                var filteredMails = allMails.AsEnumerable();

                // 1. Mail Direction Filter (Sent/Received)
                if (command.IsSentMail.HasValue)
                {
                    filteredMails = filteredMails.Where(m => m.IsSentMail == command.IsSentMail.Value);
                }

                // 2. Person Name Filter (fuzzy matching)
                if (!string.IsNullOrEmpty(command.PersonName))
                {
                    filteredMails = filteredMails.Where(m => 
                        FuzzyMatchPerson(m, command.PersonName, command.IsSentMail));
                }

                // 3. Domain Filter
                if (!string.IsNullOrEmpty(command.DomainName))
                {
                    filteredMails = filteredMails.Where(m => 
                        FuzzyMatchDomain(m, command.DomainName));
                }

                // 4. Subject Filter (fuzzy matching)
                if (!string.IsNullOrEmpty(command.Subject))
                {
                    filteredMails = filteredMails.Where(m => 
                        FuzzyMatchSubject(m.Subject, command.Subject));
                }

                // 5. Date Range Filter
                if (command.StartDate.HasValue && command.EndDate.HasValue)
                {
                    filteredMails = filteredMails.Where(m => 
                        m.ReceivedTime >= command.StartDate.Value && 
                        m.ReceivedTime <= command.EndDate.Value);
                }

                // 6. Read Status Filter
                if (command.IsUnread.HasValue)
                {
                    // IsUnread = true means we want unread mails (IsRead = false)
                    // IsUnread = false means we want read mails (IsRead = true)
                    if (command.IsUnread.Value)
                    {
                        filteredMails = filteredMails.Where(m => !m.IsRead); // unread mails
                    }
                    else
                    {
                        filteredMails = filteredMails.Where(m => m.IsRead);  // read mails
                    }
                }

                // 7. Sort by date (most recent first)
                var result = filteredMails
                    .OrderByDescending(m => m.ReceivedTime)
                    .ToList();

                return result;
            }
            catch (Exception)
            {
                return new List<RealOutlookReader.RealEmailInfo>();
            }
        }

        #region Fuzzy Matching Methods

        /// <summary>
        /// Kişi ismi fuzzy matching - sent/received durumuna göre doğru field'ı kontrol eder
        /// </summary>
        private bool FuzzyMatchPerson(RealOutlookReader.RealEmailInfo mail, string searchName, bool? isSentMail)
        {
            string targetName = "";
            string targetEmail = "";
            
            if (isSentMail == true)
            {
                // Gönderilmiş mailler için alıcı adına ve emailine bak
                targetName = mail.RecipientName ?? "";
                targetEmail = mail.RecipientEmail ?? "";
            }
            else if (isSentMail == false)
            {
                // Gelen mailler için gönderen adına ve emailine bak
                targetName = mail.SenderName ?? "";
                targetEmail = mail.SenderEmail ?? "";
            }
            else
            {
                // Yön belirtilmemişse her ikisine de bak
                targetName = (mail.SenderName ?? "") + " " + (mail.RecipientName ?? "");
                targetEmail = (mail.SenderEmail ?? "") + " " + (mail.RecipientEmail ?? "");
            }
            
            // Normalize search for email matching
            var normalizedSearch = NormalizeTurkish(searchName.ToLowerInvariant());
            
            // Check email with normalized version
            if (targetEmail.ToLowerInvariant().Contains(normalizedSearch))
            {
                return true;
            }

            // Check name with fuzzy matching
            return FuzzyMatchText(targetName, searchName, 0.6);
        }

        /// <summary>
        /// Domain fuzzy matching - email adreslerinde ve sender name'de arar
        /// Türkçe karakterler normalize edilir (günay → gunay)
        /// </summary>
        private bool FuzzyMatchDomain(RealOutlookReader.RealEmailInfo mail, string searchDomain)
        {
            var senderEmail = mail.SenderEmail ?? "";
            var recipientEmail = mail.RecipientEmail ?? "";
            var senderName = mail.SenderName ?? "";
            var recipientName = mail.RecipientName ?? "";
            
            // Normalize search term for email matching (günay → gunay)
            var normalizedSearch = NormalizeTurkish(searchDomain.ToLowerInvariant());
            var originalSearch = searchDomain.ToLowerInvariant();
            
            // Check emails with normalized version (emails don't have Turkish chars)
            if (senderEmail.ToLowerInvariant().Contains(normalizedSearch) ||
                recipientEmail.ToLowerInvariant().Contains(normalizedSearch))
            {
                return true;
            }
            
            // Check display names with both original and normalized versions
            var normalizedSenderName = NormalizeTurkish(senderName.ToLowerInvariant());
            var normalizedRecipientName = NormalizeTurkish(recipientName.ToLowerInvariant());
            
            // Match both original (Günay) and normalized (Gunay) in display names
            bool matchFound = senderName.ToLowerInvariant().Contains(originalSearch) ||
                   normalizedSenderName.Contains(normalizedSearch) ||
                   recipientName.ToLowerInvariant().Contains(originalSearch) ||
                   normalizedRecipientName.Contains(normalizedSearch);
            return matchFound;
        }

        /// <summary>
        /// Subject fuzzy matching
        /// </summary>
        private bool FuzzyMatchSubject(string mailSubject, string searchSubject)
        {
            if (string.IsNullOrEmpty(mailSubject) || string.IsNullOrEmpty(searchSubject))
                return false;

            return FuzzyMatchText(mailSubject, searchSubject, 0.5);
        }

        /// <summary>
        /// Generic fuzzy text matching with Turkish character normalization
        /// </summary>
        private bool FuzzyMatchText(string text1, string text2, double threshold = 0.6)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return false;

            // Normalize Turkish characters
            text1 = NormalizeTurkish(text1.ToLowerInvariant());
            text2 = NormalizeTurkish(text2.ToLowerInvariant());

            // Simple contains check first
            if (text1.Contains(text2) || text2.Contains(text1))
                return true;

            // Levenshtein distance based similarity
            double similarity = CalculateSimilarity(text1, text2);
            return similarity >= threshold;
        }

        /// <summary>
        /// Turkish character normalization
        /// </summary>
        private string NormalizeTurkish(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            
            return input
                .Replace('ı', 'i').Replace('İ', 'I')
                .Replace('ğ', 'g').Replace('Ğ', 'G')
                .Replace('ü', 'u').Replace('Ü', 'U')
                .Replace('ş', 's').Replace('Ş', 'S')
                .Replace('ö', 'o').Replace('Ö', 'O')
                .Replace('ç', 'c').Replace('Ç', 'C');
        }

        /// <summary>
        /// Levenshtein distance similarity calculation
        /// </summary>
        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            if (source.Equals(target, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            int distance = LevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            
            if (maxLength == 0) return 1.0;
            
            return 1.0 - (double)distance / maxLength;
        }

        /// <summary>
        /// Levenshtein distance calculation
        /// </summary>
        private int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;

            int[,] distance = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                distance[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                distance[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[source.Length, target.Length];
        }

        #endregion

        /// <summary>
        /// Filtered sonuçları analiz eder ve özet verir
        /// </summary>
        public string GenerateSearchSummary(List<RealOutlookReader.RealEmailInfo> results, 
            SmartMailCommandParser.ParsedMailCommand command)
        {
            if (!results.Any())
            {
                return "Arama kriterlerinize uygun e posta bulunamadı.";
            }

            string summary = $"{results.Count} e posta bulundu";
            
            if (!string.IsNullOrEmpty(command.PersonName))
                summary += $", {command.PersonName} ile";
            
            if (!string.IsNullOrEmpty(command.DomainName))
                summary += $", {command.DomainName} domaininden";
                
            if (!string.IsNullOrEmpty(command.Subject))
                summary += $", '{command.Subject}' konulu";
                
            if (command.IsSentMail == true)
                summary += ", gönderilmiş";
            else if (command.IsSentMail == false)
                summary += ", gelen";
                
            if (command.IsUnread == false)
                summary += ", okunmamış";

            summary += ".";
            
            return summary;
        }
    }
}