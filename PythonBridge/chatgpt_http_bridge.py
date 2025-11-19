#!/usr/bin/env python3
"""
ChatGPT HTTP Bridge - QuadroAIPilot için
Basit HTTP server ile ChatGPT browser automation
"""

import asyncio
import json
import logging
import sys
from datetime import datetime
from http.server import HTTPServer, BaseHTTPRequestHandler
from playwright.async_api import async_playwright
import threading

# Logging - Windows console için UTF-8 encoding
logging.basicConfig(
    level=logging.INFO,
    format='[%(asctime)s] [%(levelname)s] %(message)s',
    datefmt='%H:%M:%S',
    handlers=[
        logging.FileHandler('chatgpt_bridge.log', encoding='utf-8'),
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


class ChatGPTBridge:
    """ChatGPT Browser köprüsü"""

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

            # Chrome profili ile kalıcı oturum (GÖRÜNÜR MOD - ChatGPT login için)
            self.browser = await self.playwright.chromium.launch_persistent_context(
                user_data_dir='./chrome-profile',
                headless=False,  # ✅ GÖRÜNÜR: ChatGPT'ye giriş yapabilmek için pencere açık
                viewport={'width': 840, 'height': 480},  # Kompakt boyut
                args=[
                    '--disable-blink-features=AutomationControlled',
                    '--disable-gpu',
                    '--no-sandbox'
                ],
                timeout=60000
            )

            logger.info("📁 Chrome profili: ./chrome-profile")

            # Sayfa al veya oluştur
            pages = self.browser.pages
            if pages:
                self.page = pages[0]
                logger.info("📄 Mevcut sekme kullanılıyor")
            else:
                self.page = await self.browser.new_page()
                logger.info("📄 Yeni sekme oluşturuldu")

            # ChatGPT'ye git
            logger.info("🌐 ChatGPT'ye bağlanılıyor...")
            await self.page.goto('https://chat.openai.com/', wait_until='domcontentloaded', timeout=60000)

            # Network idle beklemek yerine load event bekle (daha güvenilir)
            try:
                await self.page.wait_for_load_state('networkidle', timeout=15000)
            except:
                logger.warning("⚠️ Network idle timeout (normal, devam ediliyor)")
                pass

            await self.page.wait_for_timeout(2000)

            # TÜM modal'ları başta kapat (bir kere) - MODAL KAPATMA SONRASI YENİ CHAT BAŞLATMASIN DİYE
            logger.info("🧹 Tüm modal'lar başta kapatılıyor...")
            await self.dismiss_all_modals()
            logger.info("✅ Modal temizliği tamamlandı!")

            self.is_ready = True
            logger.info("✅ ChatGPT browser hazır!")
            return True

        except Exception as e:
            logger.error(f"❌ Browser başlatma hatası: {e}")
            return False

    async def dismiss_all_modals(self):
        """TÜM modal'ları agresif şekilde kapat (rate limit, signup, chromium recovery)"""
        try:
            modals_closed = False

            # 1. Chromium "Sayfalar geri yüklensin mi?" modal'ını kapat
            try:
                chromium_modal = await self.page.query_selector('button:has-text("Geri yükle"), button:has-text("Restore")')
                if chromium_modal:
                    logger.info("⚠️ Chromium recovery modal bulundu, kapatılıyor...")
                    await self.page.keyboard.press('Escape')
                    await self.page.wait_for_timeout(500)
                    modals_closed = True
            except:
                pass

            # 2. ChatGPT rate limit modal'ını JavaScript ile ZORLA kapat
            try:
                modal = await self.page.query_selector('[data-testid="modal-no-auth-rate-limit"]')
                if modal:
                    logger.info("⚠️ ChatGPT rate limit modal bulundu, JavaScript ile ZORLA kapatılıyor...")

                    # JavaScript ile modal ve overlay'i DOM'dan SİL
                    await self.page.evaluate('''() => {
                        // Rate limit modal'ını bul ve sil
                        const modal = document.querySelector('[data-testid="modal-no-auth-rate-limit"]');
                        if (modal) {
                            modal.remove();
                        }

                        // Tüm overlay'leri bul ve sil (pointer-events engelleyenler)
                        const overlays = document.querySelectorAll('[class*="absolute"][class*="inset"]');
                        overlays.forEach(overlay => {
                            if (overlay.getAttribute('data-testid')?.includes('modal')) {
                                overlay.remove();
                            }
                        });

                        // body overflow'u geri aç (modal kapatınca açılır)
                        document.body.style.overflow = 'auto';
                    }''')

                    await self.page.wait_for_timeout(1000)
                    logger.info("✅ ChatGPT modal DOM'dan silindi!")
                    modals_closed = True
            except Exception as e:
                logger.warning(f"⚠️ ChatGPT modal silme hatası: {e}")

            # 3. Signup/Login modal'ını kapat
            try:
                signup_modal = await self.page.query_selector('button:has-text("Oturum aç"), button:has-text("Sign"), button:has-text("Ücretsiz kaydol")')
                if signup_modal:
                    logger.info("⚠️ Signup modal bulundu, kapatılıyor...")
                    await self.page.keyboard.press('Escape')
                    await self.page.wait_for_timeout(500)
                    modals_closed = True
            except:
                pass

            # 4. Son çare: Tüm modal overlay'leri JavaScript ile temizle
            if modals_closed:
                try:
                    await self.page.evaluate('''() => {
                        // Tüm "absolute inset-0" overlay'leri bul ve sil
                        document.querySelectorAll('[class*="absolute inset-0"]').forEach(el => {
                            if (el.getAttribute('data-ignore-for-page-load') === 'true') {
                                el.remove();
                            }
                        });
                    }''')
                except:
                    pass

            return modals_closed

        except Exception as e:
            logger.warning(f"⚠️ Modal kapatma hatası (devam ediliyor): {e}")
            return False

    async def dismiss_rate_limit_modal(self):
        """Rate limit modal'ını otomatik kapat (eski metod - yeni dismiss_all_modals kullan)"""
        return await self.dismiss_all_modals()

    async def send_message(self, message):
        """ChatGPT'ye mesaj gönder"""
        try:
            if not self.page or not self.is_ready:
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "ChatGPT browser hazır değil"
                }

            logger.info(f"📤 Mesaj gönderiliyor: {message[:50]}...")

            # NOT: Modal'lar init_browser()'da kapatıldı, tekrar kapatmaya gerek yok
            # Her mesajda modal kapatma yeni chat başlatabilir, bu yüzden YAPMA!

            # Textarea bul ve mesaj gönder (ChatGPT artık contenteditable div kullanıyor)
            # Önce textarea dene, yoksa contenteditable div kullan
            textarea_selector = None
            try:
                await self.page.wait_for_selector('textarea[placeholder*="Message"]', timeout=3000)
                textarea_selector = 'textarea[placeholder*="Message"]'
                logger.info("✅ Textarea bulundu (eski format)")
            except:
                await self.page.wait_for_selector('div[contenteditable="true"]', timeout=5000)
                textarea_selector = 'div[contenteditable="true"]'
                logger.info("✅ Contenteditable div bulundu (yeni format)")

            # Contenteditable div için type() kullan (headless modda daha güvenilir)
            element = await self.page.query_selector(textarea_selector)
            await element.click()  # Focus et
            await element.type(message)  # type() fill()'den daha güvenilir
            await self.page.keyboard.press('Enter')  # Klavye emülasyonu kullan

            # Yanıt elementini bekle
            response_selector = 'div[data-message-author-role="assistant"]'
            logger.info("⏳ Yanıt bekleniyor...")

            await self.page.wait_for_selector(response_selector, timeout=120000)  # 2 dakika
            logger.info("✅ Yanıt elementi bulundu, streaming bekleniyor...")

            # Streaming bitene kadar bekle (içerik uzunluğu sabitlenene kadar)
            prev_length = 0
            stable_count = 0
            max_wait = 60  # Maksimum 60 saniye bekle

            for i in range(max_wait):
                await self.page.wait_for_timeout(1000)  # 1 saniye bekle

                elements = await self.page.query_selector_all(response_selector)
                if elements:
                    current_text = await elements[-1].inner_text()
                    current_length = len(current_text)

                    if current_length == prev_length and current_length > 0:
                        stable_count += 1
                        if stable_count >= 2:  # 2 saniye sabit kalırsa streaming bitti
                            logger.info(f"✅ Streaming tamamlandı ({current_length} karakter)")
                            break
                    else:
                        stable_count = 0

                    prev_length = current_length
                    logger.info(f"📊 Streaming: {current_length} karakter (deneme {i+1}/{max_wait})")

            # Son yanıtı al
            elements = await self.page.query_selector_all(response_selector)
            if elements:
                last_element = elements[-1]
                response_text = await last_element.inner_text()

                logger.info(f"✅ Yanıt alındı: {len(response_text)} karakter")

                return {
                    "IsError": False,  # C# property ismi (PascalCase)
                    "Content": response_text,  # C# property ismi (PascalCase)
                    "ErrorMessage": None,  # C# property ismi (PascalCase)
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
bridge = ChatGPTBridge()


class ChatGPTHandler(BaseHTTPRequestHandler):
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
                    result = future.result(timeout=300)  # 5 dakika timeout (ChatGPT uzun yanıtlar için)
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
            # Session reset (şimdilik boş)
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"status": "ok"}).encode())

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
    server = HTTPServer(('127.0.0.1', 8765), ChatGPTHandler)
    logger.info("🌐 HTTP Server başlatıldı: http://127.0.0.1:8765")
    server.serve_forever()


def main():
    """Main entry point"""
    logger.info("=" * 60)
    logger.info("🚀 ChatGPT HTTP Bridge - QuadroAIPilot")
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
