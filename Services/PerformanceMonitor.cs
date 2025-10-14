using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using Windows.System.Diagnostics;
using Windows.System.Power;
using QuadroAIPilot.Managers;

namespace QuadroAIPilot.Services
{
    public enum GPULevel
    {
        Unknown,
        Low,      // Entegre grafik kartları
        Medium,   // Orta seviye GPU'lar
        High      // High-end GPU'lar
    }

    public class PerformanceMonitor
    {
        private static PerformanceMonitor _instance;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        private GPULevel _gpuLevel = GPULevel.Unknown;
        private bool _isInitialized = false;

        public static PerformanceMonitor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PerformanceMonitor();
                }
                return _instance;
            }
        }

        private PerformanceMonitor()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                // CPU ve RAM sayaçlarını oluştur
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                
                // GPU seviyesini algıla
                Task.Run(() => DetectGPULevel());
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformanceMonitor] Başlatma hatası: {ex.Message}");
            }
        }

        public async Task<GPULevel> GetGPULevelAsync()
        {
            if (_gpuLevel == GPULevel.Unknown)
            {
                await Task.Run(() => DetectGPULevel());
            }
            return _gpuLevel;
        }

        private void DetectGPULevel()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString() ?? "";
                        var adapterRAM = Convert.ToUInt64(obj["AdapterRAM"] ?? 0);
                        
                        Debug.WriteLine($"[PerformanceMonitor] GPU Algılandı: {name}, RAM: {adapterRAM / (1024 * 1024)} MB");

                        // GPU seviyesini belirle
                        if (name.Contains("NVIDIA") || name.Contains("AMD"))
                        {
                            if (name.Contains("RTX") || name.Contains("RX"))
                            {
                                _gpuLevel = GPULevel.High;
                                break;
                            }
                            else if (adapterRAM > 2L * 1024 * 1024 * 1024) // 2GB+
                            {
                                _gpuLevel = GPULevel.Medium;
                            }
                            else
                            {
                                _gpuLevel = GPULevel.Low;
                            }
                        }
                        else if (name.Contains("Intel"))
                        {
                            if (name.Contains("Iris") || name.Contains("Arc"))
                            {
                                _gpuLevel = GPULevel.Medium;
                            }
                            else
                            {
                                _gpuLevel = GPULevel.Low;
                            }
                        }
                    }
                }

                if (_gpuLevel == GPULevel.Unknown)
                {
                    _gpuLevel = GPULevel.Low; // Varsayılan
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformanceMonitor] GPU algılama hatası: {ex.Message}");
                _gpuLevel = GPULevel.Low; // Hata durumunda düşük seviye varsay
            }
        }

        public float GetCPUUsage()
        {
            if (!_isInitialized || _cpuCounter == null) return 0;
            
            try
            {
                return _cpuCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        public float GetAvailableRAM()
        {
            if (!_isInitialized || _ramCounter == null) return 0;
            
            try
            {
                return _ramCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<SystemPerformanceInfo> GetSystemPerformanceAsync()
        {
            var info = new SystemPerformanceInfo();
            
            try
            {
                // CPU kullanımı
                info.CPUUsage = GetCPUUsage();
                
                // RAM kullanımı
                var report = SystemDiagnosticInfo.GetForCurrentSystem().MemoryUsage.GetReport();
                info.TotalMemoryMB = report.TotalPhysicalSizeInBytes / (1024 * 1024);
                info.AvailableMemoryMB = report.AvailableSizeInBytes / (1024 * 1024);
                info.MemoryUsagePercent = (float)((info.TotalMemoryMB - info.AvailableMemoryMB) / (double)info.TotalMemoryMB * 100);
                
                // Pil durumu
                info.BatteryStatus = PowerManager.BatteryStatus;
                info.BatteryChargePercent = PowerManager.RemainingChargePercent;
                info.PowerSupplyStatus = PowerManager.PowerSupplyStatus;
                
                // GPU seviyesi
                info.GPULevel = await GetGPULevelAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformanceMonitor] Performans bilgisi alma hatası: {ex.Message}");
            }
            
            return info;
        }

        public async Task<PerformanceProfile> GetRecommendedPerformanceProfileAsync()
        {
            var perfInfo = await GetSystemPerformanceAsync();
            
            // Pil seviyesi düşükse
            if (perfInfo.BatteryStatus == BatteryStatus.Discharging && perfInfo.BatteryChargePercent < 20)
            {
                return PerformanceProfile.PowerSaver;
            }
            
            // Yüksek CPU/RAM kullanımı varsa
            if (perfInfo.CPUUsage > 80 || perfInfo.MemoryUsagePercent > 85)
            {
                return PerformanceProfile.Low;
            }
            
            // GPU seviyesine göre
            switch (perfInfo.GPULevel)
            {
                case GPULevel.High:
                    return PerformanceProfile.High;
                case GPULevel.Medium:
                    return PerformanceProfile.Medium;
                default:
                    return PerformanceProfile.Low;
            }
        }

        public void Dispose()
        {
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();
        }
    }

    public class SystemPerformanceInfo
    {
        public float CPUUsage { get; set; }
        public ulong TotalMemoryMB { get; set; }
        public ulong AvailableMemoryMB { get; set; }
        public float MemoryUsagePercent { get; set; }
        public BatteryStatus BatteryStatus { get; set; }
        public int BatteryChargePercent { get; set; }
        public PowerSupplyStatus PowerSupplyStatus { get; set; }
        public GPULevel GPULevel { get; set; }
    }
}