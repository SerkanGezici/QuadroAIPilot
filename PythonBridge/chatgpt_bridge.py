#!/usr/bin/env python3
"""
🚀 Quadro Pilot - ChatGPT Python Bridge
Dashboard ←→ Python Server ←→ ChatGPT Browser

WebSocket sunucusu üzerinden Dashboard ile ChatGPT arasında köprü kurar.
Playwright ile ChatGPT browser'ını otomatik kontrol eder.
"""

import asyncio
import json
import logging
import sys
from datetime import datetime
from playwright.async_api import async_playwright, TimeoutError as PlaywrightTimeout
import websockets

# Logging ayarları
logging.basicConfig(
    level=logging.INFO,
    format='[%(asctime)s] [%(levelname)s] %(message)s',
    datefmt='%H:%M:%S',
    handlers=[
        logging.FileHandler('chatgpt_bridge.log', encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)

logger = logging.getLogger(__name__)


class ChatGPTBridge:
    """ChatGPT Browser köprüsü"""

    def __init__(self):
        self.playwright = None
        self.browser = None
        self.page = None
        self.clients = set()
        self.is_ready = False

    async def init_browser(self):
        """Playwright browser'ı başlat"""
        try:
            logger.info("🚀 Playwright başlatılıyor...")
            self.playwright = await async_playwright().start()

            # Chrome profili ile kalıcı oturum
            self.browser = await self.playwright.chromium.launch_persistent_context(
                user_data_dir='./chrome-profile',
                headless=False,  # Görünür mod (Cloudflare bypass için)
                viewport={'width': 800, 'height': 600},  # Kompakt pencere boyutu
                args=[
                    '--disable-blink-features=AutomationControlled',
                    '--disable-gpu',
                    '--no-sandbox',
                    '--disable-dev-shm-usage',
                    '--window-size=800,600',  # Pencere boyutunu da ayarla
                    '--window-position=100,100'  # Ekranın sol üstüne konumlandır
                ],
                timeout=60000  # 60 saniye timeout
            )

            logger.info("📁 Chrome profili kullanılıyor: ./chrome-profile")

            # Mevcut sayfa varsa kullan, yoksa yeni sayfa
            pages = self.browser.pages
            if pages:
                self.page = pages[0]
                logger.info("📄 Mevcut sekme kullanılıyor")
            else:
                self.page = await self.browser.new_page()
                logger.info("📄 Yeni sekme oluşturuldu")

            # ChatGPT'ye git
            logger.info("🌐 ChatGPT'ye bağlanılıyor...")
            await self.page.goto('https://chat.openai.com/', wait_until='domcontentloaded', timeout=30000)

            # Sayfanın tam yüklenmesini bekle
            await self.page.wait_for_load_state('networkidle', timeout=10000)
            logger.info("✅ ChatGPT sayfası yüklendi")

            # DOM hazır mı kontrol et
            await self.page.wait_for_timeout(2000)
            logger.info("✅ Sayfa DOM hazır")

            self.is_ready = True
            logger.info("✅ ChatGPT browser hazır!")

            return True

        except Exception as e:
            logger.error(f"❌ Browser başlatma hatası: {e}")
            return False

    async def send_message_to_chatgpt(self, message):
        """ChatGPT'ye mesaj gönder ve yanıtı al"""
        try:
            if not self.page or not self.is_ready:
                raise Exception("ChatGPT browser hazır değil")

            logger.info(f"📤 ChatGPT'ye mesaj gönderiliyor: {message[:50]}...")

            # Mevcut assistant mesajlarının sayısını al (önceki mesajlar)
            prev_count = await self.page.locator('[data-message-author-role="assistant"]').count()
            logger.info(f"🔍 Mesaj göndermeden ÖNCE mevcut assistant mesajları: {prev_count}")

            # Textarea'ya yaz
            textarea_selector = 'textarea[placeholder*="Message"], textarea#prompt-textarea, div[contenteditable="true"]'
            await self.page.fill(textarea_selector, message)
            await self.page.wait_for_timeout(500)

            # Enter tuşuna bas
            await self.page.press(textarea_selector, 'Enter')
            logger.info("✅ Mesaj gönderildi, yanıt bekleniyor...")

            # Yeni assistant mesajının gelmesini bekle
            response_text = ""
            max_wait = 60  # 60 saniye max
            start_time = asyncio.get_event_loop().time()

            while (asyncio.get_event_loop().time() - start_time) < max_wait:
                await self.page.wait_for_timeout(1000)

                # Yeni assistant mesaj sayısı
                current_count = await self.page.locator('[data-message-author-role="assistant"]').count()

                if current_count > prev_count:
                    logger.info(f"🔍 Yeni assistant mesajı geldi! (toplam: {current_count}, önceki: {prev_count})")

                    # En son assistant mesajını al
                    last_assistant = self.page.locator('[data-message-author-role="assistant"]').last

                    # İlk içerik geldi mi?
                    response_text = await last_assistant.inner_text()
                    if response_text and len(response_text) > 3:
                        logger.info(f"🔥 İlk içerik geldi: {response_text[:50]}...")

                        # Yanıt tamamlandı mı kontrol et (typing animasyonu bitti mi?)
                        # Stop button kayboldu mu?
                        stop_button = self.page.locator('button[aria-label*="Stop"]')
                        stop_count = await stop_button.count()

                        if stop_count == 0:
                            # Stop button yok = yanıt tamamlandı
                            # 2 saniye daha bekle (yanıt tamamen render edilsin)
                            logger.info("⏳ Stop button kayboldu, son render için 2s bekleniyor...")
                            await self.page.wait_for_timeout(2000)

                            # Yanıtı tekrar al (tamamen render edilmiş hali)
                            response_text = await last_assistant.inner_text()
                            logger.info(f"✅ Final yanıt alındı: {len(response_text)} karakter")
                            break

            duration = asyncio.get_event_loop().time() - start_time
            logger.info("✅ ChatGPT yanıtı tamamlandı")
            logger.info(f"📤 ChatGPT yanıtı: {response_text[:100]}... (duration: {duration:.1f}s)")

            return {
                'success': True,
                'content': response_text,
                'duration': duration
            }

        except Exception as e:
            logger.error(f"❌ Mesaj gönderme hatası: {e}")
            return {
                'success': False,
                'error': str(e)
            }

    async def handle_client(self, websocket):
        """WebSocket client bağlantısını işle"""
        client_id = id(websocket)
        self.clients.add(websocket)
        logger.info(f"📡 Yeni client bağlandı: {client_id}")

        try:
            # İlk durumu gönder
            await websocket.send(json.dumps({
                'type': 'status',
                'ready': self.is_ready,
                'timestamp': datetime.now().isoformat()
            }))

            # Client'dan gelen mesajları dinle
            async for message in websocket:
                try:
                    data = json.loads(message)
                    logger.info(f"📨 Client mesajı: {data.get('type')}")

                    if data.get('type') == 'send_to_chatgpt':
                        user_message = data.get('message', '')
                        logger.info(f"📤 ChatGPT'ye mesaj gönderiliyor: {user_message}...")

                        # ChatGPT'ye gönder
                        result = await self.send_message_to_chatgpt(user_message)

                        # Yanıtı client'a gönder
                        await websocket.send(json.dumps({
                            'type': 'chatgpt_response',
                            'success': result.get('success', False),
                            'content': result.get('content', ''),
                            'error': result.get('error'),
                            'duration': result.get('duration', 0),
                            'timestamp': datetime.now().isoformat()
                        }))

                except json.JSONDecodeError:
                    logger.warning("⚠️ JSON parse hatası")

        except websockets.exceptions.ConnectionClosed:
            logger.info("connection closed")
        finally:
            self.clients.discard(websocket)

    async def start_server(self, host='localhost', port=8765):
        """WebSocket sunucusunu başlat"""
        logger.info("╔═══════════════════════════════════════════════════════════╗")
        logger.info("║                                                           ║")
        logger.info("║        🚀 Quadro Pilot - ChatGPT Python Bridge 🚀        ║")
        logger.info("║                                                           ║")
        logger.info("║  Dashboard ←→ Python Server ←→ ChatGPT Browser           ║")
        logger.info("║                                                           ║")
        logger.info("╚═══════════════════════════════════════════════════════════╝")

        # Browser'ı başlat (asenkron, sunucuyu bloklamaz)
        asyncio.create_task(self._init_browser_with_retry())

        # WebSocket sunucusunu başlat
        logger.info(f"🚀 WebSocket sunucusu başlatılıyor: ws://{host}:{port}")
        async with websockets.serve(self.handle_client, host, port):
            logger.info(f"✅ WebSocket sunucusu hazır! ws://{host}:{port}")
            logger.info("📍 Dashboard'dan bağlanabilirsiniz.")
            await asyncio.Future()  # Sonsuza kadar çalış

    async def _init_browser_with_retry(self, max_retries=3):
        """Browser'ı retry mekanizması ile başlat"""
        for attempt in range(1, max_retries + 1):
            logger.info(f"🔄 Browser başlatma denemesi {attempt}/{max_retries}...")
            success = await self.init_browser()
            if success:
                # Client'lara ready=True bildir
                for client in self.clients:
                    try:
                        await client.send(json.dumps({
                            'type': 'status',
                            'ready': True,
                            'timestamp': datetime.now().isoformat()
                        }))
                    except:
                        pass
                logger.info("📢 Client'lara ready=True bildirildi")
                return

            if attempt < max_retries:
                logger.warning(f"⏳ 5 saniye sonra tekrar denenecek...")
                await asyncio.sleep(5)

        logger.error("❌ Browser başlatılamadı! WebSocket sunucusu çalışıyor ama ChatGPT bağlantısı yok.")

    async def cleanup(self):
        """Kaynakları temizle"""
        if self.browser:
            await self.browser.close()
        if self.playwright:
            await self.playwright.stop()


async def main():
    """Ana program"""
    bridge = ChatGPTBridge()

    try:
        await bridge.start_server(host='localhost', port=8765)
    except KeyboardInterrupt:
        logger.info("\n⏹️  Sunucu durduruluyor...")
    finally:
        await bridge.cleanup()
        logger.info("✅ Temizlik tamamlandı")


if __name__ == '__main__':
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n👋 Görüşürüz!")
