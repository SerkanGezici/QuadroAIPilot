#!/usr/bin/env python3
"""
Gemini DOM Debug Script
QuadroAIPilot için Gemini selector'larını tespit eder
ChatGPT test script pattern'i kullanılarak uyarlanmıştır
"""

import asyncio
import sys
from playwright.async_api import async_playwright

# Windows console encoding fix
if sys.platform == 'win32':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except:
        pass

async def test_gemini():
    """Gemini DOM yapısını analiz et"""
    print("=" * 60)
    print("🔬 GEMINI DOM DEBUG SCRIPT")
    print("=" * 60)

    playwright = await async_playwright().start()

    # Chrome persistent context (Gemini profili)
    print("\n🌐 Chrome başlatılıyor (Gemini profili)...")
    browser = await playwright.chromium.launch_persistent_context(
        user_data_dir='./gemini-profile',
        headless=False,  # Görünür mod (manuel modal kapatma için)
        viewport={'width': 1920, 'height': 1080},
        args=[
            '--disable-blink-features=AutomationControlled',
            '--disable-dev-shm-usage',
            '--no-sandbox'
        ]
    )

    page = browser.pages[0] if browser.pages else await browser.new_page()

    # Gemini'ye git
    print("🌐 Gemini'ye gidiliyor: https://gemini.google.com/app")
    await page.goto('https://gemini.google.com/app', wait_until='domcontentloaded', timeout=90000)

    print("⏳ Network idle bekleniyor...")
    try:
        await page.wait_for_load_state('networkidle', timeout=30000)
    except:
        print("⚠️ Network idle timeout (normal, devam ediliyor)")

    await page.wait_for_timeout(5000)

    print("\n" + "=" * 60)
    print("⏸️  MANUEL MODAL KAPATMA")
    print("=" * 60)
    print("Login modal, cookie consent vb. varsa manuel kapatın.")
    print("Gemini input alanı görünür hale gelince Enter basın...")
    input()

    # ========================================
    # ADIM 1: INPUT ELEMENT ANALİZİ
    # ========================================
    print("\n" + "=" * 60)
    print("🔍 INPUT ELEMENT ANALİZİ")
    print("=" * 60)

    all_inputs = await page.query_selector_all('input, textarea, div[contenteditable="true"]')
    print(f"\nToplam input/textarea/contenteditable elementi: {len(all_inputs)}")

    if len(all_inputs) == 0:
        print("⚠️ Hiç input elementi bulunamadı!")
    else:
        for idx, element in enumerate(all_inputs):
            try:
                tag = await element.evaluate('el => el.tagName')
                role = await element.get_attribute('role') or 'none'
                aria_label = await element.get_attribute('aria-label') or 'none'
                placeholder = await element.get_attribute('placeholder') or 'none'
                class_name = await element.get_attribute('class') or 'none'

                print(f"\n[INPUT-{idx}] {tag}")
                print(f"  role: {role}")
                print(f"  aria-label: {aria_label[:80] if len(aria_label) > 80 else aria_label}")
                print(f"  placeholder: {placeholder[:80] if len(placeholder) > 80 else placeholder}")
                print(f"  class: {class_name[:80] if len(class_name) > 80 else class_name}")
            except Exception as e:
                print(f"[INPUT-{idx}] Error: {e}")

    # ========================================
    # ADIM 2: MANUEL TEST MESAJ
    # ========================================
    print("\n" + "=" * 60)
    print("📤 MANUEL TEST MESAJ")
    print("=" * 60)
    print("Gemini input alanına 'test' yazın ve Enter/Send basın.")
    print("Yanıt geldikten sonra Enter basın...")
    input()

    await page.wait_for_timeout(3000)

    # ========================================
    # ADIM 3: RESPONSE ELEMENT ANALİZİ
    # ========================================
    print("\n" + "=" * 60)
    print("🔍 RESPONSE ELEMENT ANALİZİ")
    print("=" * 60)

    # Olası response container'ları ara
    response_candidates = await page.query_selector_all(
        'div[class*="message"], div[class*="response"], div[class*="content"], '
        'div[data-test-id], message-content, model-response'
    )

    print(f"\nToplam olası response elementi: {len(response_candidates)}")

    if len(response_candidates) == 0:
        print("⚠️ Hiç response elementi bulunamadı!")
    else:
        for idx, element in enumerate(response_candidates[:15]):  # İlk 15 tane
            try:
                tag = await element.evaluate('el => el.tagName')
                class_name = await element.get_attribute('class') or 'none'
                data_test = await element.get_attribute('data-test-id') or 'none'
                role = await element.get_attribute('role') or 'none'
                text = await element.inner_text()

                print(f"\n[RESPONSE-{idx}] {tag}")
                print(f"  class: {class_name[:100] if len(class_name) > 100 else class_name}")
                print(f"  data-test-id: {data_test}")
                print(f"  role: {role}")
                print(f"  text preview: {text[:150] if len(text) > 150 else text}")
            except Exception as e:
                print(f"[RESPONSE-{idx}] Error: {e}")

    # ========================================
    # ADIM 4: BUTTON ANALİZİ (Send button)
    # ========================================
    print("\n" + "=" * 60)
    print("🔍 BUTTON ANALİZİ (Send Button)")
    print("=" * 60)

    all_buttons = await page.query_selector_all('button')
    print(f"\nToplam button: {len(all_buttons)}")

    send_candidates = []
    for idx, button in enumerate(all_buttons):
        try:
            aria_label = await button.get_attribute('aria-label') or ''
            text = await button.inner_text()

            # Send/submit ile ilgili button'ları filtrele
            if 'send' in aria_label.lower() or 'submit' in aria_label.lower() or \
               'send' in text.lower() or 'submit' in text.lower():
                send_candidates.append((idx, button, aria_label, text))
        except:
            pass

    print(f"\nSend/Submit button adayları: {len(send_candidates)}")
    for idx, button, aria_label, text in send_candidates:
        class_name = await button.get_attribute('class') or 'none'
        print(f"\n[BUTTON-{idx}]")
        print(f"  aria-label: {aria_label}")
        print(f"  text: {text}")
        print(f"  class: {class_name[:80] if len(class_name) > 80 else class_name}")

    # ========================================
    # ADIM 5: SCREENSHOT ve HTML KAYDET
    # ========================================
    print("\n" + "=" * 60)
    print("💾 KAYIT İŞLEMLERİ")
    print("=" * 60)

    # Screenshot
    screenshot_path = 'gemini_dom_analysis.png'
    await page.screenshot(path=screenshot_path, full_page=True)
    print(f"✅ Screenshot kaydedildi: {screenshot_path}")

    # HTML source
    html_path = 'gemini_page_source.html'
    html = await page.content()
    with open(html_path, 'w', encoding='utf-8') as f:
        f.write(html)
    print(f"✅ HTML kaydedildi: {html_path}")

    # ========================================
    # SONUÇ
    # ========================================
    print("\n" + "=" * 60)
    print("✅ ANALİZ TAMAMLANDI")
    print("=" * 60)
    print("\nÖnemli dosyalar:")
    print(f"  - {screenshot_path} (ekran görüntüsü)")
    print(f"  - {html_path} (HTML kaynak kodu)")
    print("\nBu çıktıları inceleyerek doğru selector'ları belirleyin!")
    print("\nKAPATMAK İÇİN ENTER BASIN...")
    input()

    await browser.close()
    await playwright.stop()


if __name__ == '__main__':
    try:
        asyncio.run(test_gemini())
    except KeyboardInterrupt:
        print("\n🛑 Kullanıcı tarafından iptal edildi")
    except Exception as e:
        print(f"\n❌ Hata: {e}")
        import traceback
        traceback.print_exc()
