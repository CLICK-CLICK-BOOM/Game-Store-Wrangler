$Root = $PSScriptRoot
$PayloadName = "windowsdesktop-runtime-9.0.16-win-x64.exe"
$DotNetSource = "$Root\$PayloadName"

# 1. The Guardian Check (Look in the ROOT folder)
If (!(Test-Path $DotNetSource)) {
    Write-Host "CRITICAL HALT: The .NET 9 Desktop Runtime installer is missing." -ForegroundColor Red
    Write-Host "Please place the downloaded runtime in the root directory at:" -ForegroundColor Yellow
    Write-Host "Path: $DotNetSource" -ForegroundColor White
    Exit
}

# 2. Purge previous build artifacts safely
Write-Host "Purging old build artifacts..." -ForegroundColor DarkGray
If (Test-Path "$Root\dist\publish") { Remove-Item -Recurse -Force "$Root\dist\publish\*" }
If (Test-Path "$Root\dist\installer") { Remove-Item -Recurse -Force "$Root\dist\installer\*" }
New-Item -ItemType Directory -Force -Path "$Root\dist\publish" | Out-Null

# 3. Stage the Payload
Write-Host "Staging .NET Runtime payload..." -ForegroundColor Cyan
Copy-Item -Path $DotNetSource -Destination "$Root\dist\publish\$PayloadName" -Force

# 4. Build GSW (Main App) - Lean, Single-File
Write-Host "Compiling GSW App..." -ForegroundColor Green
dotnet publish "$Root\GSW.csproj" -c Release -o "$Root\dist\publish"

If ($LASTEXITCODE -ne 0) {
    Write-Host "GSW App Compilation Failed. Halting pipeline." -ForegroundColor Red
    Exit
}

# 5. Build GSW_Installer (Heavy Lifter)
Write-Host "Compiling GSW Installer..." -ForegroundColor Green
dotnet publish "$Root\GSW_Installer\GSW_Installer.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$Root\dist\installer"

Write-Host "Build Complete. Installer located in dist\installer." -ForegroundColor Green