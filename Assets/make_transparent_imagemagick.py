#!/usr/bin/env python3
import subprocess
import os

def make_transparent_with_imagemagick():
    """ImageMagick kullanarak arka planı şeffaf yap"""
    
    files = ['icon128.png', 'icon16.png', 'icon48.png']
    
    for filename in files:
        if os.path.exists(filename):
            print(f"Processing {filename}...")
            
            # Geçici dosya adı
            temp_file = f"temp_{filename}"
            
            # ImageMagick komutu - mor/lacivert arka planı şeffaf yap
            cmd = [
                'magick', filename,
                '-fuzz', '20%',  # Renk toleransı
                '-transparent', '#2D1B69',  # Koyu mor/lacivert rengi
                '-background', 'none',
                temp_file
            ]
            
            try:
                subprocess.run(cmd, check=True)
                # Orijinal dosyayı değiştir
                os.replace(temp_file, filename)
                print(f"Updated: {filename}")
            except subprocess.CalledProcessError as e:
                print(f"Error processing {filename}: {e}")
            except FileNotFoundError:
                print("ImageMagick not found. Trying alternative method...")
                break

# PowerShell ile alternatif yöntem
def make_transparent_with_powershell():
    """PowerShell ve .NET kullanarak arka planı şeffaf yap"""
    
    ps_script = '''
Add-Type -AssemblyName System.Drawing

function Make-TransparentBackground {
    param($inputPath, $outputPath)
    
    $bitmap = [System.Drawing.Bitmap]::new($inputPath)
    
    # Arka plan rengini belirle (sol üst köşeden)
    $bgColor = $bitmap.GetPixel(0, 0)
    
    # Yeni bitmap oluştur
    $newBitmap = [System.Drawing.Bitmap]::new($bitmap.Width, $bitmap.Height)
    
    for ($x = 0; $x -lt $bitmap.Width; $x++) {
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            $pixel = $bitmap.GetPixel($x, $y)
            
            # Mor/lacivert arka plan kontrolü
            if ($pixel.R -lt 80 -and $pixel.G -lt 40 -and $pixel.B -gt 40 -and $pixel.B -lt 120) {
                # Şeffaf yap
                $newBitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            } else {
                # Orijinal rengi koru
                $newBitmap.SetPixel($x, $y, $pixel)
            }
        }
    }
    
    $newBitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $newBitmap.Dispose()
}

# PNG dosyalarını işle
@("icon128.png", "icon16.png", "icon48.png") | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "Processing $_..."
        Make-TransparentBackground -inputPath $_ -outputPath $_
        Write-Host "Updated: $_"
    }
}
'''
    
    # PowerShell scriptini çalıştır
    try:
        subprocess.run(['powershell', '-Command', ps_script], check=True, cwd=os.getcwd())
        print("PNG files processed with PowerShell")
    except Exception as e:
        print(f"PowerShell error: {e}")

if __name__ == "__main__":
    # Önce ImageMagick'i dene
    make_transparent_with_imagemagick()
    
    # ImageMagick yoksa PowerShell kullan
    if not os.path.exists('temp_icon128.png'):
        print("\nTrying PowerShell method...")
        make_transparent_with_powershell()