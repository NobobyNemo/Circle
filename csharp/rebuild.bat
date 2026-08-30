@echo off
setlocal

set PROJECT_ROOT=c:\Users\win\Desktop\circle\csharp
set DESKTOP_PROJ=%PROJECT_ROOT%\Circle.Desktop\Circle.Desktop.csproj
set PACKAGING=%PROJECT_ROOT%\Circle.Packaging
set STAGING=%PACKAGING%\Staging
set APPPACKAGES=%PACKAGING%\AppPackages
set MAKEAPPX="C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
set SIGNTOOL="C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
set PFX=%PROJECT_ROOT%\Circle.Desktop\Circle.Desktop_TestCertificate.pfx
set PWD=circle123

echo === Stopping app ===
taskkill /f /im Circle.Desktop.exe 2>nul

echo === Cleaning ===
dotnet clean "%DESKTOP_PROJ%" -c Release 2>nul
if exist "%STAGING%" rmdir /s /q "%STAGING%"

echo === Publishing ===
dotnet publish "%DESKTOP_PROJ%" -c Release -r win-x64 --self-contained false -o "%STAGING%"
if errorlevel 1 (
    echo PUBLISH FAILED
    pause
    exit /b 1
)

echo === Staging assets and manifest ===
if not exist "%STAGING%\Assets" mkdir "%STAGING%\Assets"
copy /y "%PACKAGING%\Assets\*" "%STAGING%\Assets\" >nul
copy /y "%PACKAGING%\Package.appxmanifest" "%STAGING%\AppxManifest.xml" >nul

echo === Building MSIX ===
if not exist "%APPPACKAGES%" mkdir "%APPPACKAGES%"
%MAKEAPPX% pack /d "%STAGING%" /p "%APPPACKAGES%\Circle.Desktop.msix" /o
if errorlevel 1 (
    echo MAKEAPPX FAILED
    pause
    exit /b 1
)

echo === Signing MSIX ===
%SIGNTOOL% sign /fd SHA256 /f "%PFX%" /p %PWD% "%APPPACKAGES%\Circle.Desktop.msix"
if errorlevel 1 (
    echo SIGNTOOL FAILED
    pause
    exit /b 1
)

echo === Removing old package ===
powershell -Command "Get-AppxPackage -Name 'Circle.Desktop' -ErrorAction SilentlyContinue | Remove-AppxPackage"

echo === Installing new package ===
powershell -Command "Add-AppxPackage '%APPPACKAGES%\Circle.Desktop.msix'"

echo === Launching app ===
powershell -Command "$pkg = Get-AppxPackage -Name 'Circle.Desktop'; if ($pkg) { $manifest = Get-AppxPackageManifest $pkg; $appId = $manifest.Package.Applications.Application.Id; Start-Process \"shell:AppsFolder\$($pkg.PackageFamilyName)!$appId\" } else { Write-Output 'NOT INSTALLED' }"

echo === DONE ===
