// FileSearchService.cs – Geliştirilmiş MRU desteği ve Windows Recent Items erişimi ile
// Optimize edilmiş versiyonu
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.Storage.FileProperties;
using Windows.System;
using QuadroAIPilot.Services;
using QuadroAIPilot.Interfaces;
using Microsoft.Win32;

namespace QuadroAIPilot.Services
{
   public sealed class FileSearchService : IFileSearchService
   {
       private const int TimeoutMs = 8000; // arama üst sınırı

       // Özel klasörler listesi 
       private static readonly string[] _specialFolders;
       private static readonly string _recentItemsPath;

       // Eşleşme türleri
       private enum MatchType
       {
           ExactWord,  // Tam kelime eşleşmesi
           Contains,   // İçeren eşleşme
           Fuzzy       // Bulanık eşleşme (Levenshtein distance)
       }

       static FileSearchService()
       {
           var baseSpecialFolders = new List<string>
           {
               Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
               Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
               Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
               Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
           };

           // Sadece Desktop ve Documents kök klasörlerini ekle (alt klasörler eklenmez)
           string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
           baseSpecialFolders.Add(desktopPath);

           string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
           baseSpecialFolders.Add(documentsPath);

           _specialFolders = baseSpecialFolders.ToArray();
           _recentItemsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\Recent");

           
       }

       private static IEnumerable<string> GetAllSubdirectories(string path)
       {
           if (!Directory.Exists(path)) return Enumerable.Empty<string>();

           var result = new List<string>();
           try
           {
               foreach (var dir in Directory.GetDirectories(path))
               {
                   try
                   {
                       result.Add(dir);
                       result.AddRange(GetAllSubdirectories(dir));
                   }
                   catch (UnauthorizedAccessException)
                   {
                       continue;
                   }
                   catch (Exception ex) when (ex is IOException || ex is PathTooLongException || ex is DirectoryNotFoundException)
                   {
                       continue;
                   }
               }
           }
           catch (UnauthorizedAccessException)
           {
           }
           catch (Exception ex) when (ex is IOException || ex is PathTooLongException || ex is DirectoryNotFoundException)
           {
           }

           return result;
       }

       #region Public API
       public async Task<string?> FindFileAsync(string fileName, string extension)
       {
           return await FindFileInternalAsync(fileName, extension, MatchType.ExactWord);
       }

       public async Task<string?> FindFileAsyncContains(string fileName, string extension)
       {
           return await FindFileInternalAsync(fileName, extension, MatchType.Contains);
       }

       public async Task<string?> FindFileAsyncFuzzy(string fileName, string extension)
       {
           return await FindFileInternalAsync(fileName, extension, MatchType.Fuzzy);
       }

       private async Task<string?> FindFileInternalAsync(string fileName, string extension, MatchType matchType)
       {
           if (string.IsNullOrWhiteSpace(fileName)) return null;
           fileName = fileName.Trim();
           var culture = new System.Globalization.CultureInfo("tr-TR");
           fileName = fileName.ToLower(culture);

           using var cts = new CancellationTokenSource(TimeoutMs);
           var token = cts.Token;

           string matchTypeText = matchType == MatchType.ExactWord ? "tam kelime" : "içeren";
           
           try
           {
               // 1. Windows Recent Items klasöründe ara
               string? path = await SearchInWindowsRecentItemsAsync(fileName, extension, matchType, token);
               if (path != null)
               {
                   return path;
               }

               // 2. Uygulama MRU listesinde ara
               path = await SearchInMruAsync(fileName, extension, matchType, token);
               if (path != null)
               {
                   return path;
               }

               // 3. Office MRU kayıtlarından ara
               path = SearchInOfficeMruRegistry(fileName, extension, matchType);
               if (path != null)
               {
                   return path;
               }

               // 4. En çok kullanılan klasörlerde (kök) ara
               path = await SearchInSpecialFoldersRootOnlyAsync(fileName, extension, matchType, token);
               if (path != null)
               {
                   return path;
               }

               // 5. Belgeler'in alt klasörlerinde (daha derin) ara
               path = await SearchInDocumentsSubfoldersAsync(fileName, extension, matchType, token);
               if (path != null)
               {
                   return path;
               }

               // 6. Hiçbir yerde bulunamazsa null dön
               string fileText = matchType == MatchType.ExactWord ? 
                   $"'{fileName}.{extension}'" : 
                   $"'{fileName}' içeren '{extension}' uzantılı";
               return null;
           }
           catch (OperationCanceledException)
           {
               return null;
           }
           catch (Exception ex)
           {
               LoggingService.LogError($"Arama ({matchTypeText}) hatası: {ex.Message}", ex);
               return null;
           }
       }

