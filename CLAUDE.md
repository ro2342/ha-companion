# HA Companion — guia do projeto

App UWP nativo (C#/XAML) que funciona como painel de controle do Home
Assistant pro Lumia 830 (Windows 10 Mobile). Ver `ha-companion-w10m.md`
pra especificação original (motivação, layout, mapeamento domínio→card) e
`prompt.md` pro histórico narrativo de tudo que já foi feito.

## Onde as coisas ficam

```
uwp/HaCompanionUWP/
├── HaCompanionUWP.csproj       ← csproj old-style, SEM wildcard — todo
│                                  .xaml/.xaml.cs/.cs novo precisa entrada
│                                  manual (<Compile Include>/<Page Include>)
├── Package.appxmanifest         ← nome, ícone, capabilities, versão
├── App.xaml(.cs)                ← estilos compartilhados + fallback de erro
├── MainPage.xaml(.cs)            ← shell: hambúrguer + SplitView + Frame
├── Models/
│   ├── HaEntityState.cs         ← espelha item de /api/states, DisplayValue/
│   │                               ContextLine por domínio
│   ├── DashboardTile.cs          ← heading | entidade solta | grupo
│   └── DashboardInfo.cs          ← item da lista de dashboards (Ajustes)
├── Services/
│   ├── CredentialStore.cs        ← PasswordVault (token) + LocalSettings
│   │                                (URL, favoritos, dashboard url_path/título)
│   ├── HaApiService.cs           ← REST: /api/states, /api/services/...
│   ├── HaWebSocketService.cs     ← WebSocket: lovelace/config, dashboards/list,
│   │                                todo/item/list+update (só o que não existe em REST)
│   ├── DashboardParser.cs        ← achata+agrupa a config do dashboard
│   ├── EntityCardTemplateSelector.cs      ← seletor de template p/ Favoritos
│   ├── DashboardTileTemplateSelector.cs   ← seletor de template p/ Dashboard
│   ├── LightControlFlyout.cs     ← painel ligar/brilho/cor, compartilhado
│   ├── ThemeHelper.cs            ← cor de destaque do sistema
│   └── UpdateCheckService.cs     ← checa/baixa/instala atualização
├── Views/                        ← uma página por seção do app
├── Assets/                       ← ícones gerados por generate_tile_assets.py
└── generate_tile_assets.py       ← gera os PNGs a partir de ../../logo.svg
.github/workflows/
├── 01-generate-cert.yml          ← manual, uma vez: gera o certificado de sideload
└── 02-build-appx.yml             ← automático a cada push em uwp/**: builda
                                     + publica artefato + publica app/ no GitHub Pages
app/                               ← index.html de download (estático) +
                                      app.appxbundle/version.json (gerados pelo workflow 02)
```

Link de download estável: **https://ro2342.github.io/ha-companion/**
(sempre a versão mais recente publicada pelo `main`).

## Regras permanentes desta sessão (não esquecer)

1. **Nunca usar `--` literal em lugar nenhum do código** — nem comentário
   `//`/`<!-- -->`, nem string mostrada ao usuário, nem Markdown. Sempre em
   dash `—`. Motivo original: `--` dentro de `<!-- -->` quebra o parser XML
   (`XamlParseException` em tempo de execução, não erro de compilação).
   Regra ampliada pra qualquer lugar por consistência. Varredura em massa:
   ```
   python3 -c "
   import re, glob
   pattern = re.compile(r'(?<=\s)-{2,}(?=\s)')
   for f in glob.glob('uwp/HaCompanionUWP/**/*.cs', recursive=True):
       with open(f, encoding='utf-8') as fh: content = fh.read()
       new_content, n = pattern.subn('—', content)
       if n:
           with open(f, 'w', encoding='utf-8') as fh: fh.write(new_content)
   "
   ```
   (exige espaço dos dois lados, não toca em `i--` nem `git diff -- path`).
   Validar XAML/csproj/manifest antes de commitar:
   ```
   python3 -c "import xml.dom.minidom as m; m.parse('arquivo')"
   ```

2. **Fluxo padrão de toda mudança**: validar localmente, commitar, dar
   `git push` pro `main` na hora (sem esperar o usuário perguntar por quê
   não subiu), depois acompanhar o Actions até verde:
   ```
   gh run list --repo ro2342/ha-companion --limit 2
   gh run watch <id> --repo ro2342/ha-companion --exit-status
   gh run view <id> --repo ro2342/ha-companion --log-failed   # se falhar
   ```
   Se falhar, ler o log, corrigir, commitar de novo, re-push — sem esperar
   o usuário pedir. Bump de versão em `Package.appxmanifest` a cada mudança
   (o checador de atualização do próprio app depende disso).

3. **Sem toolchain UWP local** (ambiente é Linux) — não dá pra compilar
   nem testar antes do push. A primeira validação real é o build no
   GitHub Actions (`windows-latest`), e a validação de verdade-de-verdade é
   o rod testando no Lumia físico. Já aconteceram bugs que só aparecem
   rodando no aparelho real (não no build, que passa verde mesmo assim):
   - `Setter Property="Style"` inválido em `App.xaml` (não existe herança
     de estilo por Setter, só `BasedOn`) — crashava antes da splash screen.
   - `SymbolIcon Symbol="Play"` — compila contra o SDK 17763, mas o enum
     `Symbol` pode não ter esse membro no runtime real do Windows 10
     Mobile do Lumia. **Antes de usar qualquer `SymbolIcon Symbol="X"`
     novo, conferir se X já está confirmado funcionando** (conjunto seguro
     conhecido: `Home`, `List`, `Favorite`, `Repair`, `Setting`, `Sync`,
     `Refresh`, `Library`, `Accept` — todos usados e comprovados no
     artistsway). Se não tiver certeza, usar `FontIcon Glyph="&#xNNNN;"`
     com o glifo cru da Segoe MDL2 Assets em vez do enum.
   - `GetNamedString(key, null)` — parâmetro HSTRING do WinRT não aceita
     `null` como defaultValue (`ArgumentNullException`/`Null_HString`). Usar
     sempre `string.Empty` como default.
   - `GetNamedString`/`GetNamedArray` só caem no `defaultValue` quando a
     CHAVE não existe — se existir com tipo diferente do esperado (comum
     em cards de terceiros HACS, esquema livre), estouram "This is not a
     string value" em vez de usar o default. `DashboardParser.cs` tem
     `GetString`/`GetArray` próprios que conferem `ValueType` primeiro —
     usar esse padrão pra qualquer JSON de origem não controlada
     (dashboards, cards de terceiros), não confiar direto no
     `JsonObject.GetNamedXxx` do WinRT.
   - Controles/propriedades XAML mais novos que `TargetPlatformMinVersion`
     (10.0.14393.0) podem não existir no runtime real, mesmo compilando —
     mesma classe de risco do `SymbolIcon`. Evitados de propósito:
     `ColorPicker`/`ColorSpectrum` (SDK mais novo), `ComboBox.PlaceholderText`
     (incerteza sobre versão mínima). Preferir controles claramente
     fundamentais (`Slider`, `Grid`, `StackPanel`, `Button`, `TextBox`,
     `CheckBox`, `Flyout`, `ContentDialog` — todos confirmados em uso).
   - **Build tem que ser `Configuration=Release` com
     `UseDotNetNativeToolchain=true`** — não `Debug`. Isso foi descoberto
     comparando o `.appxbundle` real deste projeto contra o do artistsway
     (que funciona comprovadamente no mesmo Lumia): Debug gera dependência
     de `Microsoft.NET.CoreRuntime.1.1` + `VCLibs.*.Debug.14.00` (runtime
     de depuração, não pensado pra sideload em aparelho de usuário final);
     Release+.NET Native gera `Microsoft.NET.Native.Framework/Runtime.1.7`
     + `VCLibs.*.14.00` normal — o que o artistsway realmente usa hoje
     (não confiar só na documentação histórica de um projeto irmão, olhar
     o workflow/csproj atual dele).

4. **`internetClient` sozinho não basta** pra falar com um host de IP
   privado (o HA do usuário fica na rede local, tipo `192.168.x.x`) — o
   Windows isola isso na zona "Private Network". O manifesto também
   declara `privateNetworkClientServer`.

5. **Dashboard (Lovelace) é buscado via WebSocket**, não REST —
   `lovelace/config`/`lovelace/dashboards/list`/`todo/item/list`/
   `todo/item/update` não existem como endpoint REST simples. Único lugar
   do app que usa `Windows.Networking.Sockets.MessageWebSocket`
   (`HaWebSocketService.cs`); todo o resto continua REST puro
   (`HaApiService.cs`), como a doc original pedia. Conexão nova por
   chamada (conecta, autentica, manda um comando, fecha) — sem estado de
   longa duração nem reconexão.

6. **Não dá pra replicar visualmente cards de terceiros** (`custom:mushroom-*`,
   `hue-like-light-card` etc.) — isso é o CSS/rendering de um projeto HACS
   de outra pessoa, não tenho o código. O caminho é extrair a entidade que
   cada card controla (campo `entity:`, quase sempre presente mesmo em
   cards customizados) e renderizar nativamente por domínio, estilo
   Windows Mobile. Cards sem `entity:` utilizável (`custom:firemote-card`,
   `custom:mushroom-template-card`) são ignorados silenciosamente — ver
   `DashboardParser.cs`. Cards que estavam agrupados no Lovelace original
   (`vertical-stack`/`grid`/`entities`) viram UM `DashboardTile.ForGroup`
   em vez de se espalharem em cards soltos e indistintos.

7. **Vacuum/Charlie**: antes era off-limits em qualquer HA (regra pessoal
   do rod), mas ele mesmo pediu pra incluir nesta leva de dashboard — já
   está dentro do escopo do app (ver memória permanente do usuário,
   atualizada em 2026-07-17).

## Como testar uma mudança

1. Editar, validar XML/sweep de `--` (ver regra 1).
2. Bump de versão em `Package.appxmanifest`.
3. Commit + push pro `main`.
4. Acompanhar `gh run watch` até verde.
5. Confirmar `https://ro2342.github.io/ha-companion/version.json` bate com
   a versão nova.
6. Pedir pro rod baixar/reinstalar e testar no Lumia — só isso valida de
   verdade (nem o build verde garante que funciona no aparelho real, ver
   regra 3).

## Primeiro setup (se for do zero)

Ver `uwp/README.md` — gerar o certificado (workflow 01, uma vez) antes do
primeiro build funcionar.
