#!/usr/bin/env python3
"""
ChatGPT Debug Test Script
Bu script ChatGPT bridge'in neden boş yanıt döndüğünü tespit eder
"""

import asyncio
import sys
from playwright.async_api import async_playwright

async def test_chatgpt():
    """ChatGPT'ye test mesajı gönder ve detaylı debug yap"""

    print("=" * 60)
    print("🔍 ChatGPT Debug Test Başlıyor...")
    print("=" * 60)

    playwright = None
    browser = None

    try:
        # 1. Playwright başlat
        print("\n[1/6] 🚀 Playwright başlatılıyor...")
        playwright = await async_playwright().start()
        print("✅ Playwright başlatıldı")

        # 2. Browser başlat (headless=False - pencere görünsün)
        print("\n[2/6] 🌐 Chrome başlatılıyor (pencere görünür mod)...")
        browser = await playwright.chromium.launch_persistent_context(
            user_data_dir='./chrome-profile',
            headless=False,  # Pencere görünsün - debug için
            viewport={'width': 1920, 'height': 1080},
            args=[
                '--disable-blink-features=AutomationControlled',
                '--disable-gpu',
                '--no-sandbox'
            ],
            timeout=60000
        )
        print("✅ Chrome başlatıldı")

        # 3. Sayfa al
        print("\n[3/6] 📄 Sayfa alınıyor...")
        pages = browser.pages
        if pages:
            page = pages[0]
            print(f"✅ Mevcut sayfa kullanılıyor (URL: {page.url})")
        else:
            page = await browser.new_page()
            print("✅ Yeni sayfa oluşturuldu")

        # 4. ChatGPT'ye git
        print("\n[4/6] 🌐 ChatGPT'ye bağlanılıyor...")
        current_url = page.url

        if 'chat.openai.com' not in current_url:
            print(f"⚠️ Şu anki URL: {current_url}")
            print("🔄 ChatGPT'ye yönlendiriliyor...")
            await page.goto('https://chat.openai.com/', wait_until='domcontentloaded', timeout=60000)
        else:
            print(f"✅ Zaten ChatGPT'de: {current_url}")

        # Network idle bekle (opsiyonel)
        try:
            await page.wait_for_load_state('networkidle', timeout=15000)
            print("✅ Sayfa tamamen yüklendi (networkidle)")
        except Exception as e:
            print(f"⚠️ Network idle timeout (normal, devam ediliyor): {e}")

        await page.wait_for_timeout(2000)

        # 5. Sayfanın durumunu kontrol et
        print("\n[5/6] 🔍 Sayfa analiz ediliyor...")

        # Login kontrolü
        try:
            login_button = await page.query_selector('button:has-text("Log in")')
            if login_button:
                print("❌ SORUN BULUNDU: ChatGPT'de login gerekiyor!")
                print("   👉 Lütfen browser penceresinde ChatGPT'ye giriş yapın")
                print("   👉 Giriş yaptıktan sonra Enter'a basın...")
                input()
                print("✅ Devam ediliyor...")
        except:
            print("✅ Login ekranı yok (zaten giriş yapılmış)")

        # Textarea kontrolü
        print("\n🔍 Textarea aranıyor...")
        textarea_selectors = [
            'textarea[placeholder*="Message"]',
            'textarea[id*="prompt"]',
            'textarea',
            '#prompt-textarea',
            'div[contenteditable="true"]'
        ]

        textarea_found = None
        for selector in textarea_selectors:
            try:
                element = await page.query_selector(selector)
                if element:
                    textarea_found = selector
                    print(f"✅ Textarea bulundu: {selector}")
                    break
            except:
                pass

        if not textarea_found:
            print("❌ SORUN: Textarea bulunamadı!")
            print("   📸 Sayfa screenshot'u alınıyor...")
            await page.screenshot(path='debug_no_textarea.png')
            print("   💾 Screenshot kaydedildi: debug_no_textarea.png")
            return

        # 6. Mesaj gönder
        print("\n[6/6] 📤 Test mesajı gönderiliyor...")
        test_message = "2 + 2 kaç eder?"

        print(f"   📝 Mesaj: '{test_message}'")
        await page.fill(textarea_found, test_message)
        print("   ✅ Mesaj yazıldı")

        await page.wait_for_timeout(500)
        await page.press(textarea_found, 'Enter')
        print("   ✅ Mesaj gönderildi (Enter basıldı)")

        # Yanıt bekle
        print("\n⏳ Yanıt bekleniyor...")
        await page.wait_for_timeout(3000)

        # Response selector'leri dene
        print("\n🔍 Yanıt aranıyor...")
        response_selectors = [
            'div[data-message-author-role="assistant"]',
            'div[data-testid*="conversation-turn"]',
            '.markdown',
            'article',
            'div.group'
        ]

        response_found = None
        response_text = None

        for selector in response_selectors:
            try:
                print(f"   🔍 Deneniyor: {selector}")
                await page.wait_for_selector(selector, timeout=30000)
                elements = await page.query_selector_all(selector)

                if elements:
                    print(f"   ✅ {len(elements)} element bulundu")
                    last_element = elements[-1]
                    text = await last_element.inner_text()

                    if text and len(text) > 0:
                        response_found = selector
                        response_text = text
                        print(f"   ✅ Yanıt bulundu! ({len(text)} karakter)")
                        break
            except Exception as e:
                print(f"   ⚠️ {selector} bulunamadı: {str(e)[:50]}")

        # Sonuç
        print("\n" + "=" * 60)
        print("📊 TEST SONUCU")
        print("=" * 60)

        if response_found and response_text:
            print(f"✅ BAŞARILI: Yanıt alındı!")
            print(f"   📍 Selector: {response_found}")
            print(f"   📏 Uzunluk: {len(response_text)} karakter")
            print(f"   📝 İlk 200 karakter:")
            print(f"   {response_text[:200]}")

            print("\n💡 ÖNERİ:")
            print(f"   chatgpt_http_bridge.py'de bu selector'ü kullanın:")
            print(f"   response_selector = '{response_found}'")
        else:
            print("❌ BAŞARISIZ: Yanıt alınamadı!")
            print("\n📸 Screenshot alınıyor...")
            await page.screenshot(path='debug_no_response.png')
            print("   💾 Screenshot: debug_no_response.png")

            # HTML kaydet
            html = await page.content()
            with open('debug_page.html', 'w', encoding='utf-8') as f:
                f.write(html)
            print("   💾 HTML: debug_page.html")

            print("\n💡 MANUEL KONTROL:")
            print("   1. Browser penceresinde yanıtı görebiliyor musunuz?")
            print("   2. Eğer görüyorsanız, Developer Tools açın (F12)")
            print("   3. Yanıt elementine sağ tıklayıp 'Inspect' yapın")
            print("   4. Element'in selector'ünü bulun")

        # Bekle (kullanıcı inceleyebilsin)
        print("\n⏸️  Browser açık kalacak - incelemek için Enter'a basın...")
        input()

    except Exception as e:
        print(f"\n❌ HATA: {e}")
        import traceback
        traceback.print_exc()

    finally:
        # Cleanup
        print("\n🧹 Temizlik yapılıyor...")
        if browser:
            await browser.close()
        if playwright:
            await playwright.stop()
        print("✅ Temizlendi")

if __name__ == '__main__':
    # Windows console encoding fix
    if sys.platform == 'win32':
        try:
            sys.stdout.reconfigure(encoding='utf-8')
            sys.stderr.reconfigure(encoding='utf-8')
        except:
            pass

    asyncio.run(test_chatgpt())
