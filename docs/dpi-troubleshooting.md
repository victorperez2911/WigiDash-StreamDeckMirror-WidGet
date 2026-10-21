# Captura e cliques durante mudanças de DPI

Em tela cheia exclusiva com o monitor duplicado para uma placa de captura, foi
observada uma mudança de DPI na janela do Stream Deck de 96 para 192.
O tamanho físico mudou de 914x662 para 1828x1324, enquanto a consulta feita no
contexto original do Manager continuou retornando 914x662. O buffer menor
capturava somente o canto superior esquerdo da janela.

## Correção

- Consultar o tamanho e executar `PrintWindow` em um contexto de thread com
  coordenadas físicas, restaurando o contexto anterior ao terminar.
- Converter os pixels físicos da captura para coordenadas da área cliente do
  destino e manter o contexto de DPI do destino durante os dois `PostMessage`.
- Recalcular o encaixe quando mudar o tamanho de origem ou de destino.
- Serializar o acesso ao buffer de captura e descartar quadros cujo tamanho
  mudou durante a captura.

O contexto de DPI do processo do WigiDash Manager não é alterado. A correção
usa APIs de contexto de DPI disponíveis no Windows 10 versão 1607 ou posterior.

## Verificação

Compilar e executar os testes de integração pelo Windows PowerShell:

```powershell
dotnet build .\StreamDeckMirrorWidget.csproj -c Debug
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-WindowCapture.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ClickDpi.ps1
```

O teste de captura precisa de uma janela do Stream Deck aberta e verifica o
tamanho físico da imagem e a restauração de três contextos de DPI do chamador.
O teste de cliques envia mensagens somente para uma janela de teste invisível
do próprio processo; ele não aciona comandos do usuário. Os dois scripts
aceitam `-WidgetDll` para testar outra configuração de build.

Na validação manual, alternar entre desktop e jogo em tela cheia exclusiva,
com a duplicação ativa. Conferir a grade completa e o mesmo botão antes e
durante o jogo. A imagem e o botão do Discord foram confirmados funcionando
no equipamento usado para reproduzir o problema.

## Diagnóstico opcional

Desativado por padrão. Para habilitar, criar um arquivo vazio chamado
`capture-diagnostics.enabled` junto da DLL instalada do widget e reiniciar o
Manager. Remover esse arquivo e reiniciar para desativar.

As imagens de origem e saída e a tabela de medidas são gravadas em
`%LOCALAPPDATA%\WigiDash\StreamDeckMirror\Diagnostics`, em uma pasta por
instância/inicialização. São até 240 pares, com intervalo de cinco segundos
entre amostras. Os registros incluem o nome do processo em primeiro plano e
imagens dos botões, que podem conter informações pessoais. Esses arquivos
são locais e não devem ser publicados junto com o código. Os registros de
coordenadas dos toques aparecem no log do Manager como `ClickTrace`.

Referências: [GetWindowRect e virtualização de DPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect),
[contexto de DPI da thread](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext),
[conversão de coordenadas físicas](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-physicaltologicalpointforpermonitordpi).
