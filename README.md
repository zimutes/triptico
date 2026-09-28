# Tríptico · by zimutek

Perfis de ecrãs para Windows. Liga e desliga monitores num clique ou com um atalho de teclado,
sem ir às Definições do Windows.

Feito para quem partilha monitores entre dois PCs: quando os ecrãs passam para o PC do trabalho,
um atalho deixa só o ecrã que sobra ligado; no fim do dia, outro atalho volta a ligar todos, cada um no
seu sítio.

## O que faz

- **Perfis editáveis**: que ecrãs ficam ligados, qual é o principal e onde fica cada um.
- **Guardar a disposição atual** como perfil, ou criar os **perfis sugeridos**
  (todos os ecrãs + só um de cada vez).
- **Atalhos globais** (ex.: Ctrl + Alt + F1), a funcionar com a janela fechada.
- **Ícone na bandeja** com os perfis no botão direito.
- **Identificar ecrãs**: mostra um número grande em cada monitor.
- **Nomes próprios** para os monitores («Esquerda», «Portátil»…).
- **Linha de comandos**, para atalhos no ambiente de trabalho ou Stream Deck.
- Arranque com o Windows (opcional), sem precisar de administrador.

## Instalar

Requer o runtime **.NET 10 Desktop** (ou publicar a versão autónoma).

```powershell
.\tools\publicar.ps1             # compila e instala em %LOCALAPPDATA%\Programs\Triptico + menu Iniciar
.\tools\publicar.ps1 -Autonomo   # .exe que corre em qualquer PC, sem .NET instalado (~70 MB)
.\tools\publicar.ps1 -SemInstalar
```

## Linha de comandos

```text
Triptico.exe --perfil "Trabalho"   aplica o perfil e sai
Triptico.exe --listar              mostra ecrãs e perfis
Triptico.exe --testar "Trabalho"   pergunta ao Windows se aceitaria o perfil, sem mudar nada
Triptico.exe --bandeja             arranca escondido na bandeja
```

## Dados

`%APPDATA%\Triptico\definicoes.json` (perfis, nomes dos ecrãs, opções) e `registo.txt` (diagnóstico).
A variável de ambiente `TRIPTICO_DADOS` aponta para outra pasta (testes, modo portátil).

## Se algo correr mal

**Win + P → Expandir** volta sempre a ligar todos os ecrãs.

## Como funciona

Usa a API CCD do Windows (`QueryDisplayConfig` / `SetDisplayConfig`), a mesma das Definições de ecrã:

1. **Topologia**: liga exatamente os ecrãs do perfil. Primeiro pede ao Windows a disposição que ele já
   conhece para esse conjunto; se não conhecer, mantém os modos dos ecrãs que ficam e deixa o Windows
   completar os novos.
2. **Disposição**: acerta as posições guardadas e põe o principal em (0,0). Se o perfil desliga o ecrã
   do meio, os outros encostam-se (sem buracos nem sobreposições).

Os monitores são reconhecidos pelo caminho do dispositivo e, se esse mudar (drivers reinstalados),
pelo modelo no EDID.
