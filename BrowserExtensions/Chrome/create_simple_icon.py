#!/usr/bin/env python3
import struct
import zlib

def create_png(size, filename):
    # Create a simple blue square with white text-like pattern
    width = height = size
    
    # PNG header
    header = b'\x89PNG\r\n\x1a\n'
    
    # IHDR chunk
    ihdr_data = struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0)
    ihdr_chunk = struct.pack('>I', 13) + b'IHDR' + ihdr_data + struct.pack('>I', zlib.crc32(b'IHDR' + ihdr_data))
    
    # Create image data (RGB)
    image_data = []
    for y in range(height):
        row = [0]  # filter type
        for x in range(width):
            # Blue background (#2196F3)
            r, g, b = 33, 150, 243
            
            # Add white pixels for "Q" shape (simplified)
            cx, cy = width // 2, height // 2
            # Create a simple Q pattern
            if size >= 48:  # Only for larger icons
                # Vertical line of Q
                if abs(x - cx + width//6) < width//10 and abs(y - cy) < height//3:
                    r, g, b = 255, 255, 255
                # Circle part of Q
                dist = ((x - cx)**2 + (y - cy)**2)**0.5
                if abs(dist - width//3) < width//10:
                    r, g, b = 255, 255, 255
                # Tail of Q
                if x > cx and y > cy and abs(x - cx - y + cy) < width//15:
                    r, g, b = 255, 255, 255
            else:
                # For small icon, just a white square in center
                if abs(x - cx) < width//4 and abs(y - cy) < height//4:
                    r, g, b = 255, 255, 255
            
            row.extend([r, g, b])
        image_data.extend(row)
    
    # Compress image data
    compressed = zlib.compress(bytes(image_data))
    
    # IDAT chunk
    idat_chunk = struct.pack('>I', len(compressed)) + b'IDAT' + compressed + struct.pack('>I', zlib.crc32(b'IDAT' + compressed))
    
    # IEND chunk
    iend_chunk = struct.pack('>I', 0) + b'IEND' + struct.pack('>I', zlib.crc32(b'IEND'))
    
    # Write PNG file
    with open(filename, 'wb') as f:
        f.write(header + ihdr_chunk + idat_chunk + iend_chunk)
    
    print(f'Created {filename}')

# Create icons
for size in [16, 48, 128]:
    create_png(size, f'icon{size}.png')

print("All icons created successfully!")