       public async Task<bool> OpenFileAsync(string filePath)
       {
           if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

           // Güvenlik kontrolü - sadece tehlikeli dosya uzantılarını kontrol et
           // Path kontrolünü gevşettik çünkü çok katıydı
           if (!SecurityValidator.IsFileExtensionSafe(filePath))
           {
               LoggingService.LogWarning($"Dangerous file extension blocked: {Path.GetExtension(filePath)}");
               return false;
           }

           try
           {
               // Dosyayı MRU listesine ekle
               _ = AddToMruAsync(filePath);

               // Office dosyalarını açan uygulamaları kayıt defterine ekle
               AddToOfficeMruRegistry(filePath);

               if (await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(filePath)))
                   return true;
           }
           catch (Exception ex) 
           {
               LoggingService.LogError($"LaunchFileAsync hatası: {ex.Message}", ex);
           }

           try
           {
               Process.Start(new ProcessStartInfo
               {
                   FileName = filePath,
                   UseShellExecute = true
               });
               return true;
           }
           catch (Exception ex)
           {
               LoggingService.LogError($"Açma hatası: {ex.Message}", ex);
               return false;
           }
       }
       #endregion

       #region MRU
       private static async Task<string?> SearchInMruAsync(
           string fileName, string ext, MatchType matchType, CancellationToken ct)
       {
           try 
           {
               var culture = new System.Globalization.CultureInfo("tr-TR");
               foreach (var entry in StorageApplicationPermissions.MostRecentlyUsedList.Entries)
               {
                   if (ct.IsCancellationRequested) break;

                   try
                   {
                       StorageFile file = await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(entry.Token);
                       bool isMatch = matchType switch
                       {
                           MatchType.ExactWord => MatchesWord(file.Name.ToLower(culture), fileName, ext),
                           MatchType.Contains => Matches(file.Name.ToLower(culture), fileName, ext),
                           MatchType.Fuzzy => MatchesFuzzy(file.Name.ToLower(culture), fileName, ext),
                           _ => false
                       };
                       
                       if (isMatch)
                       {
                           return file.Path;
                       }
                   }
                   catch (Exception ex)
                   {
                       LoggingService.LogVerbose($"MRU girişi erişim hatası: {ex.Message}");
                   }
               }
           }
           catch (Exception ex)
           {
               LoggingService.LogWarning($"MRU listesi erişim hatası: {ex.Message}");
           }
           return null;
       }

       private static async Task AddToMruAsync(string path)
       {
           try
           {
               StorageFile file = await StorageFile.GetFileFromPathAsync(path);
               StorageApplicationPermissions.MostRecentlyUsedList.Add(file, file.Name);
           }
           catch
           {
           }
       }
       #endregion

       #region Windows Recent Items
       private static async Task<string?> SearchInWindowsRecentItemsAsync(
           string fileName, string ext, MatchType matchType, CancellationToken ct)
       {
           if (!Directory.Exists(_recentItemsPath))
           {
               return null;
           }

           try
           {
               var recentFiles = Directory.GetFiles(_recentItemsPath, "*.lnk");
               var matchingFiles = new List<(string Path, DateTime Modified)>();

               foreach (var shortcutPath in recentFiles)
               {
                   if (ct.IsCancellationRequested) break;

                   try
                   {
                       string shortcutName = Path.GetFileNameWithoutExtension(shortcutPath);
                       
                       bool isMatch = matchType switch
                       {
                           MatchType.ExactWord => MatchesWord(shortcutName, fileName, ext),
                           MatchType.Contains => Matches(shortcutName, fileName, ext),
                           MatchType.Fuzzy => MatchesFuzzy(shortcutName, fileName, ext),
                           _ => false
                       };
                       
                       if (isMatch)
                       {
                           string? targetPath = ResolveShortcut(shortcutPath);
                           if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                           {
                               var fileInfo = new FileInfo(shortcutPath);
                               matchingFiles.Add((targetPath, fileInfo.LastWriteTime));
                           }
                       }
                   }
                   catch
                   {
                   }
               }

               if (matchingFiles.Count > 0)
               {
                   var mostRecent = matchingFiles.OrderByDescending(f => f.Modified).First();
                   return mostRecent.Path;
               }
           }
           catch
           {
           }

           return null;
       }

