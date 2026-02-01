# Plano: WigiDash Stream Deck Mirror Widget

## Contexto e Motivação

### Objetivo
Criar um widget que **captura e espelha a janela do Elgato Stream Deck (Virtual Device)** no WigiDash, permitindo visualização e controle direto pelo dispositivo físico.

### Benefícios Esperados
| Aspecto | Estimativa |
|---------|------------|
| CPU (idle) | ~0.1-0.5% |
| CPU (captura) | +0.5-1% |
| RAM | ~10-20MB |
| Intervalo mínimo | 50-100ms (10-20 FPS possível) |

---

## Descobertas Técnicas (Testadas e Validadas)

### 1. Identificação da Janela do Stream Deck

A janela do Stream Deck Virtual Device foi identificada com sucesso:

```
HWND: 133398
Classe: Qt693QWindowToolSaveBits
Título: "Stream Deck"
Processo: StreamDeck
Dimensões: 536 x 662 pixels
```

**Observações:**
- A janela **não aparece no Alt+Tab** (usa `WS_EX_TOOLWINDOW`)
- Isso é apenas uma flag cosmética, a janela é capturável normalmente
- É uma janela **Qt 6.9.3** padrão

### 2. Método de Localização da Janela

```csharp
// Usar EnumWindows + filtrar por processo "StreamDeck" + título "Stream Deck"

[DllImport("user32.dll")]
public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

[DllImport("user32.dll")]
public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

[DllImport("user32.dll")]
public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

// Filtrar por:
// - proc.ProcessName.Contains("streamdeck", StringComparison.OrdinalIgnoreCase)
// - title == "Stream Deck" (ou título configurável para múltiplos devices)
```

### 3. Captura de Janela

**Método escolhido: `PrintWindow` com `PW_RENDERFULLCONTENT`**

```csharp
[DllImport("user32.dll")]
public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

public const int PW_RENDERFULLCONTENT = 2;

// Uso:
Bitmap bmp = new Bitmap(width, height);
using (Graphics g = Graphics.FromImage(bmp)) {
    IntPtr hdc = g.GetHdc();
    PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
    g.ReleaseHdc(hdc);
}
```

**Por que PrintWindow:**
- Funciona com janelas cobertas por outras
- Funciona com janelas fora da área visível
- Funciona com janelas transparentes (testado!)
- Compatível com .NET Framework 4.7.2
- Não requer Win10 1803+ (diferente de Windows Graphics Capture)

### 4. Ocultação da Janela Original (TESTADO E FUNCIONANDO!)

É possível **esconder a janela do Stream Deck no monitor** e exibir apenas no WigiDash:

```csharp
[DllImport("user32.dll")]
public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

[DllImport("user32.dll")]
public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

[DllImport("user32.dll")]
public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

public const int GWL_EXSTYLE = -20;
public const int WS_EX_LAYERED = 0x80000;
public const uint LWA_ALPHA = 0x2;

// Para OCULTAR (alpha = 0):
int style = GetWindowLong(hwnd, GWL_EXSTYLE);
SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);  // alpha = 0 = invisível

// Para RESTAURAR (alpha = 255):
SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);  // alpha = 255 = visível
```

**Resultado do teste:**
- Janela ficou 100% invisível no monitor
- `PrintWindow` capturou o conteúdo perfeitamente (screenshot salvo com todos os botões)
- Dimensões capturadas: 536 x 662 pixels
- Janela restaurada sem problemas

**Impacto no desempenho:** NENHUM (ou ligeiramente positivo, pois o DWM não precisa compositar)

### 5. Hide Nativo do Stream Deck (Win+F12) - NÃO FUNCIONA!

**Teste realizado:**
- Pressionado Win+F12 para ocultar o Stream Deck nativamente
- Executado `PrintWindow` na janela oculta
- **Resultado: TELA PRETA** - O Stream Deck para de renderizar quando oculto nativamente

**Conclusão:** Devemos usar nosso método de transparência (alpha=0), não o hide nativo.

---

## Layout do Widget com Rodapé de Controle

### Conceito

O widget possui uma **barra de rodapé opcional** que "sequestra" 5% da altura do widget para controles, permitindo toggle de visibilidade da janela original sem sair da tela principal.

### Modos de Operação

| Configuração | Rodapé ATIVADO | Rodapé DESATIVADO |
|--------------|----------------|-------------------|
| Layout | 95% conteúdo + 5% rodapé | 100% conteúdo |
| Toggle hide/show | Long press (~500-700ms) no rodapé | Apenas nas Settings (XAML) |
| Letterbox | Dentro dos 95% | Normal (100%) |

### Layout Visual - Rodapé ATIVADO

```
┌─────────────────────────────────────────┐
│░░░│                           │░░░░░░░░░│  ← letterbox lateral (se necessário)
│░░░│                           │░░░░░░░░░│
│░░░│   STREAM DECK CONTENT     │░░░░░░░░░│  ← 95% da altura do widget
│░░░│   (preserva aspect ratio) │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
├───┴───────────────────────────┴─────────┤
│  [👁🗔] RODAPÉ DE CONTROLE (5% altura)  │  ← área de long press para toggle
└─────────────────────────────────────────┘
```

### Layout Visual - Rodapé DESATIVADO

```
┌─────────────────────────────────────────┐
│░░░│                           │░░░░░░░░░│  ← letterbox lateral (se necessário)
│░░░│                           │░░░░░░░░░│
│░░░│   STREAM DECK CONTENT     │░░░░░░░░░│  ← 100% da altura do widget
│░░░│   (preserva aspect ratio) │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
└───┴───────────────────────────┴─────────┘
```

### Comportamento do Rodapé

1. **Ícone indicador:** Combinação de janela + olho (a definir no desenvolvimento)
   - Olho aberto = janela visível no monitor
   - Olho fechado = janela oculta (transparente)

2. **Gesto de ativação:** Long Press (~500-700ms)
   - Evita acionamento acidental ao errar clique no Stream Deck
   - Feedback visual durante o press (opcional: progress indicator)

