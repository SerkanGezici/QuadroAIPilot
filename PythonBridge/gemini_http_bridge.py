#!/usr/bin/env python3
"""
Gemini HTTP Bridge - QuadroAIPilot için
ChatGPT bridge pattern'i kullanılarak Gemini için uyarlanmıştır
Basit HTTP server ile Gemini browser automation
"""

import asyncio
import json
import logging
import os
import sys
from datetime import datetime
from http.server import HTTPServer, BaseHTTPRequestHandler
from playwright.async_api import async_playwright
import threading

# Log klasörünü hazırla (AppData/QuadroAIPilot/Logs)
log_dir = os.path.join(os.getenv('LOCALAPPDATA'), 'QuadroAIPilot', 'Logs')
os.makedirs(log_dir, exist_ok=True)
log_file = os.path.join(log_dir, 'gemini_bridge.log')

# Logging - Windows console için UTF-8 encoding
logging.basicConfig(
    level=logging.INFO,
    format='[%(asctime)s] [%(levelname)s] %(message)s',
    datefmt='%H:%M:%S',
    handlers=[
        logging.FileHandler(log_file, encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)

# Windows console encoding fix (emoji desteği için)
if sys.platform == 'win32':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except:
        pass  # Python 3.7 veya öncesi desteklemeyebilir

logger = logging.getLogger(__name__)


class GeminiBridge:
    """Gemini Browser köprüsü"""

    def __init__(self):
        self.playwright = None
        self.browser = None
        self.page = None
        self.is_ready = False
        self.loop = None

    async def init_browser(self):
        """Playwright browser başlat"""
        try:
            logger.info("🚀 Playwright başlatılıyor...")
            self.playwright = await async_playwright().start()

            # Chrome profili ile kalıcı oturum (GİZLİ MOD - Arka planda çalışır)
            self.browser = await self.playwright.chromium.launch_persistent_context(
                user_data_dir='./gemini-profile',
                headless=False,  # False ama minimized/gizli
                viewport={'width': 840, 'height': 480},
                args=[
                    '--window-position=-2400,-2400',  # Ekran dışı pozisyon
                    '--disable-blink-features=AutomationControlled',
                    '--disable-dev-shm-usage',
                    '--no-sandbox',
                    '--disable-setuid-sandbox',
                    '--disable-accelerated-2d-canvas',
                    '--disable-gpu',
                    '--window-size=1,1',  # Minimum boyut (görünmez)
                    '--disable-background-timer-throttling',
                    '--disable-backgrounding-occluded-windows',
                    '--disable-renderer-backgrounding'
                ],
                timeout=120000,
                devtools=False
            )

            logger.info("📁 Chrome profili: ./gemini-profile")

            # Sayfa al veya oluştur
            pages = self.browser.pages
            if pages:
                self.page = pages[0]
                logger.info("📄 Mevcut sekme kullanılıyor")
            else:
                self.page = await self.browser.new_page()
                logger.info("📄 Yeni sekme oluşturuldu")

            # Page close event listener
            self.page.on('close', lambda: logger.warning("⚠️ Page closed unexpectedly!"))

            # Gemini'ye git
            logger.info("🌐 Gemini'ye bağlanılıyor...")
            await self.page.goto('https://gemini.google.com/app', wait_until='domcontentloaded', timeout=90000)

            # Network idle bekle
            try:
                await self.page.wait_for_load_state('networkidle', timeout=30000)
            except:
                logger.warning("⚠️ Network idle timeout (normal, devam ediliyor)")
                pass

            await self.page.wait_for_timeout(3000)

            # TÜM modal'ları başta kapat (bir kere) - DISABLED FOR TESTING
            # Popup kapatma mantığı devre dışı - sayfa doğal şekilde yüklensin
            logger.info("⏩ Modal dismissal DISABLED - letting page load naturally")
            # logger.info("🧹 Tüm modal'lar başta kapatılıyor...")
            # await self.dismiss_all_modals()
            # logger.info("✅ Modal temizliği tamamlandı!")

            # # Modal temizliğinden sonra page'in hala açık olduğunu doğrula
            # if self.page.is_closed():
            #     logger.error("❌ Page modal temizliği sırasında kapandı!")
            #     return False

            # Page health check: Temel elementleri kontrol et
            try:
                # Gemini input elementi var mı? (birden fazla selector dene)
                textarea_selectors = [
                    'div[contenteditable="true"][role="textbox"]',
                    'textarea',
                    'rich-textarea',
                    'div[contenteditable="true"]',
                    'div.ql-editor',  # Quill editor
                    'textarea[placeholder*="message"]',  # Placeholder içeren textarea
                ]

                logger.info("🔍 DEBUG: Textarea selector'ları deneniyor...")
                found = False
                for selector in textarea_selectors:
                    try:
                        await self.page.wait_for_selector(selector, timeout=5000)
                        logger.info(f"✅ Gemini input elementi bulundu: {selector}")
                        found = True
                        break
                    except:
                        logger.info(f"❌ Selector bulunamadı: {selector}")
                        continue

                if not found:
                    logger.warning("⚠️ Gemini input elementi bulunamadı, ama devam ediliyor...")
                    # DEBUG: Sayfadaki tüm contenteditable elementleri listele
                    try:
                        all_editables = await self.page.query_selector_all('[contenteditable]')
                        logger.info(f"🔍 DEBUG: Sayfada {len(all_editables)} contenteditable element var")
                        all_textareas = await self.page.query_selector_all('textarea')
                        logger.info(f"🔍 DEBUG: Sayfada {len(all_textareas)} textarea element var")
                    except:
                        pass
            except Exception as e:
                logger.warning(f"⚠️ Input check hatası: {e}")

            # Sistem promptu SİLİNDİ - İlk mesajda gönderilecek (hızlı health check için)
            self.is_ready = True
            logger.info("✅ Gemini browser hazır!")
            return True

        except Exception as e:
            logger.error(f"❌ Browser başlatma hatası: {e}")
            return False

    async def dismiss_all_modals(self):
        """TÜM modal'ları JavaScript ile DOM'dan sil (Gemini - login, signup, consent)"""
        try:
            modals_closed = False

            logger.info("🧹 Modal kapatma deneniyor...")

            # STEP 1: Login/Signup modal'ları JavaScript ile DOM'dan SİL
            try:
                await self.page.evaluate('''() => {
                    // Material Design dialog'ları (Google standart)
                    const dialogs = document.querySelectorAll('[role="dialog"], .mdc-dialog, dialog');
                    dialogs.forEach(dialog => {
                        const text = dialog.textContent.toLowerCase();
                        if (text.includes('sign in') || text.includes('log in') ||
                            text.includes('create account') || text.includes('get started') ||
                            text.includes('welcome') || text.includes('continue')) {
                            dialog.remove();
                        }
                    });

                    // Overlay backdrop'ları sil
                    const overlays = document.querySelectorAll('.mdc-dialog__scrim, [class*="backdrop"], [class*="overlay"]');
                    overlays.forEach(overlay => overlay.remove());

                    // Body scroll'u aç
                    document.body.style.overflow = 'auto';
                }''')

                logger.info("✅ Modal DOM temizliği yapıldı")
                modals_closed = True
            except Exception as e:
                logger.warning(f"⚠️ Modal silme hatası: {e}")

            # STEP 2: "Try Gemini" veya "Continue" butonuna tıkla
            try:
                # Birden fazla olası button text'i dene
                button_texts = ['Try Gemini', 'Continue', 'Skip', 'Get started', 'Start']
                for btn_text in button_texts:
                    try:
                        button = await self.page.query_selector(f'button:has-text("{btn_text}")')
                        if button:
                            await button.click()
                            logger.info(f"✅ '{btn_text}' butonuna tıklandı")
                            modals_closed = True
                            await self.page.wait_for_timeout(2000)
                            break
                    except:
                        continue
            except Exception as e:
                logger.warning(f"⚠️ Button click hatası: {e}")

            # STEP 3: ESC tuşlarına bas (diğer modal'lar için)
            for i in range(3):
                try:
                    await self.page.keyboard.press('Escape')
                    await self.page.wait_for_timeout(300)
                except:
                    pass

            # STEP 4: Body overflow fix
            try:
                await self.page.evaluate('document.body.style.overflow = "auto"')
            except:
                pass

            if modals_closed:
                logger.info("✅ Modal temizleme tamamlandı")
            else:
                logger.info("ℹ️ Modal bulunamadı (zaten açık)")

            return modals_closed

        except Exception as e:
            logger.warning(f"⚠️ Modal kapatma hatası (devam ediliyor): {e}")
            return False

    async def send_message(self, message):
        """Gemini'ye mesaj gönder"""
        try:
            if not self.page or not self.is_ready:
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Gemini browser hazır değil"
                }

            # Page health check
            if self.page.is_closed():
                logger.error("❌ Page kapalı, mesaj gönderilemez!")
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Page has been closed"
                }

            # Kullanıcı mesajını direkt gönder (sistem promptu YOK)
            logger.info(f"📤 Mesaj gönderiliyor: {message[:50]}...")

            # Textarea bul (birden fazla selector dene - ChatGPT pattern)
            textarea_selectors = [
                'div[contenteditable="true"][role="textbox"]',
                'textarea[placeholder*="Gemini"]',
                'textarea[aria-label*="Gemini"]',
                'rich-textarea',
                'textarea',
                'div[contenteditable="true"]',
                'div.ql-editor',  # Quill editor
                'textarea[placeholder*="message"]',  # Generic message input
            ]

            logger.info("🔍 DEBUG: Textarea aranıyor...")
            textarea_element = None
            used_selector = None
            for selector in textarea_selectors:
                try:
                    textarea_element = await self.page.query_selector(selector)
                    if textarea_element:
                        used_selector = selector
                        logger.info(f"✅ Textarea bulundu: {selector}")
                        break
                    else:
                        logger.info(f"❌ Selector bulunamadı: {selector}")
                except Exception as e:
                    logger.info(f"❌ Selector hatası ({selector}): {e}")
                    continue

            if not textarea_element:
                logger.error("❌ Textarea elementi bulunamadı!")
                # DEBUG: Sayfadaki tüm elementleri listele
                try:
                    all_editables = await self.page.query_selector_all('[contenteditable]')
                    logger.error(f"🔍 DEBUG: Sayfada {len(all_editables)} contenteditable element var")
                    all_textareas = await self.page.query_selector_all('textarea')
                    logger.error(f"🔍 DEBUG: Sayfada {len(all_textareas)} textarea element var")

                    # Sayfanın HTML'ini loglayalım (kısa versiyon)
                    page_html = await self.page.content()
                    logger.error(f"🔍 DEBUG: Sayfa HTML uzunluğu: {len(page_html)} karakter")
                except:
                    pass

                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Textarea not found"
                }

            # Mesaj göndermeden ÖNCE mevcut yanıt sayısını kaydet
            # (Böylece yeni yanıtı tespit edebiliriz)
            initial_response_count = 0
            try:
                initial_responses = await self.page.query_selector_all('message-content')
                initial_response_count = len(initial_responses)
                logger.info(f"📊 Mesaj göndermeden önce {initial_response_count} yanıt var")
            except:
                pass

            # Mesaj gönder (ChatGPT pattern: click + type + Enter)
            await textarea_element.click()
            await textarea_element.type(message)
            await self.page.keyboard.press('Enter')

            # Yanıt elementini bekle - ESKİ ÇALIŞAN SELECTOR (message-content)
            response_selector = 'message-content'

            logger.info("⏳ Yanıt bekleniyor...")
            logger.info(f"🔍 DEBUG: Selector kullanılıyor: {response_selector}")

            # İlk yanıt elementini yakala (timeout 2 dakika)
            response_element = None
            try:
                logger.info("🔍 DEBUG: wait_for_selector başladı...")
                await self.page.wait_for_selector(response_selector, timeout=120000)
                response_element = response_selector
                logger.info(f"✅ Yanıt elementi bulundu: {response_selector}")
            except Exception as e:
                logger.error(f"❌ wait_for_selector hatası: {e}")

                # DEBUG: Sayfadaki TÜM elementleri listele
                try:
                    logger.info("🔍 DEBUG: Sayfadaki tüm message-content elementlerini arıyorum...")
                    all_messages = await self.page.query_selector_all('message-content')
                    logger.info(f"🔍 DEBUG: {len(all_messages)} message-content bulundu")

                    # Alternatif selector'ları dene
                    alt_selectors = ['model-response', 'div[data-message-author-role]', '.message', '[role="presentation"]']
                    for alt_sel in alt_selectors:
                        try:
                            alt_elements = await self.page.query_selector_all(alt_sel)
                            logger.info(f"🔍 DEBUG: Alternatif '{alt_sel}': {len(alt_elements)} element")
                        except:
                            pass
                except Exception as dbg_ex:
                    logger.error(f"🔍 DEBUG hatası: {dbg_ex}")

            if not response_element:
                logger.error("❌ Yanıt elementi bulunamadı!")
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Response element not found"
                }

            # Streaming bitene kadar bekle (ChatGPT pattern)
            prev_length = 0
            stable_count = 0
            max_wait = 30  # 30 saniye
            polling_interval = 500  # 500ms

            for i in range(max_wait * 2):
                await self.page.wait_for_timeout(polling_interval)

                elements = await self.page.query_selector_all(response_element)
                if elements and len(elements) > initial_response_count:
                    # YENİ YANIT: initial_response_count'tan sonraki ilk eleman
                    new_response_index = initial_response_count
                    current_text = await elements[new_response_index].inner_text()
                    current_length = len(current_text)

                    if current_length == prev_length and current_length > 0:
                        stable_count += 1

                        # Kısa yanıtlar için early exit
                        if current_length < 100 and stable_count >= 1:
                            logger.info(f"✅ Kısa yanıt tamamlandı ({current_length} karakter)")
                            break

                        # Normal yanıtlar: 1 saniye stable
                        if stable_count >= 2:
                            logger.info(f"✅ Streaming tamamlandı ({current_length} karakter)")
                            break
                    else:
                        stable_count = 0

                    prev_length = current_length

                    # Log her 2 saniyede bir
                    if i % 4 == 0:
                        logger.info(f"📊 Streaming: {current_length} karakter (deneme {i+1}/{max_wait*2})")

            # Son yanıtı al - YENİ EKLENEN YANITI AL (eskiler değil!)
            elements = await self.page.query_selector_all(response_element)
            if elements and len(elements) > initial_response_count:
                # YENİ YANIT: initial_response_count'tan sonraki ilk eleman
                new_response_index = initial_response_count
                new_response_element = elements[new_response_index]
                response_text = await new_response_element.inner_text()

                logger.info(f"✅ Yanıt alındı: {len(response_text)} karakter (Yanıt #{new_response_index+1})")

                return {
                    "IsError": False,
                    "Content": response_text,
                    "ErrorMessage": None,
                    "timestamp": datetime.now().isoformat()
                }
            else:
                logger.error("❌ Yanıt elementi bulunamadı")
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Yanıt bulunamadı"
                }

        except Exception as e:
            logger.error(f"❌ Mesaj gönderme hatası: {e}")
            return {
                "IsError": True,
                "Content": None,
                "ErrorMessage": str(e)
            }

    async def close(self):
        """Browser kapat"""
        try:
            if self.browser:
                await self.browser.close()
            if self.playwright:
                await self.playwright.stop()
            logger.info("🛑 Browser kapatıldı")
        except Exception as e:
            logger.error(f"❌ Kapatma hatası: {e}")


