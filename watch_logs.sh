#!/bin/bash

LOG_DIR="/mnt/c/Users/serkan/source/repos/QuadroAIPilot/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64"

echo "Watching QuadroAIPilot logs..."
echo "Press Ctrl+C to stop"
echo "================================"

# Watch all log files in the directory
tail -f "$LOG_DIR"/*.log 2>/dev/null | while read line; do
    # Highlight important keywords
    if [[ $line == *"ERROR"* ]]; then
        echo -e "\033[31m$line\033[0m"  # Red for errors
    elif [[ $line == *"Intent detected"* ]]; then
        echo -e "\033[32m$line\033[0m"  # Green for intent detection
    elif [[ $line == *"TTS"* ]] || [[ $line == *"Speaking"* ]]; then
        echo -e "\033[34m$line\033[0m"  # Blue for TTS
    elif [[ $line == *"Unknown"* ]]; then
        echo -e "\033[33m$line\033[0m"  # Yellow for unknown
    else
        echo "$line"
    fi
done