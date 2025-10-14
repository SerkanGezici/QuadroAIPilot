# QuadroAIPilot - GitHub Auto-Update Setup Script
# This script automates all GitHub setup tasks

param(
    [string]$GitHubUsername = "quadroaipilot",
    [string]$GitHubToken = ""
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "QuadroAIPilot GitHub Auto-Update Setup" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$ProjectRoot = $PSScriptRoot
$RepoName = "QuadroAIPilot"

# Check Git configuration
Write-Host "[1/10] Checking Git configuration..." -ForegroundColor Yellow
$gitUser = git config --global user.name
$gitEmail = git config --global user.email

if ([string]::IsNullOrEmpty($gitUser)) {
    Write-Host "Setting Git username..." -ForegroundColor Cyan
    git config --global user.name "QuadroAI Pilot"
}

if ([string]::IsNullOrEmpty($gitEmail)) {
    Write-Host "Setting Git email..." -ForegroundColor Cyan
    git config --global user.email "pilot@quadroai.com"
}

Write-Host "OK: Git configured" -ForegroundColor Green
Write-Host ""

# Initialize Git repository (if not exists)
Write-Host "[2/10] Checking Git repository..." -ForegroundColor Yellow
if (-not (Test-Path ".git")) {
    Write-Host "Initializing Git repository..." -ForegroundColor Cyan
    git init
    Write-Host "OK: Git repository initialized" -ForegroundColor Green
} else {
    Write-Host "OK: Git repository exists" -ForegroundColor Green
}
Write-Host ""

# Create .gitignore (if not exists)
Write-Host "[3/10] Checking .gitignore..." -ForegroundColor Yellow
if (-not (Test-Path ".gitignore")) {
    Write-Host "Creating .gitignore..." -ForegroundColor Cyan
    @"
bin/
obj/
Output/
*.user
*.suo
.vs/
packages/
Logs/
*.log
.DS_Store
Thumbs.db
*.tmp
"@ | Out-File -FilePath ".gitignore" -Encoding UTF8
    Write-Host "OK: .gitignore created" -ForegroundColor Green
} else {
    Write-Host "OK: .gitignore exists" -ForegroundColor Green
}
Write-Host ""

# Create README.md
Write-Host "[4/10] Checking README.md..." -ForegroundColor Yellow
if (-not (Test-Path "README.md") -or (Get-Content "README.md" -Raw).Length -lt 100) {
    Write-Host "Creating README.md..." -ForegroundColor Cyan
    @"
# QuadroAI Pilot

AI-powered voice assistant for Windows 11.

## Features

- Voice recognition with advanced dictation
- AI integration (Claude API)
- News aggregation from multiple sources
- Email management (Outlook integration)
- Browser integration (Chrome, Edge, Firefox)
- Auto-update system via GitHub Releases
- Modern UI with multiple themes

## Requirements

- Windows 11 (Build 22000+)
- .NET 8.0 Runtime
- Microsoft Edge WebView2

## Installation

Download the latest installer from [Releases](https://github.com/$GitHubUsername/$RepoName/releases).

## Auto-Update

This application automatically checks for updates on startup (once per day).
You can manually check for updates from Settings > Updates.

## Version

Current version: 1.2.0

## License

See LICENSE.txt
"@ | Out-File -FilePath "README.md" -Encoding UTF8
    Write-Host "OK: README.md created" -ForegroundColor Green
} else {
    Write-Host "OK: README.md exists" -ForegroundColor Green
}
Write-Host ""

# Add all files to staging
Write-Host "[5/10] Adding files to staging area..." -ForegroundColor Yellow
git add .
Write-Host "OK: Files added" -ForegroundColor Green
Write-Host ""

# Initial commit
Write-Host "[6/10] Creating commit..." -ForegroundColor Yellow
$commitCount = git rev-list --all --count 2>$null
if ([string]::IsNullOrEmpty($commitCount) -or $commitCount -eq "0") {
    Write-Host "Making initial commit..." -ForegroundColor Cyan
    git commit -m "Initial commit: QuadroAIPilot v1.2.0 with auto-update system"
    Write-Host "OK: Initial commit done" -ForegroundColor Green
} else {
    Write-Host "Committing changes..." -ForegroundColor Cyan
    git commit -m "Add auto-update system v1.2.0" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: Commit done" -ForegroundColor Green
    } else {
        Write-Host "OK: No changes to commit" -ForegroundColor Yellow
    }
}
Write-Host ""

# Check GitHub remote
Write-Host "[7/10] Checking GitHub remote..." -ForegroundColor Yellow
$remoteUrl = git remote get-url origin 2>$null
if ([string]::IsNullOrEmpty($remoteUrl)) {
    Write-Host "Adding GitHub remote..." -ForegroundColor Cyan
    $githubUrl = "https://github.com/$GitHubUsername/$RepoName.git"
    git remote add origin $githubUrl
    Write-Host "OK: Remote added: $githubUrl" -ForegroundColor Green
} else {
    Write-Host "OK: Remote exists: $remoteUrl" -ForegroundColor Green
}
Write-Host ""

# Set branch name to main
Write-Host "[8/10] Checking branch..." -ForegroundColor Yellow
$currentBranch = git branch --show-current
if ($currentBranch -ne "main") {
    Write-Host "Setting branch to 'main'..." -ForegroundColor Cyan
    git branch -M main
    Write-Host "OK: Branch set to 'main'" -ForegroundColor Green
} else {
    Write-Host "OK: Branch is already 'main'" -ForegroundColor Green
}
Write-Host ""

# Check for setup file
Write-Host "[9/10] Checking setup file..." -ForegroundColor Yellow
$setupFiles = Get-ChildItem -Path "Output" -Filter "QuadroAIPilot_Setup*.exe" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
if ($setupFiles) {
    $latestSetup = $setupFiles[0]
    $setupPath = $latestSetup.FullName
    $setupName = $latestSetup.Name
    $setupSize = [math]::Round($latestSetup.Length / 1MB, 2)
    Write-Host "OK: Setup file found: $setupName ($setupSize MB)" -ForegroundColor Green
} else {
    Write-Host "WARNING: Setup file not found!" -ForegroundColor Red
    Write-Host "Please run BuildAndSetup.ps1 first." -ForegroundColor Yellow
    $setupPath = $null
}
Write-Host ""

# Summary
Write-Host "[10/10] Setup summary" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Git configured" -ForegroundColor Green
Write-Host "Repository initialized" -ForegroundColor Green
Write-Host "Files committed" -ForegroundColor Green
Write-Host "GitHub remote added" -ForegroundColor Green
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Create GitHub Repository:" -ForegroundColor Cyan
Write-Host "   - Go to: https://github.com/new" -ForegroundColor White
Write-Host "   - Repository name: $RepoName" -ForegroundColor White
Write-Host "   - Select 'Public' (free)" -ForegroundColor White
Write-Host "   - Click 'Create repository'" -ForegroundColor White
Write-Host ""
Write-Host "2. Push code to GitHub:" -ForegroundColor Cyan
Write-Host "   Run this command:" -ForegroundColor White
Write-Host "   git push -u origin main" -ForegroundColor Yellow
Write-Host ""
Write-Host "3. Create first release:" -ForegroundColor Cyan
Write-Host "   - Go to: https://github.com/$GitHubUsername/$RepoName/releases/new" -ForegroundColor White
Write-Host "   - Tag: v1.2.0" -ForegroundColor White
Write-Host "   - Title: QuadroAIPilot v1.2.0" -ForegroundColor White
Write-Host "   - Description: First release with auto-update system" -ForegroundColor White
if ($setupPath) {
    Write-Host "   - Attach setup file: $setupName" -ForegroundColor White
}
Write-Host "   - Click 'Publish release'" -ForegroundColor White
Write-Host ""
Write-Host "4. Test:" -ForegroundColor Cyan
Write-Host "   - Run application" -ForegroundColor White
Write-Host "   - Settings > Updates > Check for Updates" -ForegroundColor White
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Setup ready! You can now create GitHub repo and push." -ForegroundColor Green
Write-Host ""
