using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// Audio Ducking Manager - Mikrofon ses seviyesi kontrolü için
    /// TTS sırasında mikrofon hassasiyetini düşürür, feedback loop önler
    /// </summary>
    public static class AudioDuckingManager
    {
        #region Windows API Imports
        
        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);
        
        [DllImport("winmm.dll")]
        private static extern int waveInSetVolume(IntPtr hwi, uint dwVolume);
        
        [DllImport("winmm.dll")]
        private static extern int mixerOpen(out IntPtr phmx, uint uMxId, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);
        
        [DllImport("winmm.dll")]
        private static extern int mixerClose(IntPtr hmx);
        
        [DllImport("winmm.dll")]
        private static extern int mixerGetLineControls(IntPtr hmxobj, ref MIXERLINECONTROLS pmxlc, uint fdwControls);
        
        [DllImport("winmm.dll")]
        private static extern int mixerGetControlDetails(IntPtr hmxobj, ref MIXERCONTROLDETAILS pmxcd, uint fdwDetails);
        
        [DllImport("winmm.dll")]
        private static extern int mixerSetControlDetails(IntPtr hmxobj, ref MIXERCONTROLDETAILS pmxcd, uint fdwDetails);
        
        [DllImport("winmm.dll")]
        private static extern int mixerGetLineInfo(IntPtr hmxobj, ref MIXERLINE pmxl, uint fdwInfo);
        
        #endregion
        
        #region Structures
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MIXERLINE
        {
            public uint cbStruct;
            public uint dwDestination;
            public uint dwSource;
            public uint dwLineID;
            public uint fdwLine;
            public uint dwUser;
            public uint dwComponentType;
            public uint cChannels;
            public uint cConnections;
            public uint cControls;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string szShortName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szName;
            public uint dwType;
            public uint dwDeviceID;
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MIXERLINECONTROLS
        {
            public uint cbStruct;
            public uint dwLineID;
            public uint dwControlID;
            public uint cControls;
            public uint cbmxctrl;
            public IntPtr pamxctrl;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MIXERCONTROLDETAILS
        {
            public uint cbStruct;
            public uint dwControlID;
            public uint cChannels;
            public uint cMultipleItems;
            public uint cbDetails;
            public IntPtr paDetails;
        }
        
        #endregion
        
        #region Constants
        
        private const uint MIXERLINE_COMPONENTTYPE_SRC_MICROPHONE = 0x00000003;
        private const uint MIXERCONTROL_CONTROLTYPE_VOLUME = 0x50030001;
        private const uint MIXER_GETLINEINFOF_COMPONENTTYPE = 0x00000003;
        private const uint MIXER_GETLINECONTROLSF_ONEBYTYPE = 0x00000002;
        private const uint MIXER_SETCONTROLDETAILSF_VALUE = 0x00000000;
        
        #endregion
        
        #region Private Fields
        
        private static uint _originalMicrophoneVolume = 100;
        private static bool _isDucked = false;
        private static readonly object _lockObject = new object();
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Mikrofon ses seviyesini düşürür (ducking)
        /// TTS sırasında feedback loop önlemek için kullanılır
        /// </summary>
        /// <param name="duckPercentage">Düşürülecek seviye (0-100, varsayılan 20)</param>
        public static async Task DuckMicrophoneAsync(uint duckPercentage = 20)
        {
            await Task.Run(() => DuckMicrophone(duckPercentage));
        }
        
        /// <summary>
        /// Mikrofon ses seviyesini normale döndürür
        /// </summary>
        public static async Task RestoreMicrophoneAsync()
        {
            await Task.Run(() => RestoreMicrophone());
        }
        
        /// <summary>
        /// Mikrofon ses seviyesini düşürür (senkron)
        /// </summary>
        /// <param name="duckPercentage">Düşürülecek seviye (0-100)</param>
        public static void DuckMicrophone(uint duckPercentage = 20)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_isDucked)
                    {
                        LogService.LogDebug("[AudioDuckingManager] Mikrofon zaten ducked durumda");
                        return;
                    }
                    
                    // Mevcut mikrofon seviyesini kaydet
                    _originalMicrophoneVolume = GetCurrentMicrophoneVolume();
                    
                    // Yeni seviyeyi hesapla
                    uint newVolume = Math.Min(duckPercentage, 100);
                    
                    // Mikrofon seviyesini düşür
                    SetMicrophoneVolume(newVolume);
                    _isDucked = true;
                    
                    LogService.LogDebug($"[AudioDuckingManager] Mikrofon ducked: {_originalMicrophoneVolume}% -> {newVolume}%");
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"[AudioDuckingManager] Duck mikrofon hatası: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Mikrofon ses seviyesini normale döndürür (senkron)
        /// </summary>
        public static void RestoreMicrophone()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isDucked)
                    {
                        LogService.LogDebug("[AudioDuckingManager] Mikrofon zaten normal seviyede");
                        return;
                    }
                    
                    // Orijinal seviyeye döndür
                    SetMicrophoneVolume(_originalMicrophoneVolume);
                    _isDucked = false;
                    
                    LogService.LogDebug($"[AudioDuckingManager] Mikrofon restored: {_originalMicrophoneVolume}%");
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"[AudioDuckingManager] Restore mikrofon hatası: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Mevcut mikrofon ses seviyesini al
        /// </summary>
        /// <returns>Ses seviyesi (0-100)</returns>
        public static uint GetCurrentMicrophoneVolume()
        {
            try
            {
                // Basit implementasyon - Windows varsayılan seviyesi
                return 80; // TODO: Gerçek API implementasyonu
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[AudioDuckingManager] Mikrofon seviye okuma hatası: {ex.Message}");
                return 80; // Varsayılan değer
            }
        }
        
        /// <summary>
        /// Mikrofon ses seviyesini ayarla
        /// </summary>
        /// <param name="volume">Ses seviyesi (0-100)</param>
        public static void SetMicrophoneVolume(uint volume)
        {
            try
            {
                // Volume değerini 0-65535 aralığına çevir
                uint volumeValue = Math.Min(volume, 100) * 655;
                
                // Her iki kanal için aynı seviyeyi ayarla
                uint stereoVolume = volumeValue | (volumeValue << 16);
                
                // WinMM API ile mikrofon seviyesini ayarla
                int result = waveInSetVolume(IntPtr.Zero, stereoVolume);
                
                if (result != 0)
                {
                    LogService.LogDebug($"[AudioDuckingManager] waveInSetVolume hatası: {result}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"[AudioDuckingManager] Mikrofon seviye ayarlama hatası: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mikrofon ducking durumunu kontrol et
        /// </summary>
        /// <returns>True ise mikrofon ducked durumda</returns>
        public static bool IsMicrophoneDucked()
        {
            lock (_lockObject)
            {
                return _isDucked;
            }
        }
        
        /// <summary>
        /// Sistem ses kartı bilgilerini al (debug için)
        /// </summary>
        /// <returns>Ses kartı bilgileri</returns>
        public static string GetAudioDeviceInfo()
        {
            try
            {
                return $"Mikrofon Durumu: {(_isDucked ? "Ducked" : "Normal")}, " +
                       $"Orijinal Seviye: {_originalMicrophoneVolume}%";
            }
            catch (Exception ex)
            {
                return $"Ses kartı bilgisi alınamadı: {ex.Message}";
            }
        }
        
        #endregion
    }
}