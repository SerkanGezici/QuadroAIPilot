#!/usr/bin/env python3
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont
import os

# Define sizes
sizes = [16, 48, 128]

for size in sizes:
    # Create new image with blue background
    img = Image.new('RGBA', (size, size), color='#2196F3')
    draw = ImageDraw.Draw(img)
    
    # Calculate text sizes
    q_size = int(size * 0.5625)  # 72/128 ratio
    ai_size = int(size * 0.15625)  # 20/128 ratio
    
    # Try to find a suitable font
    try:
        # Try different font paths for Windows/WSL
        font_paths = [
            "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
            "/mnt/c/Windows/Fonts/arial.ttf",
            "/mnt/c/Windows/Fonts/Arial.ttf",
            "C:\\Windows\\Fonts\\arial.ttf"
        ]
        
        q_font = None
        ai_font = None
        
        for font_path in font_paths:
            if os.path.exists(font_path):
                try:
                    q_font = ImageFont.truetype(font_path, q_size)
                    ai_font = ImageFont.truetype(font_path, ai_size)
                    break
                except:
                    continue
        
        if not q_font:
            # Fallback to default font
            q_font = ImageFont.load_default()
            ai_font = ImageFont.load_default()
    except:
        q_font = ImageFont.load_default()
        ai_font = ImageFont.load_default()
    
    # Draw "Q" text
    q_text = "Q"
    # Get text bounding box
    bbox = draw.textbbox((0, 0), q_text, font=q_font)
    q_width = bbox[2] - bbox[0]
    q_height = bbox[3] - bbox[1]
    q_x = (size - q_width) // 2
    q_y = int(size * 0.586) - q_height  # 75/128 ratio adjusted for text height
    
    draw.text((q_x, q_y), q_text, fill='white', font=q_font)
    
    # Draw "AI" text
    ai_text = "AI"
    bbox = draw.textbbox((0, 0), ai_text, font=ai_font)
    ai_width = bbox[2] - bbox[0]
    ai_x = int(size * 0.75) - ai_width // 2  # 96/128 ratio
    ai_y = int(size * 0.75) - ai_size // 2  # 96/128 ratio
    
    draw.text((ai_x, ai_y), ai_text, fill='white', font=ai_font)
    
    # Save PNG
    img.save(f'icon{size}.png', 'PNG')
    print(f'Created icon{size}.png')

print("All icons created successfully!")