# Tríptico

App Windows (WPF, .NET 10) de perfis de ecrãs: liga/desliga monitores e acerta a disposição.
Marca "by zimutek". Tudo em português de Portugal (interface, comentários, commits).

## Estrutura

- `src/Triptico/Display/` — interop CCD (`DisplayConfigNative`), leitura/aplicação (`DisplayManager`),
  arrumação sem buracos (`LayoutMath`).
- `src/Triptico/Profiles/` — modelo (`Profile`, `AppSettings`) e gravação em `%APPDATA%\Triptico\definicoes.json`.
- `src/Triptico/Services/` — atalhos globais, ícone da bandeja (WinForms `NotifyIcon`), arranque com o Windows.
- `src/Triptico/Views/` — janelas WPF com o tema Fluent (`ThemeMode="System"`).
- `tools/publicar.ps1` — publica um único `.exe` e instala em `%LOCALAPPDATA%\Programs\Triptico`.
- `tools/gerar-icone.ps1` — gera `Assets/triptico.ico`.

## Regras

- **Não aplicar perfis a sério durante testes**: muda os ecrãs do utilizador (e os monitores podem estar
  a mostrar o PC do trabalho). Para testar, usar `--testar "Perfil"` (SDC_VALIDATE, não muda nada)
  e `TRIPTICO_DADOS=<pasta de testes>` para não mexer nos perfis reais.
- WPF + WinForms no mesmo projeto: `System.Drawing` e `System.Windows.Forms` não estão nos usings
  globais; usar o alias `WinForms`.
- Atalhos por omissão com Ctrl + Alt + F-teclas: Ctrl + Alt + letra/número é AltGr no teclado PT
  e bloquearia @, €, {…
- Publicar com `--no-self-contained` (o `--self-contained false` do .NET 10 dá um exe de 165 MB).
- Repositório **público** (github.com/zimutes/triptico): nada de dados pessoais nem caminhos de
  dispositivo reais; commits com o email noreply do GitHub (já configurado no repo).
- `README.md` em inglês e `LEIAME.md` em português: manter os dois em sincronia. As capturas em
  `docs/` fazem-se com dados de demonstração (`TRIPTICO_DADOS`), nunca com os perfis reais.
