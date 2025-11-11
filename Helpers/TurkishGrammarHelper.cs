using System;
using System.Collections.Generic;
using System.Linq;

namespace QuadroAIPilot.Helpers
{
    /// <summary>
    /// Türkçe dilbilgisi kurallarına göre ek işlemlerini yöneten yardımcı sınıf
    /// </summary>
    public static class TurkishGrammarHelper
    {
        // Sert ünsüzler
        private static readonly HashSet<char> HardConsonants = new HashSet<char> 
        { 
            'ç', 'f', 'h', 'k', 'p', 's', 'ş', 't',
            'Ç', 'F', 'H', 'K', 'P', 'S', 'Ş', 'T'
        };

        // Ünlüler
        private static readonly HashSet<char> Vowels = new HashSet<char> 
        { 
            'a', 'e', 'ı', 'i', 'o', 'ö', 'u', 'ü',
            'A', 'E', 'I', 'İ', 'O', 'Ö', 'U', 'Ü'
        };

        // İnce ünlüler
        private static readonly HashSet<char> ThinVowels = new HashSet<char> 
        { 
            'e', 'i', 'ö', 'ü',
            'E', 'İ', 'Ö', 'Ü'
        };

        /// <summary>
        /// İsimden sonra gelen -den/-dan/-ten/-tan ekini belirler
        /// </summary>
        /// <param name="name">İsim</param>
        /// <returns>Uygun ek (örn: "'den", "'dan", "'ten", "'tan")</returns>
        public static string GetAblativeSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "'den";

            name = name.Trim();
            
            // Son harfi bul
            char lastChar = name[name.Length - 1];
            
            // Son ünlüyü bul
            char? lastVowel = FindLastVowel(name);
            
            // Eğer ünlü ile bitiyorsa
            if (Vowels.Contains(lastChar))
            {
                // İnce ünlü ise -den, kalın ünlü ise -dan
                return ThinVowels.Contains(lastChar) ? "'den" : "'dan";
            }
            
            // Ünsüz ile bitiyorsa
            bool isHardConsonant = HardConsonants.Contains(lastChar);
            
            // Son ünlüye göre ince/kalın belirleme
            if (lastVowel.HasValue)
            {
                bool isThinVowel = ThinVowels.Contains(lastVowel.Value);
                
                // Sert ünsüz + ince ünlü = -ten
                // Sert ünsüz + kalın ünlü = -tan
                // Yumuşak ünsüz + ince ünlü = -den
                // Yumuşak ünsüz + kalın ünlü = -dan
                if (isHardConsonant)
                    return isThinVowel ? "'ten" : "'tan";
                else
                    return isThinVowel ? "'den" : "'dan";
            }
            
            // Varsayılan
            return isHardConsonant ? "'ten" : "'den";
        }

        /// <summary>
        /// İsmin içindeki son ünlüyü bulur
        /// </summary>
        private static char? FindLastVowel(string text)
        {
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (Vowels.Contains(text[i]))
                    return text[i];
            }
            return null;
        }

        /// <summary>
        /// Verilen isimle birlikte ablative eki kullanarak cümle oluşturur
        /// </summary>
        /// <param name="name">İsim</param>
        /// <param name="followingText">Ekten sonra gelen metin</param>
        /// <returns>Tam cümle</returns>
        public static string CreateAblativePhrase(string name, string followingText)
        {
            return $"{name}{GetAblativeSuffix(name)} {followingText}";
        }

        /// <summary>
        /// İsimden sonra gelen -e/-a ekini belirler (yönelme hali)
        /// </summary>
        /// <param name="name">İsim</param>
        /// <returns>Uygun ek (örn: "'e", "'a", "'ye", "'ya")</returns>
        public static string GetDativeSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "'e";

            name = name.Trim();
            
            // Son harfi bul
            char lastChar = name[name.Length - 1];
            
            // Son ünlüyü bul
            char? lastVowel = FindLastVowel(name);
            
            // Eğer ünlü ile bitiyorsa, y buffer harfi eklenir
            if (Vowels.Contains(lastChar))
            {
                // İnce ünlü ise -ye, kalın ünlü ise -ya
                return ThinVowels.Contains(lastChar) ? "'ye" : "'ya";
            }
            
            // Ünsüz ile bitiyorsa, son ünlüye göre belirlenir
            if (lastVowel.HasValue)
            {
                // İnce ünlü ise -e, kalın ünlü ise -a
                return ThinVowels.Contains(lastVowel.Value) ? "'e" : "'a";
            }
            
            // Varsayılan
            return "'e";
        }

        /// <summary>
        /// Verilen isimle birlikte dative eki kullanarak cümle oluşturur (yönelme hali)
        /// </summary>
        /// <param name="name">İsim</param>
        /// <param name="followingText">Ekten sonra gelen metin</param>
        /// <returns>Tam cümle</returns>
        public static string CreateDativePhrase(string name, string followingText)
        {
            return $"{name}{GetDativeSuffix(name)} {followingText}";
        }

        /// <summary>
        /// İsimden sonra gelen -i/-ı/-u/-ü ekini belirler (belirtme hali)
        /// </summary>
        /// <param name="name">İsim</param>
        /// <returns>Uygun ek (örn: "'i", "'ı", "'u", "'ü", "'yi", "'yı", "'yu", "'yü")</returns>
        public static string GetAccusativeSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "'i";

            name = name.Trim();
            
            // Son harfi bul
            char lastChar = name[name.Length - 1];
            
            // Son ünlüyü bul
            char? lastVowel = FindLastVowel(name);
            
            // Eğer ünlü ile bitiyorsa, y buffer harfi eklenir
            if (Vowels.Contains(lastChar))
            {
                if (lastChar == 'a' || lastChar == 'A') return "'yı";
                if (lastChar == 'e' || lastChar == 'E') return "'yi";
                if (lastChar == 'ı' || lastChar == 'I') return "'yı";
                if (lastChar == 'i' || lastChar == 'İ') return "'yi";
                if (lastChar == 'o' || lastChar == 'O') return "'yu";
                if (lastChar == 'ö' || lastChar == 'Ö') return "'yü";
                if (lastChar == 'u' || lastChar == 'U') return "'yu";
                if (lastChar == 'ü' || lastChar == 'Ü') return "'yü";
            }
            
            // Ünsüz ile bitiyorsa, son ünlüye göre belirlenir
            if (lastVowel.HasValue)
            {
                char lv = lastVowel.Value;
                if (lv == 'a' || lv == 'A' || lv == 'ı' || lv == 'I') return "'ı";
                if (lv == 'e' || lv == 'E' || lv == 'i' || lv == 'İ') return "'i";
                if (lv == 'o' || lv == 'O' || lv == 'u' || lv == 'U') return "'u";
                if (lv == 'ö' || lv == 'Ö' || lv == 'ü' || lv == 'Ü') return "'ü";
            }
            
            // Varsayılan
            return "'i";
        }
    }
}