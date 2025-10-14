#!/usr/bin/env python3
"""
QuadroAI Browser Extension Icon Generator
Creates PNG icons in required sizes from SVG
"""

import os
from PIL import Image, ImageDraw, ImageFont

def create_icon(size):
    """Create a PNG icon with the specified size"""
    # Create a new image with RGBA mode for transparency
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Background color (Material Blue)
    bg_color = (33, 150, 243, 255)  # #2196F3
    draw.rectangle([0, 0, size, size], fill=bg_color)
    
    # Text color
    text_color = (255, 255, 255, 255)  # White
    
    # Try to use a system font, fallback to default if not available
    try:
        # Adjust font size based on icon size
        font_size = int(size * 0.6)
        font = ImageFont.truetype("arial.ttf", font_size)
    except:
        # Use default font if truetype not available
        font = ImageFont.load_default()
    
    # Draw 'Q' centered
    text = 'Q'
    # Get text bounding box
    bbox = draw.textbbox((0, 0), text, font=font)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]
    
    # Calculate position to center the text
    x = (size - text_width) // 2
    y = (size - text_height) // 2 - bbox[1]  # Adjust for baseline
    
    draw.text((x, y), text, fill=text_color, font=font)
    
    # Add 'AI' text for larger icons
    if size >= 48:
        try:
            ai_font_size = int(size * 0.15)
            ai_font = ImageFont.truetype("arial.ttf", ai_font_size)
        except:
            ai_font = ImageFont.load_default()
        
        ai_text = 'AI'
        ai_bbox = draw.textbbox((0, 0), ai_text, font=ai_font)
        ai_width = ai_bbox[2] - ai_bbox[0]
        ai_height = ai_bbox[3] - ai_bbox[1]
        
        # Position AI text in bottom right
        ai_x = int(size * 0.75) - ai_width // 2
        ai_y = int(size * 0.75) - ai_height // 2 - ai_bbox[1]
        
        draw.text((ai_x, ai_y), ai_text, fill=text_color, font=ai_font)
    
    return img

def main():
    """Generate all required icon sizes"""
    sizes = [16, 48, 128]
    
    # Create icons for Chrome/Edge
    chrome_dir = os.path.join(os.path.dirname(__file__), 'Chrome')
    for size in sizes:
        img = create_icon(size)
        filename = f'icon{size}.png'
        filepath = os.path.join(chrome_dir, filename)
        img.save(filepath, 'PNG')
        print(f"Created {filepath}")
    
    # Create icons for Firefox
    firefox_dir = os.path.join(os.path.dirname(__file__), 'Firefox')
    for size in sizes:
        img = create_icon(size)
        filename = f'icon{size}.png'
        filepath = os.path.join(firefox_dir, filename)
        img.save(filepath, 'PNG')
        print(f"Created {filepath}")
    
    print("\nAll icons created successfully!")

if __name__ == "__main__":
    main()