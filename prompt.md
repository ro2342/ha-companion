# HA Companion — progresso da sessão (2026-07-16/17)

Histórico narrativo de tudo que foi feito nesta sessão, pra retomar
contexto numa conversa nova com o Claude Code. Repo:
`/home/rod/Dev/ha-companion`, remoto `https://github.com/ro2342/ha-companion`.
Ver `CLAUDE.md` pras regras permanentes/arquitetura; este arquivo é só o
"o que aconteceu e por quê", em ordem.

## O que é o projeto

App UWP nativo (C#/XAML) pro Lumia 830 (Windows 10 Mobile) que funciona
como painel de controle rápido do Home Assistant — luzes, sensores,
scripts e, mais recentemente, o dashboard Lovelace de verdade do rod,
renderizado nativamente. Especificação original em `ha-companion-w10m.md`.
Segue as mesmas convenções do projeto irmão `theartistsway/uwp/ArtistWayUWP`
(mesmo aparelho, mesma toolchain, mesmo padrão de shell hambúrguer+SplitView).

## Scaffold inicial

Projeto começou vazio (só a doc de especificação). Montado do zero
seguindo o artistsway como referência: `HaCompanionUWP.csproj` (UAP,
`TargetPlatformVersion=10.0.17763.0`, `TargetPlatformMinVersion=10.0.14393.0`,
`Platform=ARM`), `App.xaml(.cs)`, `MainPage.xaml(.cs)` com o shell de
hambúrguer+SplitView, nav inicial (Favoritos/Luzes/Sensores/Scripts/Ajustes).

Decisões do scaffold:
- **Favoritos configuráveis, sem entity_id cravado no código** — o rod
  pediu isso explicitamente depois de eu não saber os entity_id reais da
  conta dele. `FavoritesPickerPage` lista tudo ao vivo com checkbox.
- Conexão (URL + Long-Lived Access Token) fica em branco no primeiro uso,
  sem nada pré-preenchido — genérico pra qualquer instalação de HA.
- Ícone novo (`logo.svg`, casa simples) gerado via `generate_tile_assets.py`
  (adaptado do artistsway), rodei localmente instalando `cairosvg`+`pillow`.
- CI: dois workflows adaptados do artistsway (`01-generate-cert.yml` gera o
  certificado de sideload uma vez; `02-build-appx.yml` builda a cada push).
- Depois, a pedido do rod: página `/app` no GitHub Pages (link estável de
  download) e um checador de atualização no próprio app (baixa sozinho e
  mostra "Instalar agora", igual ao artistsway).

## Três bugs reais de boot (só apareceram testando no aparelho)

Depois do primeiro push verde no CI, o app não abria no Lumia — fechava
antes até da splash screen. Três causas reais, encontradas nesta ordem
(cada uma comprovadamente corrigida, confirmada pelo rod testando de
novo):

1. **`Setter Property="Style"` em `App.xaml`** — não é sintaxe válida de
   herança de estilo (o certo é o atributo `BasedOn`). Provável
   `XamlParseException` dentro do próprio `App.InitializeComponent`, antes
   de qualquer handler de erro existir.
2. **`SymbolIcon Symbol="Play"`** no ícone de nav de Scripts — comparado
   diretamente contra o `MainPage.xaml` do artistsway (a pedido do rod:
   "olha como ele tá fazendo"), esse símbolo não estava no conjunto já
   confirmado funcionando no runtime real do Lumia (mesma classe do bug
   histórico `GlobalNavigationButton` do artistsway). Trocado por
   `FontIcon` com glifo cru.
3. **A causa de verdade**: o workflow buildava com `Configuration=Debug`
   (eu tinha copiado isso de uma nota HISTÓRICA do progresso do
   artistsway, sobre uma correção antiga). Baixei e inspecionei o
   `.appxbundle` real dos dois projetos — o artistsway hoje builda com
   `Configuration=Release` + `UseDotNetNativeToolchain=true` de verdade
   (conferido no workflow atual dele, não só na doc). Meu build gerava
   dependência de `Microsoft.NET.CoreRuntime.1.1` + `VCLibs.Debug` (runtime
   de depuração, não pensado pra sideload em aparelho de usuário final);
   trocando pra Release+.NET Native, ficou igual ao que realmente funciona.

Também nessa leva: `ShowFatalError` em `MainPage.xaml.cs` ganhou um
fallback que não depende dos próprios elementos da página (caso a falha
seja bem cedo, antes deles existirem) — rede de segurança pra qualquer
crash futuro aparecer na tela em vez de falhar silenciosamente.

## Autenticação do `gh` CLI quebrou no meio da sessão

Depois de várias chamadas, o token OAuth do `gh` ficou inválido (não foi
nada que o rod fez) e o device flow (código+link) deu timeout duas vezes
seguidas — parece ser algo específico da rede deste sandbox, não do
GitHub. Resolvido com um Personal Access Token (escopos `repo` e
`workflow`) gerado manualmente pelo rod e configurado via
`gh auth login --with-token`.

## Feature grande: dashboard nativo (Lovelace de verdade)

O rod pediu pra trazer os dashboards reais dele (colou o YAML completo de
um) transformados em "tiles estilo Windows Mobile". Ele queria
inicialmente algo mais parecido com "um port real do app Android" — expliquei
a limitação de hardware real: o app oficial cobre dashboard via **WebView**
(Lovelace é web components modernos), e é exatamente isso que não
funciona no EdgeHTML do Lumia 830 (motivo original do projeto ser nativo
do zero). Não dá pra replicar visualmente cards de terceiros
(`mushroom-*`, `hue-like-light-card`) — não tenho o código deles.

Caminho acordado: buscar o dashboard de verdade via **WebSocket**
(`lovelace/config` não existe em REST simples — mudança de arquitetura
em relação ao design original, que evitava WebSocket de propósito) e
renderizar cada card nativamente por **entity + domínio**, genérico pra
qualquer instalação (não cravar nada específico da conta do rod).

Inspecionei os dashboards reais por SSH (`.storage/lovelace.dashboard_rod`
etc., leitura, sem mexer em nada) pra confirmar a estrutura antes de
escrever código. Validei também que `ws://<host>/api/websocket` responde
`auth_required` antes de implementar o protocolo.

Construído:
- `HaWebSocketService.cs` — cliente WebSocket fino (auth, correlação de
  comando/resposta por id, `lovelace/config`, `lovelace/dashboards/list`,
  `todo/item/list`/`update`).
- `DashboardParser.cs` — achata a config numa lista de tiles.
- `DashboardPage.xaml(.cs)` — nova aba "Dashboard".
- Domínios novos em `HaEntityState`: `media_player`, `fan`, `humidifier`,
  `weather`, `vacuum`, `number`, `update`, `todo`, `input_boolean`.
- Ajustes ganhou um seletor de dashboard (dropdown carregado via
  `lovelace/dashboards/list`, não mais digitar `url_path` na mão — pedido
  do rod depois que expliquei onde achar o valor manualmente).
- **Charlie (vacuum) entrou no escopo** — antes era regra pessoal do rod
  "nunca mexer nisso a menos que eu traga à tona"; ele trouxe à tona
  explicitamente nesta leva ("faz tudo que tem lá"), memória permanente
  atualizada pra refletir isso.

## Bugs reais encontrados testando o dashboard no aparelho

1. **`GetNamedString(key, null)`** — `ArgumentNullException`/`Null_HString`
   reportado pelo rod ao trocar de dashboard. Parâmetro HSTRING do WinRT
   não aceita `null`. Trocado por `string.Empty`.
2. **`GetNamedString`/`GetNamedArray` estouram em campo com TIPO errado**
   (não só chave ausente) — "This is not a string value. Use ValueType
   property to get the type." Dashboards de terceiros têm esquema livre
   (cards HACS), então esse risco é real. `DashboardParser.cs` ganhou
   `GetString`/`GetArray` próprios que conferem `ValueType` antes de
   extrair, em vez de confiar direto no método do WinRT.

## Refinamentos de usabilidade (depois do primeiro teste real completo)

O rod testou o dashboard de verdade e apontou dois problemas:
1. **Coisas que estavam juntas no Lovelace original viravam cards soltos
   e indistintos** — o vacuum (Charlie) e os 10 botões de escolher cômodo
   pra limpar, ou o status da impressora e os níveis de tinta, cada um
   virava seu próprio card idêntico aos outros, sem indicar relação.
   Corrigido: `DashboardParser` agora agrupa o conteúdo de
   `vertical-stack`/`grid`/`entities` num só `DashboardTile.ForGroup`
   (card único com linhas compactas dentro), usando o primeiro
   heading/title encontrado como legenda.
2. **Luzes RGB ficavam indistinguíveis de um interruptor comum** — só
   "ligada/desligada". `HaEntityState` ganhou menção a "Cor ajustável" na
   linha de contexto quando a luz tem `rgb_color`/`hs_color`/`color_temp`
   nos atributos.

Também nessa leva: reportado que a primeira verificação de atualização
às vezes falhava e só funcionava no segundo toque — provavelmente a
primeira conexão HTTPS pra um host novo (DNS+TLS do zero) sendo mais
lenta que as próximas. `UpdateCheckService`/`HaApiService` tiveram o
timeout do `HttpClient` subido de 8s pra 15s e ganharam uma tentativa
automática extra antes de desistir.

## Controle de brilho e cor pras luzes

Pedido do rod: além de ligar/desligar, poder ajustar brilho e cor (ele
sugeriu uma roda de cores ou um dropdown). Decisão: **sem**
`ColorPicker`/`ColorSpectrum` nativo do UWP (só existem em SDKs mais
novos que o `TargetPlatformMinVersion` deste projeto — mesma classe de
risco que já causou o crash do `SymbolIcon Play`) e **sem** roda de cor
desenhada na mão (mais código, mais risco, só validável no aparelho de
verdade). Em vez disso: `LightControlFlyout.cs` (compartilhado entre
Favoritos/Luzes/Dashboard) com botão ligar/desligar, `Slider` de brilho
(controle básico, seguro, existe desde a v1 do UWP) e uma grade de 10
botões de cor sólida pré-definida.

## Estado atual

Build `1.2.0.0`, CI verde, publicado em
`https://ro2342.github.io/ha-companion/`. Rod confirmou que o dashboard
básico funciona (agrupamento + cor RGB + retry ainda não confirmados por
ele no aparelho até o momento em que este arquivo foi escrito).

## Possíveis próximos passos (não pedidos ainda, só ficou registrado)

- `custom:firemote-card` e `custom:mushroom-template-card` continuam
  ignorados silenciosamente (sem `entity:` utilizável pra mapear).
- Só a primeira view de um dashboard é renderizada.
- `number`: só leitura + definir valor simples, sem slider/stepper rico.
- `todo`: lista + marcar concluído, sem adicionar/reordenar item.
- `weather`: condição + temperatura atual, sem previsão de dias futuros.
- `cover`: toggle genérico, sem botões explícitos abrir/fechar/parar.
- Sem cache offline — toda tela busca ao vivo (REST ou WebSocket) sempre
  que abre.