3. **Ação:** Toggle do estado `hideOriginalWindow`
   - Se visível → oculta (alpha=0)
   - Se oculta → restaura (alpha=255)

---

## Aspect Ratio e Mapeamento de Coordenadas

### Problema
O Stream Deck Virtual Device pode ter proporções diferentes do widget WigiDash:
- Stream Deck: 536 x 662 pixels (proporção ~0.81 - mais alto que largo)
- WigiDash: Pode ser 480x480, 800x480, etc.

### Solução: Letterbox com Preservação de Aspect Ratio

O conteúdo é renderizado **dentro da área disponível** (95% ou 100% dependendo do rodapé), preservando aspect ratio e adicionando letterbox quando necessário.

### Estrutura de Dados para Aspect Ratio

```csharp
public class AspectRatioInfo
{
    // Dimensões originais da janela capturada
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }

    // Dimensões da área de conteúdo (95% ou 100% do widget)
    public int ContentAreaWidth { get; set; }
    public int ContentAreaHeight { get; set; }

    // Área de renderização dentro do widget (após letterbox)
    public int RenderX { get; set; }      // Offset X do conteúdo
    public int RenderY { get; set; }      // Offset Y do conteúdo
    public int RenderWidth { get; set; }  // Largura do conteúdo renderizado
    public int RenderHeight { get; set; } // Altura do conteúdo renderizado

    // Fator de escala
    public double Scale { get; set; }
}
```

### Cálculo do Letterbox (Atualizado para Rodapé)

```csharp
public AspectRatioInfo CalculateAspectRatio(
    int sourceW, int sourceH,
    int widgetW, int widgetH,
    bool showFooterBar)
{
    // Calcular área de conteúdo disponível
    int contentH = showFooterBar
        ? (int)(widgetH * 0.95)  // 95% se rodapé ativado
        : widgetH;               // 100% se desativado
    int contentW = widgetW;

    var info = new AspectRatioInfo
    {
        SourceWidth = sourceW,
        SourceHeight = sourceH,
        ContentAreaWidth = contentW,
        ContentAreaHeight = contentH
    };

    if (sourceW <= 0 || sourceH <= 0 || contentW <= 0 || contentH <= 0)
    {
        info.RenderWidth = contentW;
        info.RenderHeight = contentH;
        info.Scale = 1.0;
        return info;
    }

    double sourceAspect = (double)sourceW / sourceH;
    double contentAspect = (double)contentW / contentH;

    if (sourceAspect > contentAspect)
    {
        // Fonte mais larga - letterbox vertical (barras em cima/baixo)
        info.RenderWidth = contentW;
        info.RenderHeight = (int)(contentW / sourceAspect);
        info.RenderX = 0;
        info.RenderY = (contentH - info.RenderHeight) / 2;
    }
    else
    {
        // Fonte mais alta - letterbox horizontal (barras laterais)
        info.RenderHeight = contentH;
        info.RenderWidth = (int)(contentH * sourceAspect);
        info.RenderX = (contentW - info.RenderWidth) / 2;
        info.RenderY = 0;
    }

    info.Scale = (double)info.RenderWidth / sourceW;

    return info;
}
```

### Mapeamento de Coordenadas para Click

```csharp
public (int x, int y)? MapClickToSource(int clickX, int clickY, AspectRatioInfo info, bool showFooterBar, int widgetHeight)
{
    // 1. Verificar se click está no rodapé
    if (showFooterBar)
    {
        int footerY = (int)(widgetHeight * 0.95);
        if (clickY >= footerY)
        {
            // Click no rodapé - não mapear para Stream Deck
            return null;
        }
    }

    // 2. Verificar se o click está dentro da área de conteúdo
    if (clickX < info.RenderX || clickX >= info.RenderX + info.RenderWidth ||
        clickY < info.RenderY || clickY >= info.RenderY + info.RenderHeight)
    {
        // Click na área de letterbox - ignorar
        return null;
    }

    // 3. Converter coordenadas do widget para coordenadas da fonte
    int relativeX = clickX - info.RenderX;
    int relativeY = clickY - info.RenderY;

    int sourceX = (int)(relativeX / info.Scale);
    int sourceY = (int)(relativeY / info.Scale);

    // 4. Garantir que está dentro dos limites
    sourceX = Math.Max(0, Math.Min(sourceX, info.SourceWidth - 1));
    sourceY = Math.Max(0, Math.Min(sourceY, info.SourceHeight - 1));

    return (sourceX, sourceY);
}
```

### Envio do Click para a Janela

```csharp
public void SendClickToWindow(IntPtr hwnd, int x, int y)
{
    // Combinar coordenadas em lParam (LOWORD = X, HIWORD = Y)
    IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));

    // Enviar mouse down e mouse up
    PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
    PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
}

// Constantes
public const uint WM_LBUTTONDOWN = 0x0201;
public const uint WM_LBUTTONUP = 0x0202;
```

---

## Arquitetura do Widget

### Estrutura de Arquivos

```
WigiDash-StreamDeckMirror-WidGet/
├── StreamDeckWidgetObject.cs        # IWidgetObject - entry point, metadata
├── StreamDeckWidgetInstance.cs      # IWidgetInstance - lógica principal
├── NativeMethods.cs                 # Win32 P/Invoke declarations
├── AspectRatioHelper.cs             # Cálculos de aspect ratio e coordenadas
├── WindowCapture.cs                 # Lógica de captura de janela
├── FooterBarRenderer.cs             # Renderização do rodapé de controle
├── StreamDeckSettingsControl.xaml   # UI de configurações (WPF)
├── StreamDeckSettingsControl.xaml.cs
├── Properties/
│   └── AssemblyInfo.cs
├── Resources/
│   ├── icon.png                     # Ícone do widget (128x128 PNG)
│   ├── eye_open.png                 # Ícone olho aberto
│   └── eye_closed.png               # Ícone olho fechado
├── deploy.ps1                       # Script de deploy
└── StreamDeckWidget.csproj          # Projeto .NET Framework 4.7.2
```