       [DllImport("shell32.dll", CharSet = CharSet.Auto)]
       private static extern IntPtr ILCreateFromPath(string path);

       [DllImport("shell32.dll", CharSet = CharSet.Auto)]
       private static extern bool SHGetPathFromIDList(IntPtr pidl, System.Text.StringBuilder pszPath);

       [DllImport("shell32.dll", CharSet = CharSet.Auto)]
       private static extern int SHGetNameFromIDList(IntPtr pidl, int sigdnName, out IntPtr ppszName);

       private static string? ResolveShortcut(string shortcutPath)
       {
           try
           {
               if (shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
               {
                   // WshShell COM nesnesini oluştur
                   Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                   if (shellType != null)
                   {
                       // COM interop ile WScript.Shell kullan
                       dynamic wshShell = Activator.CreateInstance(shellType);
                       dynamic wshShortcut = wshShell.CreateShortcut(shortcutPath);
                       
                       // Hedef yolu al
                       string targetPath = wshShortcut.TargetPath;
                       
                       // COM nesnelerini temizle
                       Marshal.ReleaseComObject(wshShortcut);
                       Marshal.ReleaseComObject(wshShell);
                       
                       return targetPath;
                   }
               }
           }
           catch
           {
                          }

           return null;
       }
       #endregion

       #region Office MRU Registry
       private static string? SearchInOfficeMruRegistry(string fileName, string ext, MatchType matchType)
       {
           try
           {
               
               // Office uygulamaları için MRU kayıtlarını kontrol et
               string[] registryPaths = {
                   @"Software\Microsoft\Office\16.0\Word\File MRU",     // Word 2016/2019/2021
                   @"Software\Microsoft\Office\15.0\Word\File MRU",     // Word 2013
                   @"Software\Microsoft\Office\14.0\Word\File MRU",     // Word 2010
                   @"Software\Microsoft\Office\16.0\Excel\File MRU",    // Excel 2016/2019/2021
                   @"Software\Microsoft\Office\15.0\Excel\File MRU",    // Excel 2013
                   @"Software\Microsoft\Office\14.0\Excel\File MRU",    // Excel 2010
                   @"Software\Microsoft\Office\16.0\PowerPoint\File MRU", // PowerPoint 2016/2019/2021
                   @"Software\Microsoft\Office\15.0\PowerPoint\File MRU", // PowerPoint 2013
                   @"Software\Microsoft\Office\14.0\PowerPoint\File MRU"  // PowerPoint 2010
               };

               using (RegistryKey? currentUser = Registry.CurrentUser)
               {
                   foreach (var regPath in registryPaths)
                   {
                       using (RegistryKey? key = currentUser.OpenSubKey(regPath))
                       {
                           if (key != null)
                           {
                               foreach (var valueName in key.GetValueNames())
                               {
                                   string? mruEntry = key.GetValue(valueName) as string;
                                   if (!string.IsNullOrEmpty(mruEntry))
                                   {
                                       // Office MRU kayıtları genellikle "T01*Dosya_Yolu" formatındadır
                                       var filePath = mruEntry.Substring(mruEntry.IndexOf('*') + 1);
                                       string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                                       string fileExt = Path.GetExtension(filePath).TrimStart('.');
                                       
                                       bool isMatch = matchType switch
                                       {
                                           MatchType.ExactWord => MatchesWord(fileNameWithoutExt, fileName, ext),
                                           MatchType.Contains => Matches(fileNameWithoutExt, fileName, ext),
                                           MatchType.Fuzzy => MatchesFuzzy(fileNameWithoutExt, fileName, ext),
                                           _ => false
                                       };
                                       
                                       if (isMatch && File.Exists(filePath))
                                       {
                                           return filePath;
                                       }
                                   }
                               }
                           }
                       }
                   }
               }
           }
           catch
           {
           }

           return null;
       }

       private static void AddToOfficeMruRegistry(string filePath)
       {
           try
           {
               string extension = Path.GetExtension(filePath).ToLowerInvariant();
               string registryPath = null;

               // Uzantıya göre Office uygulamasını belirle
               switch (extension)
               {
                   case ".docx":
                   case ".doc":
                   case ".rtf":
                       registryPath = @"Software\Microsoft\Office\16.0\Word\File MRU";
                       break;
                   case ".xlsx":
                   case ".xls":
                   case ".csv":
                       registryPath = @"Software\Microsoft\Office\16.0\Excel\File MRU";
                       break;
                   case ".pptx":
                   case ".ppt":
                       registryPath = @"Software\Microsoft\Office\16.0\PowerPoint\File MRU";
                       break;
                   default:
                       return; // Office dosyası değil
               }

               // Eğer Office 16.0 (2016/2019/2021) yoksa Office 15.0 (2013) dene
               using (RegistryKey? currentUser = Registry.CurrentUser)
               {
                   RegistryKey? key = currentUser.OpenSubKey(registryPath, true);
                   if (key == null)
                   {
                       registryPath = registryPath.Replace("16.0", "15.0");
                       key = currentUser.OpenSubKey(registryPath, true);
                   }

                   // Office 15.0 (2013) yoksa Office 14.0 (2010) dene
                   if (key == null)
                   {
                       registryPath = registryPath.Replace("15.0", "14.0");
                       key = currentUser.OpenSubKey(registryPath, true);
                   }

                   if (key != null)
                   {
                       // Office MRU kaydı oluştur (T01*Dosya_Yolu formatında)
                       string mruEntry = $"T01*{filePath}";
                       key.SetValue("Item 1", mruEntry);
                   }
               }
           }
           catch
           {
           }
       }
       #endregion

       #region Indexed Search
       private static async Task<string?> SearchInIndexedFolderAsync(
           StorageFolder folder, string fileName, string ext, MatchType matchType, CancellationToken ct)
       {
           var opts = new QueryOptions
           {
               FolderDepth = FolderDepth.Deep,
               IndexerOption = IndexerOption.UseIndexerWhenAvailable,
               UserSearchFilter = $"System.FileName:~\"{fileName}\""
           };

           if (!string.IsNullOrWhiteSpace(ext))
               foreach (var e in ext.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0))
                   opts.FileTypeFilter.Add("." + e);
           else
               opts.FileTypeFilter.Add("*");

           var q = folder.CreateFileQueryWithOptions(opts);
           IReadOnlyList<StorageFile> files = await q.GetFilesAsync(0, 50);

           // Dosyaları sıralama için geçici liste
           var sortedFiles = new List<(StorageFile File, DateTimeOffset Modified)>();

           // Dosyaların özelliklerini alıp listeye ekle
           foreach (var file in files)
           {
               if (ct.IsCancellationRequested) break;

               try
               {
                   BasicProperties props = await file.GetBasicPropertiesAsync();
                   sortedFiles.Add((file, props.DateModified));
               }
               catch
               {
               }
           }

           // En son değiştirilene göre sırala
           var orderedFiles = sortedFiles.OrderByDescending(item => item.Modified);

           foreach (var item in orderedFiles)
           {
               bool isMatch = matchType switch
               {
                   MatchType.ExactWord => MatchesWord(item.File.Name, fileName, ext),
                   MatchType.Contains => Matches(item.File.Name, fileName, ext),
                   MatchType.Fuzzy => MatchesFuzzy(item.File.Name, fileName, ext),
                   _ => false
               };
               
               if (isMatch)
                   return item.File.Path;
           }

           return null;
       }
       #endregion

