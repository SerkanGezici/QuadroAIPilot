using System;
using System.IO;

namespace QuadroAIPilot.Models
{
    /// <summary>
    /// Dosya arama sonuçlarını temsil eden model
    /// </summary>
    public class FileSearchResult
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Directory { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }
        public double MatchScore { get; set; } // Eşleşme skoru (fuzzy matching için)
        public int SearchPriority { get; set; } // Arama önceliği (MRU, Recent vs.)
        
        public FileSearchResult()
        {
        }
        
        public FileSearchResult(string filePath)
        {
            FilePath = filePath;
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                FileName = fileInfo.Name;
                Directory = fileInfo.DirectoryName;
                Extension = fileInfo.Extension.TrimStart('.');
                Size = fileInfo.Length;
                LastModified = fileInfo.LastWriteTime;
                LastAccessed = fileInfo.LastAccessTime;
            }
        }
        
        /// <summary>
        /// Dosya boyutunu okunabilir formata çevirir
        /// </summary>
        public string GetFormattedSize()
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = Size;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// Dosya tipine göre emoji döndürür
        /// </summary>
        public string GetFileIcon()
        {
            return Extension?.ToLowerInvariant() switch
            {
                "xlsx" or "xls" or "csv" or "xlsm" => "📊",
                "docx" or "doc" or "rtf" or "odt" => "📄",
                "pptx" or "ppt" or "pps" or "ppsx" => "📽️",
                "pdf" or "xps" => "📕",
                "txt" or "log" or "md" => "📝",
                "jpg" or "jpeg" or "png" or "gif" or "bmp" or "svg" => "🖼️",
                "mp4" or "avi" or "mkv" or "mov" or "wmv" => "🎬",
                "mp3" or "wav" or "m4a" or "flac" => "🎵",
                "zip" or "rar" or "7z" or "tar" or "gz" => "📦",
                "exe" or "msi" => "⚙️",
                "dll" or "sys" => "🔧",
                "cs" or "cpp" or "js" or "py" or "java" => "💻",
                "html" or "css" or "xml" or "json" => "🌐",
                _ => "📄"
            };
        }
    }

    /// <summary>
    /// Klasör arama sonuçlarını temsil eden model
    /// </summary>
    public class FolderSearchResult
    {
        public string FolderPath { get; set; }
        public string FolderName { get; set; }
        public string ParentDirectory { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }
        public int FileCount { get; set; }
        public int SubFolderCount { get; set; }
        public long TotalSize { get; set; }
        public double MatchScore { get; set; }
        
        public FolderSearchResult()
        {
        }
        
        public FolderSearchResult(string folderPath)
        {
            FolderPath = folderPath;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                var dirInfo = new DirectoryInfo(folderPath);
                FolderName = dirInfo.Name;
                ParentDirectory = dirInfo.Parent?.FullName;
                LastModified = dirInfo.LastWriteTime;
                LastAccessed = dirInfo.LastAccessTime;
                
                try
                {
                    FileCount = dirInfo.GetFiles().Length;
                    SubFolderCount = dirInfo.GetDirectories().Length;
                    // Boyut hesaplama (performans için sadece ilk seviye)
                    TotalSize = 0;
                    foreach (var file in dirInfo.GetFiles())
                    {
                        TotalSize += file.Length;
                    }
                }
                catch
                {
                    // Erişim hatalarını yoksay
                }
            }
        }
        
        /// <summary>
        /// Klasör boyutunu okunabilir formata çevirir
        /// </summary>
        public string GetFormattedSize()
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = TotalSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// Klasör ikonu
        /// </summary>
        public string GetFolderIcon()
        {
            // Özel klasör adlarına göre ikon
            var lowerName = FolderName?.ToLowerInvariant() ?? "";
            
            if (lowerName.Contains("download") || lowerName.Contains("indirilen"))
                return "⬇️";
            if (lowerName.Contains("document") || lowerName.Contains("belge"))
                return "📑";
            if (lowerName.Contains("picture") || lowerName.Contains("resim") || lowerName.Contains("photo"))
                return "🖼️";
            if (lowerName.Contains("music") || lowerName.Contains("müzik"))
                return "🎵";
            if (lowerName.Contains("video"))
                return "📹";
            if (lowerName.Contains("desktop") || lowerName.Contains("masaüstü"))
                return "🖥️";
            if (lowerName.Contains("backup") || lowerName.Contains("yedek"))
                return "💾";
            if (lowerName.Contains("project") || lowerName.Contains("proje"))
                return "📂";
            if (lowerName.Contains("temp") || lowerName.Contains("geçici"))
                return "⏳";
                
            return "📁";
        }
    }
}