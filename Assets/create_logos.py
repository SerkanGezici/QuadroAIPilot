#!/usr/bin/env python3
"""
QuadroAIPilot Logo Generator
QAI Logo'dan tüm gerekli boyutlarda logo dosyaları oluşturur
"""

from PIL import Image
import os
import sys

def create_ico(img_path, output_path):
    """Multiple size ICO dosyası oluştur"""
    img = Image.open(img_path)
    
    # ICO için gerekli boyutlar
    sizes = [(16, 16), (32, 32), (48, 48), (256, 256)]
    imgs = []
    
    for size in sizes:
        # RGBA moduna çevir (transparency için)
        resized = img.resize(size, Image.Resampling.LANCZOS)
        if resized.mode != 'RGBA':
            resized = resized.convert('RGBA')
        imgs.append(resized)
    
    # ICO olarak kaydet
    imgs[0].save(output_path, format='ICO', sizes=sizes, append_images=imgs[1:])
    print(f"[OK] Created: {output_path}")

def create_png(img_path, size, output_path):
    """Belirli boyutta PNG oluştur"""
    img = Image.open(img_path)
    
    # Boyutlandır
    if isinstance(size, tuple):
        # Wide veya custom aspect ratio
        resized = img.resize(size, Image.Resampling.LANCZOS)
    else:
        # Square
        resized = img.resize((size, size), Image.Resampling.LANCZOS)
    
    # RGBA moduna çevir
    if resized.mode != 'RGBA':
        resized = resized.convert('RGBA')
    
    # PNG olarak kaydet
    resized.save(output_path, 'PNG')
    print(f"[OK] Created: {output_path} ({size})")

def main():
    # Kaynak logo
    source_logo = "QAI_Logo_Transparent.png"
    
    if not os.path.exists(source_logo):
        print(f"[ERROR] {source_logo} bulunamadi!")
        sys.exit(1)
    
    print("QuadroAIPilot Logo Generator")
    print("=" * 40)
    
    # 1. ICO dosyası oluştur
    create_ico(source_logo, "icon.ico")
    
    # 2. UWP/WinUI için gerekli PNG'ler
    logos = {
        # Temel logolar
        "icon128.png": 128,
        "StoreLogo.png": 50,
        
        # Scale-200 logolar (2x boyut)
        "Square44x44Logo.scale-200.png": 88,
        "Square150x150Logo.scale-200.png": 300,
        "LockScreenLogo.scale-200.png": 48,
        
        # Özel boyutlar
        "Square44x44Logo.targetsize-24_altform-unplated.png": 24,
        "SplashScreen.scale-200.png": (1240, 600),
        "Wide310x150Logo.scale-200.png": (620, 300),
    }
    
    for filename, size in logos.items():
        create_png(source_logo, size, filename)
    
    # 3. Browser extension logolar
    browser_sizes = {
        "icon16.png": 16,
        "icon48.png": 48,
        "icon128.png": 128
    }
    
    # Chrome için
    chrome_dir = "../BrowserExtensions/Chrome"
    if os.path.exists(chrome_dir):
        print("\n[Chrome Extension Icons]")
        for filename, size in browser_sizes.items():
            create_png(source_logo, size, os.path.join(chrome_dir, filename))
    
    # Edge için
    edge_dir = "../BrowserExtensions/Edge"
    if os.path.exists(edge_dir):
        print("\n[Edge Extension Icons]")
        for filename, size in browser_sizes.items():
            create_png(source_logo, size, os.path.join(edge_dir, filename))
    
    # Firefox için
    firefox_dir = "../BrowserExtensions/Firefox"
    if os.path.exists(firefox_dir):
        print("\n[Firefox Extension Icons]")
        for filename, size in browser_sizes.items():
            create_png(source_logo, size, os.path.join(firefox_dir, filename))
    
    print("\n[SUCCESS] Tum logo dosyalari basariyla olusturuldu!")

if __name__ == "__main__":
    main()