       #region Special Folders Search
       // Klasörlerin sadece kökünde (alt klasörler olmadan) arama
       private static async Task<string?> SearchInSpecialFoldersRootOnlyAsync(
           string fileName, string ext, MatchType matchType, CancellationToken ct)
       {
           var foundFiles = new List<FileInfo>();
           foreach (var dir in _specialFolders)
           {
               LoggingService.LogVerbose($"Kökte aranıyor: {dir}");
               if (ct.IsCancellationRequested) break;
               try
               {
                   foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                   {
                       if (ct.IsCancellationRequested) break;
                       
                       bool isMatch = matchType switch
                       {
                           MatchType.ExactWord => MatchesWord(Path.GetFileName(file), fileName, ext),
                           MatchType.Contains => Matches(Path.GetFileName(file), fileName, ext),
                           MatchType.Fuzzy => MatchesFuzzy(Path.GetFileName(file), fileName, ext),
                           _ => false
                       };
                       
                       if (isMatch)
                       {
                           foundFiles.Add(new FileInfo(file));
                       }
                   }
               }
               catch (UnauthorizedAccessException)
               {
               }
               catch
               {
               }
           }
           if (foundFiles.Count > 0)
           {
               var mostRecentlyAccessed = foundFiles.OrderByDescending(f => f.LastAccessTime).FirstOrDefault();
               if (mostRecentlyAccessed != null)
                   return mostRecentlyAccessed.FullName;
           }
           return null;
       }

