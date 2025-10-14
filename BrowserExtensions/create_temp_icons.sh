#!/bin/bash
# Create temporary PNG icons using ImageMagick or simple base64 encoded images

# Base64 encoded 1x1 blue pixel PNG
BLUE_PIXEL="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="

# Create icons in Chrome folder
echo "Creating Chrome icons..."
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Chrome/icon16.png
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Chrome/icon48.png
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Chrome/icon128.png

# Create icons in Edge folder
echo "Creating Edge icons..."
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Edge/icon16.png
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Edge/icon48.png
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Edge/icon128.png

# Create icons in Firefox folder
echo "Creating Firefox icons..."
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Firefox/icon16.png
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Firefox/icon48.png
echo $BLUE_PIXEL | base64 -d > BrowserExtensions/Firefox/icon128.png

echo "Temporary icons created. Replace these with proper icons later."