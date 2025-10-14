// Context menu oluştur
browser.runtime.onInstalled.addListener(() => {
  browser.contextMenus.create({
    id: "quadroai-read",
    title: "QuadroAI ile Oku",
    contexts: ["selection"]
  });
});

// Context menu tıklaması
browser.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === "quadroai-read" && info.selectionText) {
    try {
      // Seçili metni kopyala
      await browser.tabs.executeScript(tab.id, {
        code: copySelectedText.toString() + '; copySelectedText();'
      });

      // QuadroAI Pilot'a sinyal gönder
      await triggerQuadroAI();
      
    } catch (error) {
      console.error('QuadroAI okuma hatası:', error);
    }
  }
});

// Seçili metni panoya kopyala
function copySelectedText() {
  // Mevcut seçimi koru
  const selection = window.getSelection();
  const selectedText = selection.toString();
  
  if (selectedText) {
    // Geçici textarea oluştur
    const textarea = document.createElement('textarea');
    textarea.value = selectedText;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    
    // Metni seç ve kopyala
    textarea.select();
    document.execCommand('copy');
    
    // Temizle
    document.body.removeChild(textarea);
    
    // Orijinal seçimi geri yükle
    if (selection.rangeCount > 0) {
      const range = selection.getRangeAt(0);
      selection.removeAllRanges();
      selection.addRange(range);
    }
  }
}

// SECURITY: Shared authentication token (must match C# server)
const AUTH_TOKEN = "QuadroAI-f7a3c9d8-4e2b-11ef-9a1c-0242ac120002";