       // Belgeler'in alt klasörlerinde (kök hariç) arama
       private static async Task<string?> SearchInDocumentsSubfoldersAsync(
           string fileName, string ext, MatchType matchType, CancellationToken ct)
       {
           var foundFiles = new List<FileInfo>();
           string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
           
           try
           {
               string[] subDirs = Directory.GetDirectories(documents);
               foreach(var subDir in subDirs)
               {
                   try 
                   {
                       LoggingService.LogVerbose($"Belgeler alt klasörünü arıyor: {subDir}");
                       foreach (var file in Directory.EnumerateFiles(subDir, "*.*", SearchOption.AllDirectories))
                       {
                           if (ct.IsCancellationRequested) break;
                           
                           bool isMatch = matchType switch
                           {
                               MatchType.ExactWord => MatchesWord(Path.GetFileName(file), fileName, ext),
                               MatchType.Contains => Matches(Path.GetFileName(file), fileName, ext),
                               MatchType.Fuzzy => MatchesFuzzy(Path.GetFileName(file), fileName, ext),
                               _ => false
                           };
                           
                           if (isMatch)
                           {
                               foundFiles.Add(new FileInfo(file));
                           }
                       }
                   }
                   catch (UnauthorizedAccessException)
                   {
                       // Sadece bu alt klasörü atla, diğerlerine devam et
                       continue;
                   }
                   catch
                   {
                       // Sadece bu alt klasörü atla, diğerlerine devam et
                       continue;
                   }
               }
           }
           catch
           {
           }

           if (foundFiles.Count > 0)
           {
               var mostRecentlyAccessed = foundFiles.OrderByDescending(f => f.LastAccessTime).FirstOrDefault();
               if (mostRecentlyAccessed != null)
                   return mostRecentlyAccessed.FullName;
           }
           return null;
       }
       #endregion

       #region Brute-Force
       private static async Task<string?> SearchFileSystemBruteAsync(
           string fileName, string ext, MatchType matchType, CancellationToken ct)
       {
           var foundFiles = new List<FileInfo>();
           var rootFolders = new[]
           {
               Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
           };

           var patterns = BuildPatterns(ext);

           foreach (var root in rootFolders)
           {
               if (ct.IsCancellationRequested) break;

               foreach (var pattern in patterns)
               {
                   if (ct.IsCancellationRequested) break;

                   try
                   {
                       var files = Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories);
                       foreach (var file in files)
                       {
                           if (ct.IsCancellationRequested) break;

                           bool isMatch = matchType switch
                           {
                               MatchType.ExactWord => MatchesWord(Path.GetFileName(file), fileName, ext),
                               MatchType.Contains => Matches(Path.GetFileName(file), fileName, ext),
                               MatchType.Fuzzy => MatchesFuzzy(Path.GetFileName(file), fileName, ext),
                               _ => false
                           };
                           
                           if (isMatch)
                           {
                               foundFiles.Add(new FileInfo(file));
                           }
                       }
                   }
                   catch (Exception ex)
                   {
                       LoggingService.LogVerbose($"Brute-force arama hatası: {ex.Message}");
                       continue;
                   }
               }
           }

           // Bulunan dosyaları önce erişim tarihine göre, sonra değiştirme tarihine göre sırala
           if (foundFiles.Count > 0)
           {
               // En son erişilen dosyayı seç
               var mostRecentlyAccessed = foundFiles.OrderByDescending(f => f.LastAccessTime).FirstOrDefault();
               if (mostRecentlyAccessed != null)
                   return mostRecentlyAccessed.FullName;
           }

