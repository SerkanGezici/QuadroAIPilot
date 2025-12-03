#!/usr/bin/env python3
"""
Unified AI HTTP Bridge - QuadroAIPilot için
Tek Chromium instance'da ChatGPT ve Gemini (2 sekme)
~400-600MB RAM tasarrufu sağlar
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
log_file = os.path.join(log_dir, 'unified_ai_bridge.log')

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
        pass

logger = logging.getLogger(__name__)


class UnifiedAIBridge:
    """
    Birleşik AI Browser köprüsü
    Tek Chromium instance, iki ayrı BrowserContext (ChatGPT + Gemini)
    """

    def __init__(self):
        self.playwright = None
        self.browser = None

        # ChatGPT
        self.chatgpt_context = None
        self.chatgpt_page = None
        self.chatgpt_ready = False

        # Gemini
        self.gemini_context = None
        self.gemini_page = None
        self.gemini_ready = False

        self.loop = None

    async def init_browser(self):
        """Tek Playwright browser başlat, iki context oluştur"""
        try:
            logger.info("=" * 60)
            logger.info("🚀 Unified AI Bridge başlatılıyor...")
            logger.info("=" * 60)

            self.playwright = await async_playwright().start()

            # TEK Chromium browser başlat (GİZLİ MOD - ekran dışı)
            logger.info("🌐 Chromium browser başlatılıyor (TEK INSTANCE)...")
            self.browser = await self.playwright.chromium.launch(
                headless=False,
                args=[
                    '--window-position=-10000,-10000',  # Ekran dışına taşı
                    '--start-minimized',
                    '--disable-blink-features=AutomationControlled',
                    '--disable-dev-shm-usage',
                    '--no-sandbox',
                    '--disable-setuid-sandbox',
                    '--disable-accelerated-2d-canvas',
                    '--disable-gpu',
                    '--window-size=840,480',
                    '--disable-background-timer-throttling',
                    '--disable-backgrounding-occluded-windows',
                    '--disable-renderer-backgrounding'
                ]
            )

            logger.info("✅ Chromium browser başlatıldı (TEK INSTANCE)")

            # İki ayrı BrowserContext oluştur (cookie izolasyonu için)
            # Her context kendi storage state'ini kullanır

            # 1. ChatGPT Context
            logger.info("📁 ChatGPT context oluşturuluyor...")
            chatgpt_storage_path = './unified-profile/chatgpt-storage.json'
            os.makedirs('./unified-profile', exist_ok=True)

            chatgpt_storage = None
            if os.path.exists(chatgpt_storage_path):
                try:
                    with open(chatgpt_storage_path, 'r') as f:
                        chatgpt_storage = json.load(f)
                    logger.info("✅ ChatGPT storage yüklendi")
                except:
                    logger.warning("⚠️ ChatGPT storage okunamadı, yeni oluşturulacak")

            self.chatgpt_context = await self.browser.new_context(
                viewport={'width': 840, 'height': 480},
                storage_state=chatgpt_storage if chatgpt_storage else None
            )

            # 2. Gemini Context
            logger.info("📁 Gemini context oluşturuluyor...")
            gemini_storage_path = './unified-profile/gemini-storage.json'

            gemini_storage = None
            if os.path.exists(gemini_storage_path):
                try:
                    with open(gemini_storage_path, 'r') as f:
                        gemini_storage = json.load(f)
                    logger.info("✅ Gemini storage yüklendi")
                except:
                    logger.warning("⚠️ Gemini storage okunamadı, yeni oluşturulacak")

            self.gemini_context = await self.browser.new_context(
                viewport={'width': 840, 'height': 480},
                storage_state=gemini_storage if gemini_storage else None
            )

            # Sayfaları paralel olarak başlat
            logger.info("🌐 ChatGPT ve Gemini sayfaları paralel başlatılıyor...")

            chatgpt_task = asyncio.create_task(self._init_chatgpt_page())
            gemini_task = asyncio.create_task(self._init_gemini_page())

            await asyncio.gather(chatgpt_task, gemini_task)

            logger.info("=" * 60)
            logger.info(f"✅ Unified AI Bridge hazır!")
            logger.info(f"   ChatGPT: {'✅ Hazır' if self.chatgpt_ready else '❌ Hazır değil'}")
            logger.info(f"   Gemini:  {'✅ Hazır' if self.gemini_ready else '❌ Hazır değil'}")
            logger.info("=" * 60)

            return True

        except Exception as e:
            logger.error(f"❌ Browser başlatma hatası: {e}")
            return False

    async def _init_chatgpt_page(self):
        """ChatGPT sayfasını başlat"""
        try:
            logger.info("🔵 ChatGPT sayfası başlatılıyor...")

            self.chatgpt_page = await self.chatgpt_context.new_page()
            self.chatgpt_page.on('close', lambda: logger.warning("⚠️ ChatGPT page closed!"))

            # ChatGPT'ye git
            chatgpt_url = 'https://chatgpt.com/?utm_source=quadro&utm_medium=app&utm_campaign=pilot'
            await self.chatgpt_page.goto(chatgpt_url, wait_until='domcontentloaded', timeout=90000)

            try:
                await self.chatgpt_page.wait_for_load_state('networkidle', timeout=30000)
            except:
                logger.warning("⚠️ ChatGPT network idle timeout (normal)")

            await self.chatgpt_page.wait_for_timeout(3000)

            # Modal'ları kapat
            await self._dismiss_chatgpt_modals()

            # Input elementi kontrol
            try:
                await self.chatgpt_page.wait_for_selector(
                    '#prompt-textarea, div.ProseMirror, textarea[name="prompt-textarea"]',
                    timeout=15000
                )
                logger.info("✅ ChatGPT input elementi bulundu")
            except:
                logger.warning("⚠️ ChatGPT input elementi bulunamadı")

            self.chatgpt_ready = True
            logger.info("🔵 ChatGPT sayfası hazır!")

        except Exception as e:
            logger.error(f"❌ ChatGPT sayfa hatası: {e}")
            self.chatgpt_ready = False

    async def _init_gemini_page(self):
        """Gemini sayfasını başlat"""
        try:
            logger.info("🟢 Gemini sayfası başlatılıyor...")

            self.gemini_page = await self.gemini_context.new_page()
            self.gemini_page.on('close', lambda: logger.warning("⚠️ Gemini page closed!"))

            # Gemini'ye git
            await self.gemini_page.goto('https://gemini.google.com/app', wait_until='domcontentloaded', timeout=90000)

            try:
                await self.gemini_page.wait_for_load_state('networkidle', timeout=30000)
            except:
                logger.warning("⚠️ Gemini network idle timeout (normal)")

            await self.gemini_page.wait_for_timeout(3000)

            # Input elementi kontrol
            textarea_selectors = [
                'div[contenteditable="true"][role="textbox"]',
                'rich-textarea',
                'div[contenteditable="true"]',
            ]

            found = False
            for selector in textarea_selectors:
                try:
                    await self.gemini_page.wait_for_selector(selector, timeout=2000)
                    logger.info(f"✅ Gemini input elementi bulundu: {selector}")
                    found = True
                    break
                except:
                    continue

            if not found:
                logger.warning("⚠️ Gemini input elementi bulunamadı")

            self.gemini_ready = True
            logger.info("🟢 Gemini sayfası hazır!")

        except Exception as e:
            logger.error(f"❌ Gemini sayfa hatası: {e}")
            self.gemini_ready = False

    async def _dismiss_chatgpt_modals(self):
        """ChatGPT modal'larını kapat (rate limit, login, signup, vb.)"""
        try:
            logger.info("🧹 ChatGPT modal kontrolü yapılıyor...")

            # TÜM modal'ları JavaScript ile DOM'dan sil
            modals_found = await self.chatgpt_page.evaluate('''() => {
                let found = [];

                // 1. Rate limit modal
                const rateLimit = document.querySelector('[data-testid="modal-no-auth-rate-limit"]');
                if (rateLimit) {
                    rateLimit.remove();
                    found.push('rate-limit');
                }

                // 2. Login/Signup modal (çeşitli varyasyonlar)
                const loginSelectors = [
                    '[data-testid="login-modal"]',
                    '[data-testid="signup-modal"]',
                    '[data-testid="auth-modal"]',
                    '[role="dialog"]',
                    '.modal',
                    '[class*="modal"]',
                    '[class*="Modal"]',
                    '[class*="dialog"]',
                    '[class*="Dialog"]',
                    '[class*="popup"]',
                    '[class*="Popup"]',
                    '[class*="overlay"][class*="auth"]',
                    '[class*="login"]',
                    '[class*="Login"]',
                    '[class*="signin"]',
                    '[class*="SignIn"]',
                    '[class*="signup"]',
                    '[class*="SignUp"]'
                ];

                loginSelectors.forEach(sel => {
                    try {
                        const elements = document.querySelectorAll(sel);
                        elements.forEach(el => {
                            // Modal içeriğini kontrol et (login/signup ile ilgili mi?)
                            const text = el.textContent.toLowerCase();
                            if (text.includes('log in') || text.includes('login') ||
                                text.includes('sign in') || text.includes('signin') ||
                                text.includes('sign up') || text.includes('signup') ||
                                text.includes('create account') || text.includes('get started') ||
                                text.includes('continue with') || text.includes('stay logged out') ||
                                text.includes('giriş yap') || text.includes('kayıt ol') ||
                                text.includes('hesap oluştur')) {
                                el.remove();
                                found.push('login-modal');
                            }
                        });
                    } catch(e) {}
                });

                // 3. Overlay/backdrop'ları sil
                const overlaySelectors = [
                    '[data-ignore-for-page-load="true"]',
                    '[class*="backdrop"]',
                    '[class*="Backdrop"]',
                    '[class*="overlay"]',
                    '[class*="Overlay"]',
                    '.fixed.inset-0',
                    '[class*="fixed"][class*="inset"]'
                ];

                overlaySelectors.forEach(sel => {
                    try {
                        const overlays = document.querySelectorAll(sel);
                        overlays.forEach(overlay => {
                            // Ana içerik değilse sil
                            if (!overlay.querySelector('main') && !overlay.querySelector('#__next')) {
                                const style = window.getComputedStyle(overlay);
                                if (style.position === 'fixed' || style.position === 'absolute') {
                                    overlay.remove();
                                    found.push('overlay');
                                }
                            }
                        });
                    } catch(e) {}
                });

                // 4. Body scroll'u aç
                document.body.style.overflow = 'auto';
                document.body.style.pointerEvents = 'auto';

                // 5. Tüm disabled/blocked elementleri aktif et
                document.querySelectorAll('[aria-hidden="true"]').forEach(el => {
                    if (el.tagName !== 'SCRIPT' && el.tagName !== 'STYLE') {
                        el.setAttribute('aria-hidden', 'false');
                    }
                });

                return found;
            }''')

            if modals_found and len(modals_found) > 0:
                logger.info(f"✅ ChatGPT modal'ları silindi: {modals_found}")

            # ESC tuşlarına bas (ek güvenlik)
            for _ in range(3):
                await self.chatgpt_page.keyboard.press('Escape')
                await self.chatgpt_page.wait_for_timeout(200)

            # Kısa bekleme (DOM güncellemesi için)
            await self.chatgpt_page.wait_for_timeout(500)

        except Exception as e:
            logger.warning(f"⚠️ ChatGPT modal kapatma hatası: {e}")

    async def send_chatgpt_message(self, message):
        """ChatGPT'ye mesaj gönder"""
        try:
            if not self.chatgpt_page or not self.chatgpt_ready:
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "ChatGPT browser hazır değil"
                }

            if self.chatgpt_page.is_closed():
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "ChatGPT page kapalı"
                }

            logger.info(f"🔵 [ChatGPT] Mesaj gönderiliyor: {message[:50]}...")

            # ÖNEMLİ: Mesaj göndermeden önce modal kontrolü yap
            # Login popup açılmış olabilir, kapatmamız lazım
            await self._dismiss_chatgpt_modals()

            # Textarea bul
            selectors = [
                ('#prompt-textarea', 'ProseMirror ID'),
                ('div.ProseMirror[contenteditable="true"]', 'ProseMirror class'),
                ('textarea[name="prompt-textarea"]', 'Named textarea'),
                ('div[contenteditable="true"]', 'Generic contenteditable'),
            ]

            textarea_selector = None
            for selector, desc in selectors:
                try:
                    await self.chatgpt_page.wait_for_selector(selector, timeout=10000)
                    textarea_selector = selector
                    logger.info(f"✅ ChatGPT input: {desc}")
                    break
                except:
                    continue

            if not textarea_selector:
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "ChatGPT input bulunamadı"
                }

            # Mesaj gönder
            element = await self.chatgpt_page.query_selector(textarea_selector)
            await element.click()
            await element.type(message)
            await self.chatgpt_page.keyboard.press('Enter')

            # Yanıt bekle
            response_selector = 'div[data-message-author-role="assistant"]'
            await self.chatgpt_page.wait_for_selector(response_selector, timeout=120000)

            # Streaming bitene kadar bekle
            prev_length = 0
            stable_count = 0

            for i in range(60):  # 30 saniye
                await self.chatgpt_page.wait_for_timeout(500)

                elements = await self.chatgpt_page.query_selector_all(response_selector)
                if elements:
                    current_text = await elements[-1].inner_text()
                    current_length = len(current_text)

                    if current_length == prev_length and current_length > 0:
                        stable_count += 1
                        if current_length < 100 and stable_count >= 1:
                            break
                        if stable_count >= 2:
                            break
                    else:
                        stable_count = 0

                    prev_length = current_length

            # Yanıt al
            elements = await self.chatgpt_page.query_selector_all(response_selector)
            if elements:
                response_text = await elements[-1].inner_text()
                logger.info(f"🔵 [ChatGPT] Yanıt: {len(response_text)} karakter")

                # Storage state'i kaydet
                await self._save_chatgpt_storage()

                return {
                    "IsError": False,
                    "Content": response_text,
                    "ErrorMessage": None,
                    "timestamp": datetime.now().isoformat()
                }

            return {
                "IsError": True,
                "Content": None,
                "ErrorMessage": "ChatGPT yanıt bulunamadı"
            }

        except Exception as e:
            logger.error(f"❌ [ChatGPT] Mesaj hatası: {e}")
            return {
                "IsError": True,
                "Content": None,
                "ErrorMessage": str(e)
            }

    async def send_gemini_message(self, message):
        """Gemini'ye mesaj gönder"""
        try:
            if not self.gemini_page or not self.gemini_ready:
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Gemini browser hazır değil"
                }

            if self.gemini_page.is_closed():
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Gemini page kapalı"
                }

            logger.info(f"🟢 [Gemini] Mesaj gönderiliyor: {message[:50]}...")

            # Textarea bul
            textarea_selectors = [
                'div[contenteditable="true"][role="textbox"]',
                'rich-textarea',
                'div[contenteditable="true"]',
            ]

            textarea_element = None
            for selector in textarea_selectors:
                try:
                    textarea_element = await self.gemini_page.query_selector(selector)
                    if textarea_element:
                        logger.info(f"✅ Gemini input: {selector}")
                        break
                except:
                    continue

            if not textarea_element:
                return {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Gemini input bulunamadı"
                }

            # Mevcut yanıt sayısını kaydet
            initial_responses = await self.gemini_page.query_selector_all('message-content')
            initial_count = len(initial_responses)

            # Mesaj gönder
            await textarea_element.click()
            await textarea_element.type(message)
            await self.gemini_page.keyboard.press('Enter')

            # Yanıt bekle
            await self.gemini_page.wait_for_selector('message-content', timeout=120000)

            # Streaming bitene kadar bekle
            prev_length = 0
            stable_count = 0

            for i in range(60):  # 30 saniye
                await self.gemini_page.wait_for_timeout(500)

                elements = await self.gemini_page.query_selector_all('message-content')
                if elements and len(elements) > initial_count:
                    new_response = elements[initial_count]
                    current_text = await new_response.inner_text()
                    current_length = len(current_text)

                    if current_length == prev_length and current_length > 0:
                        stable_count += 1
                        if current_length < 100 and stable_count >= 1:
                            break
                        if stable_count >= 2:
                            break
                    else:
                        stable_count = 0

                    prev_length = current_length

            # Yanıt al
            elements = await self.gemini_page.query_selector_all('message-content')
            if elements and len(elements) > initial_count:
                response_text = await elements[initial_count].inner_text()
                logger.info(f"🟢 [Gemini] Yanıt: {len(response_text)} karakter")

                # Storage state'i kaydet
                await self._save_gemini_storage()

                return {
                    "IsError": False,
                    "Content": response_text,
                    "ErrorMessage": None,
                    "timestamp": datetime.now().isoformat()
                }

            return {
                "IsError": True,
                "Content": None,
                "ErrorMessage": "Gemini yanıt bulunamadı"
            }

        except Exception as e:
            logger.error(f"❌ [Gemini] Mesaj hatası: {e}")
            return {
                "IsError": True,
                "Content": None,
                "ErrorMessage": str(e)
            }

    async def _save_chatgpt_storage(self):
        """ChatGPT storage state'i kaydet"""
        try:
            storage = await self.chatgpt_context.storage_state()
            with open('./unified-profile/chatgpt-storage.json', 'w') as f:
                json.dump(storage, f)
        except Exception as e:
            logger.warning(f"⚠️ ChatGPT storage kaydetme hatası: {e}")

    async def _save_gemini_storage(self):
        """Gemini storage state'i kaydet"""
        try:
            storage = await self.gemini_context.storage_state()
            with open('./unified-profile/gemini-storage.json', 'w') as f:
                json.dump(storage, f)
        except Exception as e:
            logger.warning(f"⚠️ Gemini storage kaydetme hatası: {e}")

    async def close(self):
        """Browser ve context'leri kapat"""
        try:
            # Storage state'leri kaydet
            await self._save_chatgpt_storage()
            await self._save_gemini_storage()

            # Context'leri kapat
            if self.chatgpt_context:
                await self.chatgpt_context.close()
            if self.gemini_context:
                await self.gemini_context.close()

            # Browser'ı kapat
            if self.browser:
                await self.browser.close()
            if self.playwright:
                await self.playwright.stop()

            logger.info("🛑 Unified AI Bridge kapatıldı")
        except Exception as e:
            logger.error(f"❌ Kapatma hatası: {e}")


