@echo off
echo === HWND Viewer - Build & Run ===
echo Verificando .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
  echo.
  echo [ERROR] No tienes .NET 8 SDK instalado.
  echo Descargalo de: https://dotnet.microsoft.com/download/dotnet/8.0
  echo.
  pause
  exit /b
)
echo .NET encontrado:
dotnet --version
echo.
echo Compilando...
dotnet build -c Release
if errorlevel 1 (
  echo [ERROR] Fallo la compilacion
  pause
  exit /b
)
echo.
echo Ejecutando...
dotnet run -c Release --no-build
pause