### Dependências

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>

  <ItemGroup>
    <!-- Framework do WigiDash - SEM WebView2! -->
    <PackageReference Include="victorperez2911.WigiDashWidgetFramework" Version="1.1.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Referências do sistema -->
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="PresentationFramework" />
    <Reference Include="PresentationCore" />
    <Reference Include="WindowsBase" />
  </ItemGroup>
</Project>
```

---

## Configurações do Widget

### Settings Completas

| Setting | Tipo | Default | Descrição |
|---------|------|---------|-----------|
| `deviceName` | string | "Stream Deck" | Nome/título da janela (para múltiplos devices) |
| `refreshInterval` | int | 100 | Intervalo em ms (100ms = 10 FPS) |
| `hideOriginalWindow` | bool | false | Ocultar janela no monitor via transparência |
| `showFooterBar` | bool | true | Exibir barra de rodapé com controle de visibilidade |
| `backgroundColor` | string | "#000000" | Cor do letterbox e rodapé |
| `longPressDuration` | int | 600 | Duração do long press em ms (500-700 recomendado) |

### UI de Configurações

```
┌─────────────────────────────────────────────────────────────┐
│  Stream Deck Mirror - Configurações                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Device:        [▼ Stream Deck          ] [🔄 Refresh]      │
│                                                              │
│  Refresh Rate:  [====●=============] 100ms (10 FPS)         │
│                 50ms              1000ms                     │
│                                                              │
│  ─────────────────────────────────────────────────────────  │
│  VISIBILIDADE DA JANELA ORIGINAL                            │
│  ─────────────────────────────────────────────────────────  │
│                                                              │
│  [✓] Ocultar janela original no monitor                     │
│                                                              │
│  [✓] Exibir barra de rodapé (toggle rápido)                 │
│      └─ Long press duration: [===●====] 600ms               │
│                              400ms    800ms                  │
│                                                              │
│  Cor de fundo:  [■ #000000] [Escolher...]                   │
│                                                              │
│  ─────────────────────────────────────────────────────────  │
│  Status: ● Conectado | Janela: 536x662 | FPS: 10            │
│  Visibilidade: 👁 Visível no monitor                        │
└─────────────────────────────────────────────────────────────┘
```

---

## Implementação Detalhada

### NativeMethods.cs

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace StreamDeckWidget
{
    public static class NativeMethods
    {
        // ===== Window Enumeration =====

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // ===== Window Rectangle =====

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        // ===== Window Capture =====

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        public const int PW_RENDERFULLCONTENT = 2;

        // ===== Window Transparency =====

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(
            IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x80000;
        public const uint LWA_ALPHA = 0x2;

        // ===== Mouse Input =====

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;
        public const uint WM_RBUTTONDOWN = 0x0204;
        public const uint WM_RBUTTONUP = 0x0205;

        // ===== Window Validation =====

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);
    }
}
```

### AspectRatioHelper.cs

```csharp
using System;

namespace StreamDeckWidget
{
    public class AspectRatioInfo
    {
        public int SourceWidth { get; set; }
        public int SourceHeight { get; set; }
        public int ContentAreaWidth { get; set; }
        public int ContentAreaHeight { get; set; }
        public int RenderX { get; set; }
        public int RenderY { get; set; }
        public int RenderWidth { get; set; }
        public int RenderHeight { get; set; }
        public double Scale { get; set; }
    }

    public static class AspectRatioHelper
    {
        public const double FOOTER_HEIGHT_PERCENT = 0.05; // 5%

        public static AspectRatioInfo Calculate(
            int sourceW, int sourceH,
            int widgetW, int widgetH,
            bool showFooterBar)
        {
            // Calcular área de conteúdo disponível
            int contentH = showFooterBar
                ? (int)(widgetH * (1.0 - FOOTER_HEIGHT_PERCENT))
                : widgetH;
            int contentW = widgetW;

            var info = new AspectRatioInfo
            {
                SourceWidth = sourceW,
                SourceHeight = sourceH,
                ContentAreaWidth = contentW,
                ContentAreaHeight = contentH
            };

            if (sourceW <= 0 || sourceH <= 0 || contentW <= 0 || contentH <= 0)
            {
                info.RenderWidth = contentW;
                info.RenderHeight = contentH;
                info.Scale = 1.0;
                return info;
            }

            double sourceAspect = (double)sourceW / sourceH;
            double contentAspect = (double)contentW / contentH;

            if (sourceAspect > contentAspect)
            {
                // Fonte mais larga - letterbox vertical
                info.RenderWidth = contentW;
                info.RenderHeight = (int)(contentW / sourceAspect);
                info.RenderX = 0;
                info.RenderY = (contentH - info.RenderHeight) / 2;
            }
            else
            {
                // Fonte mais alta - letterbox horizontal
                info.RenderHeight = contentH;
                info.RenderWidth = (int)(contentH * sourceAspect);
                info.RenderX = (contentW - info.RenderWidth) / 2;
                info.RenderY = 0;
            }

            info.Scale = (double)info.RenderWidth / sourceW;

            return info;
        }

        public static (int x, int y)? MapClickToSource(
            int clickX, int clickY,
            AspectRatioInfo info,
            bool showFooterBar,
            int widgetHeight)
        {
            // Verificar se click está no rodapé
            if (showFooterBar)
            {
                int footerY = (int)(widgetHeight * (1.0 - FOOTER_HEIGHT_PERCENT));
                if (clickY >= footerY)
                {
                    return null; // Click no rodapé
                }
            }

            // Verificar se click está na área de conteúdo
            if (clickX < info.RenderX || clickX >= info.RenderX + info.RenderWidth ||
                clickY < info.RenderY || clickY >= info.RenderY + info.RenderHeight)
            {
                return null; // Click no letterbox
            }

            // Converter coordenadas
            int relativeX = clickX - info.RenderX;
            int relativeY = clickY - info.RenderY;

            int sourceX = (int)(relativeX / info.Scale);
            int sourceY = (int)(relativeY / info.Scale);

            // Clamp aos limites
            sourceX = Math.Max(0, Math.Min(sourceX, info.SourceWidth - 1));
            sourceY = Math.Max(0, Math.Min(sourceY, info.SourceHeight - 1));

            return (sourceX, sourceY);
        }

        public static bool IsClickInFooter(int clickY, int widgetHeight, bool showFooterBar)
        {
            if (!showFooterBar) return false;
            int footerY = (int)(widgetHeight * (1.0 - FOOTER_HEIGHT_PERCENT));
            return clickY >= footerY;
        }
    }
}
```

### FooterBarRenderer.cs

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;

namespace StreamDeckWidget
{
    public static class FooterBarRenderer
    {
        public static void Render(
            Graphics g,
            int widgetWidth,
            int widgetHeight,
            bool isWindowHidden,
            Color backgroundColor,
            float longPressProgress = 0f) // 0.0 a 1.0 durante long press
        {
            int footerY = (int)(widgetHeight * (1.0 - AspectRatioHelper.FOOTER_HEIGHT_PERCENT));
            int footerHeight = widgetHeight - footerY;

            // Fundo do rodapé
            using (var brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, 0, footerY, widgetWidth, footerHeight);
            }

            // Linha separadora sutil
            using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
            {
                g.DrawLine(pen, 0, footerY, widgetWidth, footerY);
            }

            // Ícone centralizado (placeholder - substituir por ícone real)
            int iconSize = (int)(footerHeight * 0.6);
            int iconX = (widgetWidth - iconSize) / 2;
            int iconY = footerY + (footerHeight - iconSize) / 2;

            // Cor do ícone baseada no estado
            Color iconColor = isWindowHidden
                ? Color.FromArgb(180, 100, 100, 100)   // Cinza = oculto
                : Color.FromArgb(180, 100, 200, 100);  // Verde = visível

            // Desenhar ícone simplificado (olho)
            using (var brush = new SolidBrush(iconColor))
            {
                // Olho (elipse)
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, iconX, iconY, iconSize, iconSize * 0.6f);

                // Pupila (se visível)
                if (!isWindowHidden)
                {
                    using (var pupilBrush = new SolidBrush(Color.FromArgb(200, 50, 50, 50)))
                    {
                        int pupilSize = iconSize / 3;
                        int pupilX = iconX + (iconSize - pupilSize) / 2;
                        int pupilY = iconY + (int)(iconSize * 0.3f - pupilSize / 2);
                        g.FillEllipse(pupilBrush, pupilX, pupilY, pupilSize, pupilSize);
                    }
                }
            }

            // Progress indicator durante long press
            if (longPressProgress > 0f)
            {
                int progressWidth = (int)(widgetWidth * longPressProgress);
                using (var progressBrush = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
                {
                    g.FillRectangle(progressBrush, 0, footerY, progressWidth, 3);
                }
            }
        }
    }
}
```

---

## Fluxo de Execução

### Diagrama do Loop de Captura

```
┌─────────────────────────────────────────────────────────────────┐
│                      CAPTURE LOOP                                │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌─────────────────────┐
                    │   isRunning == true │◄────────────────┐
                    └─────────────────────┘                 │
                              │                             │
                              ▼                             │
                    ┌─────────────────────┐                 │
                    │  IsWindow(hwnd)?    │                 │
                    └─────────────────────┘                 │
                      │ sim           │ não                 │
                      ▼               ▼                     │