// QuadroAI Pilot'a HTTP isteği gönder
async function triggerQuadroAI() {
  try {
    const response = await fetch('http://127.0.0.1:19741/trigger-read', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${AUTH_TOKEN}`  // SECURITY FIX: Token authentication
      },
      body: JSON.stringify({
        action: 'read-clipboard',
        source: 'firefox-extension'
      })
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const result = await response.json();
    console.log('QuadroAI yanıtı:', result);
    
  } catch (error) {
    console.error('QuadroAI bağlantı hatası:', error);
    
    // Kullanıcıya bilgi ver
    browser.notifications.create({
      type: 'basic',
      iconUrl: 'icon48.png',
      title: 'QuadroAI Pilot',
      message: 'QuadroAI Pilot uygulaması çalışmıyor olabilir. Lütfen uygulamanın açık olduğundan emin olun.'
    });
  }
}

// Extension icon tıklaması
browser.browserAction.onClicked.addListener(async (tab) => {
  // Icon'a tıklandığında QuadroAI'ın çalışıp çalışmadığını kontrol et
  try {
    const response = await fetch('http://127.0.0.1:19741/trigger-read', {
      method: 'OPTIONS',
      headers: {
        'Authorization': `Bearer ${AUTH_TOKEN}`  // SECURITY FIX: Token for health check
      }
    });

    if (response.ok) {
      browser.notifications.create({
        type: 'basic',
        iconUrl: 'icon48.png',
        title: 'QuadroAI Pilot',
        message: 'Bağlantı başarılı! Metin seçip sağ tık yaparak kullanabilirsiniz.'
      });
    }
  } catch (error) {
    browser.notifications.create({
      type: 'basic',
      iconUrl: 'icon48.png',
      title: 'QuadroAI Pilot',
      message: 'QuadroAI Pilot uygulaması bulunamadı. Lütfen uygulamayı başlatın.'
    });
  }
});

// ============================================
// TAB TRACKING VE KAPATMA SİSTEMİ
// ============================================

// QuadroAI tarafından açılan tabları takip eden Map
// keyword → {tabId, url, title, openedAt}
const quadroTabs = new Map();

// Tab kapatma endpoint'i - C# tarafından HTTP POST ile çağrılır
browser.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.command === 'closeTab') {
    handleCloseTab(request.criteria)
      .then(result => sendResponse(result))
      .catch(error => sendResponse({ success: false, error: error.message }));
    return true; // Async response için
  } else if (request.command === 'trackTab') {
    handleTrackTab(request)
      .then(result => sendResponse(result))
      .catch(error => sendResponse({ success: false, error: error.message }));
    return true;
  } else if (request.command === 'getTrackedTabs') {
    const tabs = Array.from(quadroTabs.entries()).map(([keyword, data]) => ({
      keyword,
      ...data
    }));
    sendResponse({ success: true, tabs });
    return false;
  }
});

// Tab tracking fonksiyonu - yeni tab açıldığında çağrılır
async function handleTrackTab(request) {
  try {
    const { keyword, url, tabId } = request;

    // Tab bilgilerini al
    const tab = tabId ? await browser.tabs.get(tabId) : null;

    // QuadroTabs'a kaydet
    quadroTabs.set(keyword.toLowerCase(), {
      tabId: tabId || null,
      url: url,
      title: tab ? tab.title : '',
      openedAt: Date.now()
    });

    console.log(`[QuadroAI] Tab tracked: ${keyword} → ${url} (TabID: ${tabId})`);

    return { success: true, keyword, tabId };
  } catch (error) {
    console.error('[QuadroAI] Track tab error:', error);
    return { success: false, error: error.message };
  }
}

// Tab kapatma fonksiyonu - sesli komuttan gelen isteği işler
async function handleCloseTab(criteria) {
  try {
    const { keyword, urlPattern } = criteria;
    const normalizedKeyword = keyword.toLowerCase();

    console.log(`[QuadroAI] Closing tab: keyword="${keyword}", pattern="${urlPattern}"`);

    // Öncelik 1: QuadroAI tarafından açılan ve keyword ile eşleşen tab
    if (quadroTabs.has(normalizedKeyword)) {
      const trackedTab = quadroTabs.get(normalizedKeyword);

      try {
        // Tab hala açık mı kontrol et
        const tab = await browser.tabs.get(trackedTab.tabId);

        // Tab varsa kapat
        await browser.tabs.remove(trackedTab.tabId);

        // Tracking'den kaldır
        quadroTabs.delete(normalizedKeyword);

        console.log(`[QuadroAI] ✓ Tracked tab closed: ${keyword} (TabID: ${trackedTab.tabId})`);

        return {
          success: true,
          closedTab: {
            tabId: trackedTab.tabId,
            title: tab.title,
            url: tab.url,
            source: 'tracked'
          }
        };
      } catch (error) {
        // Tab bulunamadı (muhtemelen kullanıcı manuel kapattı)
        console.log(`[QuadroAI] Tracked tab not found, removing from tracking: ${keyword}`);
        quadroTabs.delete(normalizedKeyword);
        // Fallback: tüm tabları ara
      }
    }

    // Öncelik 2: Tüm açık tablarda URL pattern ile ara
    const allTabs = await browser.tabs.query({});

    for (const tab of allTabs) {
      if (matchesPattern(tab.url, urlPattern) || matchesKeyword(tab.title, keyword)) {
        await browser.tabs.remove(tab.id);

        console.log(`[QuadroAI] ✓ Tab closed by pattern: ${tab.title} (TabID: ${tab.id})`);

        return {
          success: true,
          closedTab: {
            tabId: tab.id,
            title: tab.title,
            url: tab.url,
            source: 'pattern'
          }
        };
      }
    }

    // Tab bulunamadı
    console.log(`[QuadroAI] ✗ Tab not found: ${keyword}`);
    return {
      success: false,
      error: 'Tab not found',
      keyword
    };

  } catch (error) {
    console.error('[QuadroAI] Close tab error:', error);
    return {
      success: false,
      error: error.message
    };
  }
}

// URL pattern matching fonksiyonu
function matchesPattern(url, pattern) {
  if (!url || !pattern) return false;

  // Wildcard pattern'i regex'e çevir
  // "*://*.hurriyet.com.tr/*" → /^.*:\/\/.*\.hurriyet\.com\.tr\/.*$/
  const regexPattern = pattern
    .replace(/\./g, '\\.')
    .replace(/\*/g, '.*');

  const regex = new RegExp(`^${regexPattern}$`, 'i');
  return regex.test(url);
}

// Keyword matching fonksiyonu (title içinde arama)
function matchesKeyword(title, keyword) {
  if (!title || !keyword) return false;

  // Türkçe karakter normalizasyonu
  const normalizedTitle = normalizeTurkish(title.toLowerCase());
  const normalizedKeyword = normalizeTurkish(keyword.toLowerCase());

  return normalizedTitle.includes(normalizedKeyword);
}

// Türkçe karakter normalizasyonu
function normalizeTurkish(text) {
  return text
    .replace(/ı/g, 'i').replace(/İ/g, 'I')
    .replace(/ğ/g, 'g').replace(/Ğ/g, 'G')
    .replace(/ü/g, 'u').replace(/Ü/g, 'U')
    .replace(/ş/g, 's').replace(/Ş/g, 'S')
    .replace(/ö/g, 'o').replace(/Ö/g, 'O')
    .replace(/ç/g, 'c').replace(/Ç/g, 'C');
}

// Tab güncellendiğinde tracking'i güncelle
browser.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === 'complete') {
    // Eğer bu tab tracking'de varsa, title'ı güncelle
    for (const [keyword, data] of quadroTabs.entries()) {
      if (data.tabId === tabId) {
        data.title = tab.title;
        console.log(`[QuadroAI] Tab updated: ${keyword} → ${tab.title}`);
        break;
      }
    }
  }
});

// Tab kapatıldığında tracking'den kaldır
browser.tabs.onRemoved.addListener((tabId, removeInfo) => {
  for (const [keyword, data] of quadroTabs.entries()) {
    if (data.tabId === tabId) {
      quadroTabs.delete(keyword);
      console.log(`[QuadroAI] Tab removed from tracking: ${keyword}`);
      break;
    }
  }
});