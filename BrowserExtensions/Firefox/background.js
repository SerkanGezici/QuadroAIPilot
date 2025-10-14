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

// QuadroAI Pilot'a HTTP isteği gönder
async function triggerQuadroAI() {
  try {
    const response = await fetch('http://127.0.0.1:19741/trigger-read', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
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
      method: 'OPTIONS'
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