           return null;
       }
       #endregion

       #region Helpers
       // Türkçe karakter normalizasyonu
       private static string NormalizeTurkish(string input)
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

       // Levenshtein mesafesi hesaplama
       private static int LevenshteinDistance(string source, string target)
       {
           if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
           if (string.IsNullOrEmpty(target)) return source.Length;

           int sourceLength = source.Length;
           int targetLength = target.Length;
           int[,] distance = new int[sourceLength + 1, targetLength + 1];

           for (int i = 0; i <= sourceLength; i++)
               distance[i, 0] = i;

           for (int j = 0; j <= targetLength; j++)
               distance[0, j] = j;

           for (int i = 1; i <= sourceLength; i++)
           {
               for (int j = 1; j <= targetLength; j++)
               {
                   int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                   distance[i, j] = Math.Min(
                       Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                       distance[i - 1, j - 1] + cost);
               }
           }

           return distance[sourceLength, targetLength];
       }

       // Benzerlik oranı hesaplama (0.0 - 1.0 arası)
       private static double CalculateSimilarity(string source, string target)
       {
           if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
               return 0.0;

           // Türkçe karakterleri normalize et
           string normalizedSource = NormalizeTurkish(source.ToLowerInvariant());
           string normalizedTarget = NormalizeTurkish(target.ToLowerInvariant());

           // Tam eşleşme varsa 1.0 döndür
           if (normalizedSource.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
               return 1.0;

           int distance = LevenshteinDistance(normalizedSource, normalizedTarget);
           int maxLength = Math.Max(normalizedSource.Length, normalizedTarget.Length);
           
           if (maxLength == 0) return 1.0;
           
           return 1.0 - (double)distance / maxLength;
       }

       // Fuzzy matching helper
       private static bool MatchesFuzzy(string fullName, string searchName, string ext, double threshold = 0.7)
       {
           if (string.IsNullOrWhiteSpace(fullName)) return false;

           string nameNoExt = Path.GetFileNameWithoutExtension(fullName);
           var searchWords = searchName.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
           var fileWords = nameNoExt.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

           // Her arama kelimesi için en iyi eşleşmeyi bul
           foreach (var searchWord in searchWords)
           {
               bool foundMatch = false;
               foreach (var fileWord in fileWords)
               {
                   double similarity = CalculateSimilarity(searchWord, fileWord);
                   if (similarity >= threshold)
                   {
                       foundMatch = true;
                       break;
                   }
               }
               if (!foundMatch) return false;
           }

           // Uzantı kontrolü
           if (string.IsNullOrWhiteSpace(ext)) return true;

           string fileExt = Path.GetExtension(fullName).TrimStart('.').ToLowerInvariant();
           return ext.Split(',').Any(e =>
                     fileExt.Equals(e.Trim(), StringComparison.OrdinalIgnoreCase));
       }

       private static bool Matches(string fullName, string searchName, string ext)
       {
           if (string.IsNullOrWhiteSpace(fullName)) return false;

           string nameNoExt = Path.GetFileNameWithoutExtension(fullName);
           var culture = new System.Globalization.CultureInfo("tr-TR");
           string nameLower = nameNoExt.ToLower(culture);
           var searchWords = searchName.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
           // All words in searchName must be present in any order in the file name
           foreach (var word in searchWords)
           {
               if (!nameLower.Contains(word, StringComparison.OrdinalIgnoreCase))
                   return false;
           }

           if (string.IsNullOrWhiteSpace(ext)) return true;

           string fileExt = Path.GetExtension(fullName).TrimStart('.').ToLowerInvariant();
           return ext.Split(',').Any(e =>
                     fileExt.Equals(e.Trim(), StringComparison.OrdinalIgnoreCase));
       }

       private static IEnumerable<string> BuildPatterns(string ext)
       {
           if (string.IsNullOrWhiteSpace(ext))
               yield return "*.*";
           else
               foreach (var e in ext.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0))
                   yield return $"*.{e}";
       }

       // Tam kelime eşleşmesi (ör: 'ali' sadece 'ali' geçen dosya adlarında eşleşir, 'analiz' eşleşmez)
       private static bool MatchesWord(string fullName, string searchName, string ext)
       {
           if (string.IsNullOrWhiteSpace(fullName)) return false;
           string nameNoExt = Path.GetFileNameWithoutExtension(fullName);
           var culture = new System.Globalization.CultureInfo("tr-TR");
           var fileWords = nameNoExt.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(w => w.ToLower(culture)).ToArray();
           var searchWords = searchName.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(w => w.ToLower(culture)).ToArray();
           // All search words must be present as a full word in the file name
           foreach (var word in searchWords)
           {
               if (!fileWords.Any(w => w.Equals(word, StringComparison.OrdinalIgnoreCase)))
                   return false;
           }
           if (string.IsNullOrWhiteSpace(ext)) return true;
           string fileExt = Path.GetExtension(fullName).TrimStart('.').ToLowerInvariant();
           return ext.Split(',').Any(e => fileExt.Equals(e.Trim(), StringComparison.OrdinalIgnoreCase));
       }

       #region Interface Implementation

       public async Task<List<string>> FindFilesAsync(string fileName, string searchPath = "")
       {
           var results = new List<string>();
           var searchPaths = string.IsNullOrEmpty(searchPath) ? GetCommonDirectories() : new List<string> { searchPath };
           
           foreach (var path in searchPaths)
           {
               try
               {
                   if (Directory.Exists(path))
                   {
                       var files = Directory.GetFiles(path, $"*{fileName}*", SearchOption.AllDirectories);
                       results.AddRange(files);
                   }
               }
               catch (Exception ex)
               {
                   LoggingService.LogVerbose($"Error searching for {fileName} in {path}: {ex.Message}");
               }
           }
           
           return results;
       }

       public async Task<List<string>> SearchInDirectoryAsync(string directory, string searchPattern)
       {
           var results = new List<string>();
           
           try
           {
               if (Directory.Exists(directory))
               {
                   var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
                   results.AddRange(files);
               }
           }
           catch
           {
           }
           
           return results;
       }

       public List<string> GetCommonDirectories()
       {
           return _specialFolders.ToList();
       }

       public async Task<List<string>> FindFilesByExtensionAsync(string extension, string searchPath = "")
       {
           var results = new List<string>();
           var searchPaths = string.IsNullOrEmpty(searchPath) ? GetCommonDirectories() : new List<string> { searchPath };
           
           foreach (var path in searchPaths)
           {
               try
               {
                   if (Directory.Exists(path))
                   {
                       var files = Directory.GetFiles(path, $"*.{extension.TrimStart('.')}", SearchOption.AllDirectories);
                       results.AddRange(files);
                   }
               }
               catch
               {
               }
           }
           
           return results;
       }

       public async Task<List<string>> FindFilesByContentAsync(string content, string searchPath = "")
       {
           // Simple implementation - in real scenario, you'd use Windows Search API
           var results = new List<string>();
           var searchPaths = string.IsNullOrEmpty(searchPath) ? GetCommonDirectories() : new List<string> { searchPath };
           
           foreach (var path in searchPaths)
           {
               try
               {
                   if (Directory.Exists(path))
                   {
                       var files = Directory.GetFiles(path, "*.txt", SearchOption.TopDirectoryOnly);
                       foreach (var file in files.Take(50)) // Limit for performance
                       {
                           try
                           {
                               var fileContent = await File.ReadAllTextAsync(file);
                               if (fileContent.Contains(content, StringComparison.OrdinalIgnoreCase))
                               {
                                   results.Add(file);
                               }
                           }
                           catch { /* Skip files that can't be read */ }
                       }
                   }
               }
               catch
               {
               }
           }
           
           return results;
       }

       public async Task<List<string>> GetDirectoriesAsync(string path)
       {
           try
           {
               if (Directory.Exists(path))
               {
                   return Directory.GetDirectories(path).ToList();
               }
           }
           catch
           {
           }
           
           return new List<string>();
       }

       public async Task<bool> DirectoryExistsAsync(string path)
       {
           return Directory.Exists(path);
       }

       public async Task<bool> FileExistsAsync(string filePath)
       {
           return File.Exists(filePath);
       }

       public string GetDesktopPath()
       {
           return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
       }

       public string GetDocumentsPath()
       {
           return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
       }

       public string GetDownloadsPath()
       {
           return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
       }

       #endregion
       #endregion
   }
}