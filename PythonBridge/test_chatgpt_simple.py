#!/usr/bin/env python3
"""
ChatGPT Basit Test - ChatGPT'ye mesaj gönder ve yanıtı kontrol et
"""

import asyncio
import sys
from playwright.async_api import async_playwright

async def test_chatgpt():
    """ChatGPT'ye test mesajı gönder"""

    print("=" * 60)
    print("🔍 ChatGPT Test Başlıyor...")
    print("=" * 60)

    playwright = None
    browser = None

    try:
        # Playwright başlat
        print("\n[1] 🚀 Playwright başlatılıyor...")
        playwright = await async_playwright().start()
        print("✅ Playwright başlatıldı")

        # Chrome başlat (GÖRÜNÜR MOD - debug için)
        print("\n[2] 🌐 Chrome başlatılıyor (GÖRÜNÜR MOD)...")
        browser = await playwright.chromium.launch_persistent_context(
            user_data_dir='./chrome-profile',
            headless=False,  # ❗ GÖRÜNÜR - tarayıcıyı görebilirsiniz
            viewport={'width': 1920, 'height': 1080},
            args=[
                '--disable-blink-features=AutomationControlled',
                '--disable-gpu',
                '--no-sandbox'
            ],
            timeout=60000
        )
        print("✅ Chrome başlatıldı")

        # Sayfa al
        pages = browser.pages
        if pages:
            page = pages[0]
        else:
            page = await browser.new_page()

        print(f"📄 Şu anki URL: {page.url}")

        # ChatGPT'ye GIT
        print("\n[3] 🌐 ChatGPT'ye GİDİLİYOR...")
        print("   URL: https://chat.openai.com/")

        await page.goto('https://chat.openai.com/', wait_until='domcontentloaded', timeout=60000)
        print(f"✅ Sayfa yüklendi: {page.url}")

        # Networkidle bekle (opsiyonel)
        try:
            await page.wait_for_load_state('networkidle', timeout=15000)
            print("✅ Network idle (sayfa tamamen yüklendi)")
        except:
            print("⚠️ Network idle timeout (devam ediliyor)")

        await page.wait_for_timeout(3000)

        print(f"\n📍 Son URL: {page.url}")

        # Login kontrolü
        print("\n[4] 🔍 Login kontrolü yapılıyor...")
        title = await page.title()
        print(f"📄 Sayfa başlığı: {title}")

        # Textarea bul
        print("\n[5] 🔍 Textarea aranıyor...")
        textarea_selector = 'textarea[placeholder*="Message"]'

        try:
            await page.wait_for_selector(textarea_selector, timeout=10000)
            print(f"✅ Textarea bulundu: {textarea_selector}")
        except Exception as e:
            print(f"❌ Textarea bulunamadı: {e}")
            print("   Alternatif selector'ler deneniyor...")

            # Alternatifler
            for alt_selector in ['textarea', 'div[contenteditable="true"]', '#prompt-textarea']:
                try:
                    await page.wait_for_selector(alt_selector, timeout=5000)
                    textarea_selector = alt_selector
                    print(f"✅ Alternatif bulundu: {alt_selector}")
                    break
                except:
                    pass

        # Mesaj gönder
        print("\n[6] 📤 Mesaj gönderiliyor...")
        test_message = "2 + 2 kaç eder?"
        print(f"   📝 Mesaj: '{test_message}'")

        await page.fill(textarea_selector, test_message)
        print("   ✅ Mesaj yazıldı")

        await page.wait_for_timeout(500)
        await page.press(textarea_selector, 'Enter')
        print("   ✅ Enter basıldı")

        # Yanıt bekle
        print("\n[7] ⏳ Yanıt bekleniyor...")
        await page.wait_for_timeout(5000)  # 5 saniye bekle

        # Response bul
        print("\n[8] 🔍 Yanıt aranıyor...")

        response_selectors = [
            'div[data-message-author-role="assistant"]',
            'div[class*="agent-turn"]',
            'div[class*="markdown"]',
            'article'
        ]

        response_text = None
        used_selector = None

        for selector in response_selectors:
            try:
                print(f"   🔍 Deneniyor: {selector}")
                await page.wait_for_selector(selector, timeout=30000)
                elements = await page.query_selector_all(selector)

                if elements:
                    last_element = elements[-1]
                    text = await last_element.inner_text()

                    if text and len(text.strip()) > 0:
                        response_text = text
                        used_selector = selector
                        print(f"   ✅ BULUNDU! ({len(text)} karakter)")
                        break
            except:
                print(f"   ⚠️ Bulunamadı")

        # SONUÇ
        print("\n" + "=" * 60)
        print("📊 TEST SONUCU")
        print("=" * 60)

        if response_text:
            print("✅ BAŞARILI - Yanıt alındı!")
            print(f"\n📍 Çalışan Selector: {used_selector}")
            print(f"📏 Yanıt Uzunluğu: {len(response_text)} karakter")
            print(f"\n📝 Yanıt (ilk 300 karakter):")
            print("-" * 60)
            print(response_text[:300])
            print("-" * 60)

            print(f"\n💡 ÇÖZÜMLERİMİZ:")
            print(f"   1. chatgpt_http_bridge.py'de bu selector'ü kullan:")
            print(f"      response_selector = '{used_selector}'")
            print(f"   2. Timeout değerlerini artır (en az 30 saniye)")
        else:
            print("❌ BAŞARISIZ - Yanıt alınamadı")
            print("\n📸 Screenshot alınıyor...")
            await page.screenshot(path='chatgpt_failed.png', full_page=True)
            print("   💾 Screenshot: chatgpt_failed.png")

        # 10 saniye daha bekle (inceleme için)
        print("\n⏸️  10 saniye bekleniyor (browser'ı inceleyin)...")
        await page.wait_for_timeout(10000)

    except Exception as e:
        print(f"\n❌ HATA: {e}")
        import traceback
        traceback.print_exc()

    finally:
        print("\n🧹 Temizlik yapılıyor...")
        if browser:
            await browser.close()
        if playwright:
            await playwright.stop()
        print("✅ Bitti")

if __name__ == '__main__':
    # Windows UTF-8 fix
    if sys.platform == 'win32':
        try:
            sys.stdout.reconfigure(encoding='utf-8')
        except:
            pass

    asyncio.run(test_chatgpt())
    print("\n✅ Script tamamlandı - Chrome kapatıldı")
