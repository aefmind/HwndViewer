@echo off
echo Generando EXE portable de un solo archivo...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo Error en publish
  pause
  exit /b
)
echo.
echo Listo! El exe esta en:
echo bin\Release\net8.0-windows\win-x64\publish\HwndViewer.exe
explorer bin\Release\net8.0-windows\win-x64\publish\
pause
