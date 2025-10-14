/**
 * QuadroAI Icon Generator
 * Creates PNG icons in multiple sizes
 * Run this in a browser console or Node.js with canvas support
 */

const sizes = [16, 48, 128];

function drawIcon(size) {
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const ctx = canvas.getContext('2d');
    
    // Background
    ctx.fillStyle = '#2196F3';
    ctx.fillRect(0, 0, size, size);
    
    // Q letter
    ctx.fillStyle = 'white';
    ctx.font = `bold ${size * 0.6}px Arial`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('Q', size / 2, size / 2);
    
    // AI text for larger icons
    if (size >= 48) {
        ctx.font = `${size * 0.15}px Arial`;
        ctx.fillText('AI', size * 0.75, size * 0.75);
    }
    
    return canvas;
}

// For browser environment
if (typeof window !== 'undefined') {
    sizes.forEach(size => {
        const canvas = drawIcon(size);
        canvas.toBlob(blob => {
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `icon${size}.png`;
            a.click();
            URL.revokeObjectURL(url);
        });
    });
}

// Export for Node.js if needed
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { drawIcon, sizes };
}