# Global bridge instance
bridge = UnifiedAIBridge()


class UnifiedAIHandler(BaseHTTPRequestHandler):
    """HTTP Request Handler - Tek port, birden fazla endpoint"""

    def log_message(self, format, *args):
        """Suppress default logging"""
        pass

    def do_GET(self):
        """Health check endpoints"""
        if self.path == '/health':
            # Genel health check
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            response = json.dumps({
                "status": "ok",
                "chatgpt_ready": bridge.chatgpt_ready,
                "gemini_ready": bridge.gemini_ready
            })
            self.wfile.write(response.encode())

        elif self.path == '/chatgpt/health':
            # ChatGPT health check
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            response = json.dumps({"status": "ok", "ready": bridge.chatgpt_ready})
            self.wfile.write(response.encode())

        elif self.path == '/gemini/health':
            # Gemini health check
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            response = json.dumps({"status": "ok", "ready": bridge.gemini_ready})
            self.wfile.write(response.encode())

        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        """Chat endpoints"""

        # ChatGPT chat
        if self.path == '/chatgpt/chat' or self.path == '/chat':
            self._handle_chat_request(bridge.send_chatgpt_message)

        # Gemini chat
        elif self.path == '/gemini/chat':
            self._handle_chat_request(bridge.send_gemini_message)

        # Reset endpoints
        elif self.path in ['/reset', '/chatgpt/reset', '/gemini/reset']:
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"status": "ok"}).encode())

        # Shutdown
        elif self.path == '/shutdown':
            self._handle_shutdown()

        else:
            self.send_response(404)
            self.end_headers()

    def _handle_chat_request(self, send_func):
        """Chat request'i işle"""
        try:
            content_length = int(self.headers['Content-Length'])
            body = self.rfile.read(content_length)
            data = json.loads(body.decode())

            message = data.get('message', '')

            loop = bridge.loop
            if loop and loop.is_running():
                future = asyncio.run_coroutine_threadsafe(send_func(message), loop)
                result = future.result(timeout=300)
            else:
                result = {
                    "IsError": True,
                    "Content": None,
                    "ErrorMessage": "Event loop not running"
                }

            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps(result).encode())

        except Exception as e:
            logger.error(f"❌ Request hatası: {e}")
            self.send_response(500)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({
                "IsError": True,
                "Content": None,
                "ErrorMessage": str(e)
            }).encode())

    def _handle_shutdown(self):
        """Graceful shutdown"""
        logger.info("🛑 Shutdown isteği alındı...")

        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps({"status": "shutting down"}).encode())

        def shutdown_server():
            import time
            time.sleep(0.5)

            logger.info("🛑 Browser kapatılıyor...")
            try:
                if bridge.loop and bridge.loop.is_running():
                    future = asyncio.run_coroutine_threadsafe(bridge.close(), bridge.loop)
                    future.result(timeout=5)
            except Exception as e:
                logger.warning(f"⚠️ Browser kapatma hatası: {e}")

            logger.info("🛑 Process sonlandırılıyor...")
            os._exit(0)

        threading.Thread(target=shutdown_server, daemon=True).start()