# Global bridge instance
bridge = GeminiBridge()


class GeminiHandler(BaseHTTPRequestHandler):
    """HTTP Request Handler"""

    def log_message(self, format, *args):
        """Suppress default logging"""
        pass

    def do_GET(self):
        """Health check endpoint"""
        if self.path == '/health':
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            response = json.dumps({"status": "ok", "ready": bridge.is_ready})
            self.wfile.write(response.encode())
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        """Chat endpoint"""
        if self.path == '/chat':
            try:
                # JSON body oku
                content_length = int(self.headers['Content-Length'])
                body = self.rfile.read(content_length)
                data = json.loads(body.decode())

                message = data.get('message', '')

                # Async fonksiyonu sync olarak çalıştır
                loop = bridge.loop
                if loop and loop.is_running():
                    future = asyncio.run_coroutine_threadsafe(
                        bridge.send_message(message),
                        loop
                    )
                    result = future.result(timeout=300)  # 5 dakika timeout
                else:
                    result = {
                        "IsError": True,
                        "Content": None,
                        "ErrorMessage": "Event loop not running"
                    }

                # Yanıt gönder
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps(result).encode())

            except Exception as e:
                logger.error(f"❌ Request hatası: {e}")
                self.send_response(500)
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                error_response = json.dumps({
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": str(e)
                })
                self.wfile.write(error_response.encode())

        elif self.path == '/reset':
            # Session reset (şimdilik boş - Gemini context yönetimi için)
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"status": "ok"}).encode())

        elif self.path == '/shutdown':
            # Graceful shutdown endpoint
            logger.info("🛑 Shutdown isteği alındı, kapatılıyor...")

            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"status": "shutting down"}).encode())

            # Response gönderildikten SONRA kapat
            def shutdown_server():
                import time
                time.sleep(0.5)

                logger.info("🛑 Browser kapatılıyor...")
                try:
                    if bridge.loop and bridge.loop.is_running():
                        future = asyncio.run_coroutine_threadsafe(bridge.close(), bridge.loop)
                        future.result(timeout=5)
                        logger.info("✅ Browser kapatıldı")
                except Exception as e:
                    logger.warning(f"⚠️ Browser kapatma hatası (ignored): {e}")

                logger.info("🛑 Process sonlandırılıyor...")
                import os
                os._exit(0)

            threading.Thread(target=shutdown_server, daemon=True).start()

        else:
            self.send_response(404)
            self.end_headers()


async def run_async():
    """Async event loop"""
    global bridge

    # Browser başlat
    await bridge.init_browser()

    # Event loop'u çalışır tut
    while True:
        await asyncio.sleep(1)


def start_server():
    """HTTP server başlat"""
    server = HTTPServer(('127.0.0.1', 8766), GeminiHandler)  # ⚠️ Port: 8766 (ChatGPT: 8765)
    logger.info("🌐 HTTP Server başlatıldı: http://127.0.0.1:8766")
    server.serve_forever()


def main():
    """Main entry point"""
    logger.info("=" * 60)
    logger.info("🚀 Gemini HTTP Bridge - QuadroAIPilot")
    logger.info("=" * 60)

    # Event loop oluştur
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    bridge.loop = loop

    # Async task başlat (background)
    loop.run_in_executor(None, start_server)

    # Browser başlat ve event loop çalıştır
    try:
        loop.run_until_complete(run_async())
    except KeyboardInterrupt:
        logger.info("🛑 Kapatılıyor...")
        loop.run_until_complete(bridge.close())
        loop.close()


if __name__ == '__main__':
    main()
