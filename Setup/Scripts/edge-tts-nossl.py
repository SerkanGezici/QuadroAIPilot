#!/usr/bin/env python
"""edge-tts wrapper with SSL verification disabled"""
import sys
import ssl
import aiohttp

# Global SSL context'i patch et
original_create_default_context = ssl.create_default_context

def create_unverified_context(purpose=ssl.Purpose.SERVER_AUTH, *, cafile=None, capath=None, cadata=None):
    context = original_create_default_context(purpose, cafile=cafile, capath=capath, cadata=cadata)
    context.check_hostname = False
    context.verify_mode = ssl.CERT_NONE
    return context

ssl.create_default_context = create_unverified_context
ssl._create_default_https_context = ssl._create_unverified_context

# aiohttp TCPConnector'ı patch et
original_init = aiohttp.TCPConnector.__init__

def patched_init(self, *args, **kwargs):
    kwargs['ssl'] = False  # SSL doğrulamasını tamamen kapat
    original_init(self, *args, **kwargs)

aiohttp.TCPConnector.__init__ = patched_init

# edge-tts modülünü import et ve çalıştır
if __name__ == "__main__":
    from edge_tts.util import main
    main()