async def run_async():
    """Async event loop"""
    global bridge
    await bridge.init_browser()

    while True:
        await asyncio.sleep(1)


def start_server():
    """HTTP server başlat"""
    server = HTTPServer(('127.0.0.1', 8765), UnifiedAIHandler)
    logger.info("🌐 Unified AI HTTP Server: http://127.0.0.1:8765")
    logger.info("   📌 ChatGPT: POST /chatgpt/chat veya /chat")
    logger.info("   📌 Gemini:  POST /gemini/chat")
    logger.info("   📌 Health:  GET /health, /chatgpt/health, /gemini/health")
    server.serve_forever()


def main():
    """Main entry point"""
    logger.info("=" * 60)
    logger.info("🚀 Unified AI HTTP Bridge - QuadroAIPilot")
    logger.info("   Tek Chromium, 2 Sekme (ChatGPT + Gemini)")
    logger.info("   ~400-600MB RAM tasarrufu")
    logger.info("=" * 60)

    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    bridge.loop = loop

    loop.run_in_executor(None, start_server)

    try:
        loop.run_until_complete(run_async())
    except KeyboardInterrupt:
        logger.info("🛑 Kapatılıyor...")
        loop.run_until_complete(bridge.close())
        loop.close()


if __name__ == '__main__':
    main()