┌─────────────────────────┐  ┌─────────────────────┐       │
│  GetWindowRect()        │  │  FindTargetWindow() │       │
│  PrintWindow()          │  │  (reconectar)       │       │
│  CalculateAspectRatio() │  └─────────────────────┘       │
│  DrawWithLetterbox()    │           │                    │
│  DrawFooterBar()        │           │                    │
│  RaiseWidgetUpdated()   │           │                    │
└─────────────────────────┘           │                    │
                      │               │                    │
                      ▼               ▼                    │
┌─────────────────────────────────────────────────────────────────┐
│  Thread.Sleep(refreshInterval)                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              └────────────────────────────────────┘
```

### Diagrama de Click (Atualizado)

```
┌─────────────────────────────────────────────────────────────────┐
│                     CLICK EVENT                                  │
│                   (x=400, y=450)                                │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  1. Verificar se click está no RODAPÉ (5% inferior)             │
│     - footerY = widgetHeight * 0.95                             │
│     - clickY >= footerY?                                        │
└─────────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
                    │                   │
               no rodapé           fora do rodapé
                    │                   │
                    ▼                   ▼
┌─────────────────────────┐  ┌─────────────────────────────────┐
│  INICIAR LONG PRESS     │  │  2. Verificar se está na área   │
│  TIMER (600ms)          │  │     de CONTEÚDO (não letterbox) │
│                         │  └─────────────────────────────────┘
│  Se completar:          │                   │
│  → Toggle hideWindow    │         ┌─────────┴─────────┐
│                         │         │                   │
│  Se soltar antes:       │    no conteúdo         no letterbox
│  → Cancelar             │         │                   │
└─────────────────────────┘         ▼                   ▼
                          ┌─────────────────┐  ┌───────────────┐
                          │ 3. Mapear coords│  │ IGNORAR CLICK │
                          │ 4. SendClick()  │  └───────────────┘
                          └─────────────────┘
