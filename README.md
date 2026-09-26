# HWND Viewer v2 - Windows 11
Visor de ventanas abiertas con HWND, Parent y Owner (GW_OWNER) + Flash y Bring to Front

## ¿Qué hace?
- Lista TODAS las ventanas top-level (EnumWindows)
- Muestra HWND en decimal y HEX (0x...)
- Detecta si es WS_CHILD o WS_POPUP leyendo GWL_STYLE
- Muestra:
  - GetParent() -> solo fiable para childs
  - GetWindow(GW_OWNER=4) -> RECOMENDADO para popups (Chrome extensions)
  - GetAncestor(GA_ROOTOWNER=3) -> owner raíz
  - Cadena completa de owners
- Filtro en vivo: escribe tu UUID, "Chrome", clase, PID, etc.
- Acciones:
  - ⚡ Flashear: hace parpadear la barra de tareas de esa ventana (FlashWindowEx)
  - 🎯 Traer al frente: restaura si está minimizada y la trae al frente (SetForegroundWindow)
  - 📋 Copiar HWND / HEX

## Requisitos Windows 11
- .NET 8 SDK instalado (dotnet --version debe dar 8.x)
  Descarga: https://dotnet.microsoft.com/download/dotnet/8.0
- No necesita Visual Studio

## Cómo desplegar fácil (3 opciones)

### Opción A - Doble click (más fácil)
1. Descomprime el ZIP donde quieras
2. Doble click en `build-and-run.bat`
   - Compila y lanza automáticamente
   - Si no tienes .NET SDK, te dirá qué instalar

### Opción B - Consola
1. Click derecho en la carpeta -> "Abrir en Terminal" (Windows Terminal)
2. Ejecuta:
   dotnet build -c Release
   dotnet run -c Release
3. El exe queda en bin\Release\net8.0-windows\HwndViewer.exe

### Opción C - EXE portable de un solo archivo
Si quieres un .exe que puedas llevar a otra PC sin instalar nada:
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
El exe queda en:
   bin\Release\net8.0-windows\win-x64\publish\HwndViewer.exe (~40MB, no necesita .NET)

## Cómo usar para tu caso (companion + Chrome popup)
1. En tu extensión de Chrome, asegúrate de que el popup tenga el UUID en el título:
   document.title = "MiPopup UUID-12345-...";
2. Abre el HwndViewer
3. En el filtro escribe "UUID-12345"
4. Selecciona la ventana. Abajo verás la cadena de owners.
5. Prueba "Flashear" para confirmar visualmente.
6. El HWND que dice "Owner (GW_OWNER)" es la ventana principal de Chrome desde donde se lanzó.
   Ese es el que tu companion debe usar:

   [DllImport("user32.dll")]
   static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
   const uint GW_OWNER = 4;
   IntPtr owner = GetWindow(popupHwnd, GW_OWNER);

## Atajos
- F5: refrescar lista
- Ctrl+F: foco al filtro
- Doble click en fila: flashear ventana

## Notas de permisos
- Si una ventana es de un proceso elevado (Admin) y tu visor no lo es, no podrás traerla al frente ni flashearla.
  Solución: ejecuta HwndViewer como Administrador (click derecho -> Ejecutar como administrador)

Creado por Meta AI para Windows 11
