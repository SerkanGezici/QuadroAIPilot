#!/usr/bin/env python3
"""
Creates simple blue square PNG icons for QuadroAI browser extensions
"""

import base64
import os

# 16x16 blue square PNG (base64 encoded)
icon_16 = b'''iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAB3RJTUUH6AELEwMAMpNT8QAAAB1pVFh0Q29tbWVudAAAAAAAQ3JlYXRlZCB3aXRoIEdJTVBkLmUHAAAAMklEQVQ4y2NkYGD4z0ABYBw1gIGB4T8jhgJ0CSwGkKWZZBcQbQApBjAwMDCMGjBoDAAAJbYGAR1XarcAAAAASUVORK5CYII='''

# 48x48 blue square PNG (base64 encoded)
icon_48 = b'''iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAB3RJTUUH6AELEwQK5/P8kAAAAB1pVFh0Q29tbWVudAAAAAAAQ3JlYXRlZCB3aXRoIEdJTVBkLmUHAAAANklEQVRo3u3PQREAAAjDMKYc/h1GwQ9JoNett4ABAwYMGDBgwIABAwYMGDBgwIABAwYMGNgfGyb9BgHd0DWzAAAAAElFTkSuQmCC'''

# 128x128 blue square PNG (base64 encoded)
icon_128 = b'''iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAYAAADDPmHLAAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAB3RJTUUH6AELEwUAvjGI+wAAAB1pVFh0Q29tbWVudAAAAAAAQ3JlYXRlZCB3aXRoIEdJTVBkLmUHAAAAPElEQVR42u3BAQ0AAADCoPdPbQ8HFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADwb1+AAAfSdGr0AAAAASUVORK5CYII='''

def create_icons(folder):
    """Create icon files in the specified folder"""
    if not os.path.exists(folder):
        print(f"Folder {folder} does not exist")
        return
    
    with open(os.path.join(folder, 'icon16.png'), 'wb') as f:
        f.write(base64.b64decode(icon_16))
    
    with open(os.path.join(folder, 'icon48.png'), 'wb') as f:
        f.write(base64.b64decode(icon_48))
    
    with open(os.path.join(folder, 'icon128.png'), 'wb') as f:
        f.write(base64.b64decode(icon_128))
    
    print(f"Created icons in {folder}")

# Create icons for all browsers
folders = [
    'BrowserExtensions/Chrome',
    'BrowserExtensions/Edge',
    'BrowserExtensions/Firefox'
]

for folder in folders:
    create_icons(folder)

print("\nAll icons created successfully!")
print("Note: These are temporary blue squares. Replace with proper QuadroAI branded icons.")