```

---

## Seleção e Identificação de Virtual Devices

### Pré-requisito: App Stream Deck Aberto

**IMPORTANTE:** A seleção de Virtual Device só é possível com o aplicativo Elgato Stream Deck em execução.

- Sem o app rodando, não há janelas para enumerar
- O dropdown de seleção fica vazio/desabilitado
- Mensagem clara para o usuário: "Abra o Stream Deck para configurar"

### Filtro de Janelas

O widget **só lista janelas do processo StreamDeck**, nunca outras janelas do sistema.

```csharp
public static List<StreamDeckWindowInfo> FindAllStreamDeckWindows()
{
    var windows = new List<StreamDeckWindowInfo>();

    NativeMethods.EnumWindows((hwnd, lParam) =>
    {
        // ===== FILTRO 1: Verificar processo =====
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        try
        {
            var proc = Process.GetProcessById((int)processId);

            // APENAS processo StreamDeck - ignora todos os outros
            if (!proc.ProcessName.Equals("StreamDeck", StringComparison.OrdinalIgnoreCase))
                return true; // continuar enumeração
        }
        catch
        {
            return true; // processo inacessível, ignorar
        }

        // ===== FILTRO 2: Verificar se tem título =====
        var sbTitle = new StringBuilder(256);
        NativeMethods.GetWindowText(hwnd, sbTitle, 256);
        string title = sbTitle.ToString();

        if (string.IsNullOrWhiteSpace(title))
            return true; // janela sem título, ignorar

        // ===== FILTRO 3: Verificar classe da janela (Qt) =====
        var sbClass = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sbClass, 256);
        string className = sbClass.ToString();

        // Virtual Devices usam classe Qt específica
        if (!className.StartsWith("Qt", StringComparison.OrdinalIgnoreCase))
            return true; // não é janela Qt do Virtual Device

        // ===== FILTRO 4: Verificar dimensões válidas =====
        NativeMethods.GetWindowRect(hwnd, out var rect);
        if (rect.Width <= 0 || rect.Height <= 0)
            return true; // dimensões inválidas

        // ===== Passou em todos os filtros =====
        windows.Add(new StreamDeckWindowInfo
        {
            Hwnd = hwnd,
            Title = title,
            ClassName = className,
            Width = rect.Width,
            Height = rect.Height
        });

        return true;
    }, IntPtr.Zero);

    return windows;
}
```

### Estrutura de Dados para Identificação

```csharp
/// <summary>
/// Informações de uma janela de Virtual Device encontrada
/// </summary>
public class StreamDeckWindowInfo
{
    public IntPtr Hwnd { get; set; }
    public string Title { get; set; }
    public string ClassName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Texto para exibição no dropdown
    /// Formato: "Stream Deck (536x662)"
    /// </summary>
    public string DisplayText => $"{Title} ({Width}x{Height})";
}

