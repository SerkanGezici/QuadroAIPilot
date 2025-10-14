#!/usr/bin/env python3
import os
from PIL import Image
import numpy as np

def make_background_transparent(image_path, output_path, bg_color_range=None):
    """
    Arka planı şeffaf yapar
    bg_color_range: Arka plan renk aralığı (min_rgb, max_rgb) tuple'ı
    """
    img = Image.open(image_path).convert('RGBA')
    data = np.array(img)
    
    # Mor/lacivert arka plan renk aralığı
    if bg_color_range is None:
        # Koyu mor/lacivert tonları için renk aralığı
        min_rgb = np.array([20, 0, 40, 255])  # Koyu mor alt sınır
        max_rgb = np.array([80, 40, 120, 255])  # Mor üst sınır
    else:
        min_rgb, max_rgb = bg_color_range
    
    # Arka plan piksellerini bul
    mask = np.all((data >= min_rgb) & (data <= max_rgb), axis=-1)
    
    # Arka planı şeffaf yap
    data[mask] = [0, 0, 0, 0]
    
    # Kenar yumuşatma için alfa kanalını ayarla
    alpha = data[:, :, 3]
    
    # Kenarları yumuşat
    from scipy.ndimage import gaussian_filter
    alpha_float = alpha.astype(float) / 255.0
    alpha_smooth = gaussian_filter(alpha_float, sigma=0.5)
    data[:, :, 3] = (alpha_smooth * 255).astype(np.uint8)
    
    # Yeni görüntüyü kaydet
    new_img = Image.fromarray(data, 'RGBA')
    new_img.save(output_path)
    print(f"Saved: {output_path}")

def process_all_icons():
    """Tüm icon dosyalarını işle"""
    sizes = {
        'icon.ico': None,  # ICO dosyası özel işlem gerektirir
        'icon128.png': 128,
        'icon16.png': 16,
        'icon48.png': 48,
        'LockScreenLogo.scale-200.png': 48,
        'SplashScreen.scale-200.png': 1240,
        'Square150x150Logo.scale-200.png': 300,
        'Square44x44Logo.scale-200.png': 88,
        'Square44x44Logo.targetsize-24_altform-unplated.png': 24,
        'StoreLogo.png': 50,
        'Wide310x150Logo.scale-200.png': 620
    }
    
    # Önce PNG dosyalarını işle
    for filename, size in sizes.items():
        if filename == 'icon.ico':
            continue
            
        input_path = filename
        if os.path.exists(input_path):
            print(f"Processing {filename}...")
            make_background_transparent(input_path, input_path)
    
    print("\nTüm PNG dosyaları işlendi!")

if __name__ == "__main__":
    # scipy kurulu değilse basit versiyon kullan
    try:
        from scipy.ndimage import gaussian_filter
        process_all_icons()
    except ImportError:
        print("scipy bulunamadı, basit versiyon kullanılıyor...")
        
        # Basit versiyon
        def make_background_transparent_simple(image_path):
            img = Image.open(image_path).convert('RGBA')
            data = img.getdata()
            
            new_data = []
            for item in data:
                # Mor/lacivert tonlarını kontrol et
                if 20 <= item[0] <= 80 and 0 <= item[1] <= 40 and 40 <= item[2] <= 120:
                    # Arka planı şeffaf yap
                    new_data.append((0, 0, 0, 0))
                else:
                    new_data.append(item)
            
            img.putdata(new_data)
            img.save(image_path)
            print(f"Updated: {image_path}")
        
        # Tüm PNG dosyalarını işle
        for filename in ['icon128.png', 'icon16.png', 'icon48.png']:
            if os.path.exists(filename):
                print(f"Processing {filename}...")
                make_background_transparent_simple(filename)