/// <summary>
/// Dados persistidos para identificar o device nas próximas sessões
/// </summary>
public class DeviceIdentifier
{
    /// <summary>
    /// Título da janela no momento da seleção
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Largura da janela no momento da seleção
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Altura da janela no momento da seleção
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Índice da janela se houver múltiplas com mesmo título/dimensões
    /// Ex: Se há 2 janelas "Stream Deck" de 536x662, qual era? (0 ou 1)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Classe da janela Qt (para validação adicional)
    /// </summary>
    public string ClassName { get; set; }
}
```

### Persistência nas Settings

| Setting | Tipo | Descrição |
|---------|------|-----------|
| `device_title` | string | Título da janela selecionada |
| `device_width` | int | Largura da janela |
| `device_height` | int | Altura da janela |
| `device_index` | int | Índice se houver duplicatas |
| `device_class` | string | Classe Qt da janela |

### Algoritmo de Reconexão

Quando o widget inicia ou precisa reconectar:

```csharp
public IntPtr FindSavedDevice(DeviceIdentifier saved)
{
    // 1. Buscar todas as janelas do Stream Deck
    var windows = FindAllStreamDeckWindows();

    if (windows.Count == 0)
        return IntPtr.Zero; // App não está rodando

    // 2. Filtrar por título e dimensões
    var matches = windows
        .Where(w => w.Title == saved.Title &&
                    w.Width == saved.Width &&
                    w.Height == saved.Height)
        .ToList();

    if (matches.Count == 0)
        return IntPtr.Zero; // Device não encontrado

    if (matches.Count == 1)
        return matches[0].Hwnd; // Match único!

    // 3. Múltiplas janelas idênticas - usar índice
    if (saved.Index < matches.Count)
        return matches[saved.Index].Hwnd;

    // 4. Índice inválido - retornar primeira
    return matches[0].Hwnd;
}
```

### Fluxo de Seleção na UI

```
┌─────────────────────────────────────────────────────────────┐
│  Stream Deck Mirror - Configurações                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ CASO 1: App Stream Deck NÃO está rodando                ││
│  ├─────────────────────────────────────────────────────────┤│
│  │                                                          ││
│  │  ⚠️ Stream Deck não detectado                           ││
│  │                                                          ││
│  │  Device: [▼ (nenhum disponível)      ] [🔄 Detectar]    ││
│  │          └─ dropdown desabilitado                        ││
│  │                                                          ││
│  │  Abra o aplicativo Elgato Stream Deck para selecionar   ││
│  │  um Virtual Device.                                      ││
│  │                                                          ││
│  └─────────────────────────────────────────────────────────┘│
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ CASO 2: App rodando, 1 Virtual Device                   ││
│  ├─────────────────────────────────────────────────────────┤│
│  │                                                          ││
│  │  ✅ 1 Virtual Device encontrado                         ││
│  │                                                          ││
│  │  Device: [▼ Stream Deck (536x662)    ] [🔄 Atualizar]   ││
│  │                                                          ││
│  └─────────────────────────────────────────────────────────┘│
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ CASO 3: App rodando, múltiplos Virtual Devices          ││
│  ├─────────────────────────────────────────────────────────┤│
│  │                                                          ││
│  │  ✅ 3 Virtual Devices encontrados                       ││
│  │                                                          ││
│  │  Device: [▼ Stream Deck (536x662)    ] [🔄 Atualizar]   ││
│  │          ├─ Stream Deck (536x662)      ← selecionado    ││
│  │          ├─ Stream Deck XL (800x480)                    ││
│  │          └─ Stream Deck Mini (320x240)                  ││
│  │                                                          ││
│  └─────────────────────────────────────────────────────────┘│
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ CASO 4: Múltiplos devices IDÊNTICOS (mesmo modelo)      ││
│  ├─────────────────────────────────────────────────────────┤│
│  │                                                          ││
│  │  ⚠️ 2 Virtual Devices idênticos encontrados             ││
│  │                                                          ││
│  │  Device: [▼ Stream Deck (536x662) #1 ] [🔄 Atualizar]   ││
│  │          ├─ Stream Deck (536x662) #1   ← índice 0       ││
│  │          └─ Stream Deck (536x662) #2   ← índice 1       ││
│  │                                                          ││
│  │  💡 Dica: Observe o preview na tela WigiDash para       ││
│  │     identificar qual device é qual.                      ││
│  │                                                          ││
│  └─────────────────────────────────────────────────────────┘│
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## Estados Visuais do Widget

O widget exibe diferentes telas dependendo do estado de conexão com o Virtual Device.

### Enum de Estados

```csharp
public enum WidgetState
{
    /// <summary>
    /// Aplicativo Stream Deck não está em execução
    /// </summary>
    AppNotRunning,

    /// <summary>
    /// App está rodando, mas o Virtual Device configurado não foi encontrado
    /// (pode ter sido removido ou renomeado)
    /// </summary>
    DeviceNotFound,

    /// <summary>
    /// Device encontrado, mas erro ao capturar imagem
    /// (PrintWindow falhou, janela minimizada de forma estranha, etc.)
    /// </summary>
    CaptureError,

    /// <summary>
    /// Nenhum device foi configurado ainda (primeira execução)
    /// </summary>
    NotConfigured,

    /// <summary>
    /// Tudo funcionando - exibindo conteúdo do Virtual Device
    /// </summary>
    Connected
}
```

### Telas de Estado

#### Estado: AppNotRunning

```
┌─────────────────────────────────────────┐
│                                         │
│                                         │
│           ┌───────────┐                 │
│           │  🖥️  ❌   │                 │
│           └───────────┘                 │
│                                         │
│      Stream Deck não está               │
│         em execução                     │
│                                         │
│    Inicie o aplicativo Elgato          │
│    Stream Deck para continuar          │
│                                         │
│                                         │
├─────────────────────────────────────────┤
│  [👁] Rodapé (se ativado)              │
└─────────────────────────────────────────┘
```

**Detalhes:**
- Ícone: Monitor/computador com X vermelho
- Cor de fundo: Cor configurada (backgroundColor)
- Texto centralizado
- Verifica periodicamente se o app iniciou (a cada 2-3 segundos)

#### Estado: DeviceNotFound

```
┌─────────────────────────────────────────┐
│                                         │
│                                         │
│           ┌───────────┐                 │
│           │  🔍  ❓   │                 │
│           └───────────┘                 │
│                                         │
│       Virtual Device                    │
│     "Stream Deck XL"                    │
│       não encontrado                    │
│                                         │
│    O device pode ter sido removido     │
│    ou o Stream Deck reiniciado         │
│                                         │
│       [ 🔄 Reconectando... ]           │
│                                         │
├─────────────────────────────────────────┤
│  [👁] Rodapé (se ativado)              │
└─────────────────────────────────────────┘
```

**Detalhes:**
- Ícone: Lupa com ponto de interrogação
- Mostra o nome do device que estava configurado
- Tenta reconectar automaticamente a cada 2-3 segundos
- Se o device voltar, reconecta silenciosamente

#### Estado: CaptureError

```
┌─────────────────────────────────────────┐
│                                         │
│                                         │
│           ┌───────────┐                 │
│           │  ⚠️  🖼️   │                 │
│           └───────────┘                 │
│                                         │
│       Erro ao capturar                  │
│           imagem                        │
│                                         │
│    Tentando novamente em 3s...         │
│                                         │
│    Se o problema persistir,            │
│    reinicie o Stream Deck              │
│                                         │
│                                         │
├─────────────────────────────────────────┤
│  [👁] Rodapé (se ativado)              │
└─────────────────────────────────────────┘
```

**Detalhes:**
- Ícone: Aviso (triângulo) com imagem/frame
- Mantém o último frame válido como fallback (opcional)
- Retry automático
- Contador de tentativas visível

#### Estado: NotConfigured

```
┌─────────────────────────────────────────┐
│                                         │
│                                         │
│           ┌───────────┐                 │
│           │  ⚙️  ➡️   │                 │
│           └───────────┘                 │
│                                         │
│      Nenhum Virtual Device             │
│         configurado                     │
│                                         │
│    Abra as configurações do widget     │
│    para selecionar um device           │
│                                         │
│                                         │
│                                         │
├─────────────────────────────────────────┤
│  [👁] Rodapé (se ativado)              │
└─────────────────────────────────────────┘
```

**Detalhes:**
- Ícone: Engrenagem com seta
- Exibido na primeira execução
- Instrui o usuário a configurar

#### Estado: Connected

```
┌─────────────────────────────────────────┐
│░░░│                           │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
│░░░│   [Conteúdo real do      │░░░░░░░░░│
│░░░│    Stream Deck           │░░░░░░░░░│
│░░░│    Virtual Device]       │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
│░░░│                           │░░░░░░░░░│
├───┴───────────────────────────┴─────────┤
│  [👁] Rodapé (se ativado)              │
└─────────────────────────────────────────┘
```

**Detalhes:**
- Exibe o conteúdo capturado do Virtual Device
- Letterbox conforme aspect ratio
- Rodapé de controle (se ativado)

### Implementação do Renderer de Estados

```csharp
public static class StateRenderer
{
    public static void RenderState(
        Graphics g,
        WidgetState state,
        int widgetWidth,
        int widgetHeight,
        bool showFooterBar,
        Color backgroundColor,
        string deviceName = null,
        int retryCountdown = 0)
    {
        // Calcular área de conteúdo (excluindo rodapé)
        int contentHeight = showFooterBar
            ? (int)(widgetHeight * 0.95)
            : widgetHeight;

        // Limpar fundo
        using (var brush = new SolidBrush(backgroundColor))
        {
            g.FillRectangle(brush, 0, 0, widgetWidth, contentHeight);
        }

        // Configurar texto
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        var titleFont = new Font("Segoe UI", 14, FontStyle.Bold);
        var subtitleFont = new Font("Segoe UI", 10, FontStyle.Regular);
        var textBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
        var dimTextBrush = new SolidBrush(Color.FromArgb(150, 255, 255, 255));

        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        // Área central para ícone
        int iconSize = Math.Min(widgetWidth, contentHeight) / 4;
        int iconY = contentHeight / 3 - iconSize / 2;
        int iconX = (widgetWidth - iconSize) / 2;

        // Renderizar baseado no estado
        switch (state)
        {
            case WidgetState.AppNotRunning:
                DrawIcon(g, "🖥️❌", iconX, iconY, iconSize);
                DrawText(g, "Stream Deck não está", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 20, sf);
                DrawText(g, "em execução", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 45, sf);
                DrawText(g, "Inicie o aplicativo Elgato", subtitleFont, dimTextBrush,
                    widgetWidth / 2, contentHeight / 2 + 80, sf);
                DrawText(g, "Stream Deck para continuar", subtitleFont, dimTextBrush,
                    widgetWidth / 2, contentHeight / 2 + 100, sf);
                break;

            case WidgetState.DeviceNotFound:
                DrawIcon(g, "🔍❓", iconX, iconY, iconSize);
                DrawText(g, "Virtual Device", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 20, sf);
                if (!string.IsNullOrEmpty(deviceName))
                {
                    DrawText(g, $"\"{deviceName}\"", subtitleFont, textBrush,
                        widgetWidth / 2, contentHeight / 2 + 45, sf);
                }
                DrawText(g, "não encontrado", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 70, sf);
                DrawText(g, "🔄 Reconectando...", subtitleFont, dimTextBrush,
                    widgetWidth / 2, contentHeight / 2 + 110, sf);
                break;

            case WidgetState.CaptureError:
                DrawIcon(g, "⚠️🖼️", iconX, iconY, iconSize);
                DrawText(g, "Erro ao capturar", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 20, sf);
                DrawText(g, "imagem", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 45, sf);
                if (retryCountdown > 0)
                {
                    DrawText(g, $"Tentando novamente em {retryCountdown}s...",
                        subtitleFont, dimTextBrush,
                        widgetWidth / 2, contentHeight / 2 + 85, sf);
                }
                break;

            case WidgetState.NotConfigured:
                DrawIcon(g, "⚙️", iconX, iconY, iconSize);
                DrawText(g, "Nenhum Virtual Device", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 20, sf);
                DrawText(g, "configurado", titleFont, textBrush,
                    widgetWidth / 2, contentHeight / 2 + 45, sf);
                DrawText(g, "Abra as configurações do widget", subtitleFont, dimTextBrush,
                    widgetWidth / 2, contentHeight / 2 + 85, sf);
                DrawText(g, "para selecionar um device", subtitleFont, dimTextBrush,
                    widgetWidth / 2, contentHeight / 2 + 105, sf);
                break;
        }

        // Cleanup
        titleFont.Dispose();
        subtitleFont.Dispose();
        textBrush.Dispose();
        dimTextBrush.Dispose();
    }

    private static void DrawIcon(Graphics g, string emoji, int x, int y, int size)
    {
        // Renderizar emoji como ícone
        // Em produção, usar imagens PNG reais para melhor qualidade
        using (var font = new Font("Segoe UI Emoji", size / 2))
        using (var brush = new SolidBrush(Color.White))
        {
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(emoji, font, brush, x + size / 2, y + size / 2, sf);
        }
    }

    private static void DrawText(Graphics g, string text, Font font,
        Brush brush, float x, float y, StringFormat sf)
    {
        g.DrawString(text, font, brush, x, y, sf);
    }
}
```

### Transições de Estado

```
                                    ┌─────────────────┐
                                    │  NotConfigured  │
                                    │ (primeira vez)  │
                                    └────────┬────────┘
                                             │ usuário configura device
                                             ▼
┌─────────────────┐  app fechado   ┌─────────────────┐  app aberto    ┌─────────────────┐
│  AppNotRunning  │◄───────────────│    Connected    │───────────────►│  AppNotRunning  │
│                 │                │  (funcionando)  │                │                 │
└────────┬────────┘                └────────┬────────┘                └────────┬────────┘
         │                                  │                                  │
         │ app inicia                       │ device removido                  │ app inicia
         ▼                                  ▼                                  ▼
┌─────────────────┐                ┌─────────────────┐                ┌─────────────────┐
│    Connected    │◄───────────────│ DeviceNotFound  │───────────────►│    Connected    │
│  (funcionando)  │  device volta  │                 │                │  (funcionando)  │
└────────┬────────┘                └─────────────────┘                └─────────────────┘
         │
         │ PrintWindow falha
         ▼
┌─────────────────┐
│  CaptureError   │
│                 │
└────────┬────────┘
         │ retry bem-sucedido
         ▼
┌─────────────────┐
│    Connected    │
│  (funcionando)  │
└─────────────────┘
```

### Arquivos de Recursos para Estados

```
Resources/
├── state_app_not_running.png      # 128x128 - Monitor com X
├── state_device_not_found.png     # 128x128 - Lupa com ?
├── state_capture_error.png        # 128x128 - Triângulo de aviso
├── state_not_configured.png       # 128x128 - Engrenagem
├── icon.png                       # 128x128 - Ícone do widget
├── eye_open.png                   # 32x32 - Olho aberto (rodapé)
└── eye_closed.png                 # 32x32 - Olho fechado (rodapé)
```

---

## Tratamento de Erros

### Cenários de Erro

| Cenário | Detecção | Ação |
|---------|----------|------|
| Janela não encontrada | `FindTargetWindow` retorna `IntPtr.Zero` | Mostrar "Aguardando Stream Deck..." |
| Janela fechada durante execução | `IsWindow(hwnd)` retorna false | Tentar reconectar automaticamente |
| Stream Deck reiniciado | HWND inválido | `FindTargetWindow` novamente |
| Captura falha | `PrintWindow` retorna false | Manter último frame válido |
| Dimensões inválidas | width/height <= 0 | Skip frame |

### Mensagens de Status

```csharp
public enum WidgetStatus
{
    Disconnected,    // "Aguardando Stream Deck..."
    Connecting,      // "Conectando..."
    Connected,       // "Conectado | 536x662 | 10 FPS"
    Error            // "Erro: [mensagem]"
}
```

---

## Checklist de Implementação

### Fase 1: Estrutura Básica
- [ ] Configurar `.csproj` com .NET Framework 4.7.2
- [ ] Adicionar referência ao WigiDashWidgetFramework 1.1.0
- [ ] Criar `NativeMethods.cs` com P/Invoke declarations
- [ ] Criar `AspectRatioHelper.cs`
- [ ] Implementar `StreamDeckWidgetObject.cs` (metadata)
- [ ] Implementar `StreamDeckWidgetInstance.cs` (esqueleto)

### Fase 2: Detecção e Identificação de Devices
- [ ] Implementar `StreamDeckWindowInfo` e `DeviceIdentifier` classes
- [ ] Implementar `FindAllStreamDeckWindows()` com filtros (processo, classe Qt, título)
- [ ] Implementar `FindSavedDevice()` para reconexão
- [ ] Implementar persistência de `DeviceIdentifier` nas settings
- [ ] Testar detecção com app aberto/fechado

### Fase 3: Estados Visuais do Widget
- [ ] Implementar enum `WidgetState`
- [ ] Implementar `StateRenderer.cs` com todas as telas de estado
- [ ] Criar imagens PNG para cada estado (app_not_running, device_not_found, capture_error, not_configured)
- [ ] Implementar transições de estado automáticas
- [ ] Testar todos os cenários de erro

### Fase 4: Captura de Janela
- [ ] Implementar `CaptureWindow()` com PrintWindow
- [ ] Implementar loop de captura em background thread
- [ ] Implementar verificação periódica de estado do app/device
- [ ] Testar captura básica funcionando no WigiDash

### Fase 5: Aspect Ratio e Renderização
- [ ] Implementar cálculo de letterbox (com suporte a rodapé)
- [ ] Implementar renderização com preservação de aspect ratio
- [ ] Testar com diferentes tamanhos de widget
- [ ] Adicionar configuração de cor de fundo

### Fase 6: Rodapé de Controle
- [ ] Implementar `FooterBarRenderer.cs`
- [ ] Implementar detecção de long press
- [ ] Implementar toggle de visibilidade via rodapé
- [ ] Adicionar feedback visual (progress durante long press)
- [ ] Testar ícone de estado (visível/oculto)

### Fase 7: Click Forwarding
- [ ] Implementar mapeamento de coordenadas (com rodapé)
- [ ] Implementar detecção de click no letterbox
- [ ] Implementar detecção de click no rodapé
- [ ] Implementar envio de click via PostMessage
- [ ] Testar clicks nos botões do Stream Deck

### Fase 8: Settings UI
- [ ] Criar `StreamDeckSettingsControl.xaml`
- [ ] Implementar dropdown de devices (com estados: vazio, 1 device, múltiplos, idênticos)
- [ ] Implementar botão "Detectar/Atualizar" lista de devices
- [ ] Implementar mensagens contextuais (app não rodando, etc.)
- [ ] Implementar slider de refresh interval
- [ ] Implementar checkbox de ocultar janela
- [ ] Implementar checkbox de mostrar rodapé
- [ ] Implementar slider de long press duration
- [ ] Implementar persistência de settings

### Fase 9: Ocultação de Janela
- [ ] Implementar `HideWindow()` com transparência
- [ ] Implementar `RestoreWindow()`
- [ ] Garantir restauração no Dispose
- [ ] Testar captura com janela oculta

### Fase 10: Polish
- [ ] Tratamento de erros robusto
- [ ] Reconexão automática com retry inteligente
- [ ] Status indicator na UI de settings
- [ ] Criar ícones do widget (128x128 PNG)
- [ ] Criar ícones de estado (128x128 PNG cada)
- [ ] Criar ícones de olho (aberto/fechado 32x32)
- [ ] Criar `deploy.ps1`
- [ ] Testar em produção

---

## Referências

### Repositórios Base
- https://github.com/victorperez2911/WigiDash-WebPageWidGet (estrutura e padrões)
- https://github.com/victorperez2911/WigiDash-DiscordWidGet (exemplo avançado)
- https://github.com/victorperez2911/WigiDashWidgetFramework (framework)

### Documentação Microsoft
- [PrintWindow](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow)
- [SetLayeredWindowAttributes](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes)
- [PostMessage](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postmessagew)

### WigiDash Framework
- NuGet: `victorperez2911.WigiDashWidgetFramework` v1.1.0
- Interfaces: `IWidgetObject`, `IWidgetInstance`

---

## Resultados dos Testes Realizados

### Teste 1: Identificação da Janela
- **Status:** ✅ SUCESSO
- **Resultado:** HWND 133398, Classe Qt693QWindowToolSaveBits, Título "Stream Deck"

### Teste 2: Captura com Janela Visível
- **Status:** ✅ SUCESSO
- **Resultado:** Screenshot capturado perfeitamente (536x662 pixels)

### Teste 3: Captura com Transparência (alpha=0)
- **Status:** ✅ SUCESSO
- **Resultado:** Janela invisível no monitor, PrintWindow capturou conteúdo completo

### Teste 4: Captura com Hide Nativo (Win+F12)
- **Status:** ❌ FALHOU
- **Resultado:** Screenshot completamente preto - Stream Deck para de renderizar

### Conclusão
Usar método de transparência (`SetLayeredWindowAttributes` com alpha=0) para ocultar a janela, **não** o hide nativo do Stream Deck.

---

*Documento atualizado em: Janeiro 2026*
*Versão: 3.0 - Adicionado seleção de devices, identificação persistente e estados visuais de erro*
