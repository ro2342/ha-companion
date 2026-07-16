# HA Companion — UWP (Windows 10 Mobile / Lumia)

App UWP nativo em C#/XAML: painel de controle do Home Assistant pro Lumia
830, via API REST (`/api/states`, `/api/services/<domain>/<service>`), sem
WebView/Lovelace -- ver [`../ha-companion-w10m.md`](../ha-companion-w10m.md)
pra especificação completa (motivação, layout, mapeamento de domínio→card).

Estrutura e convenções de build seguem
[`theartistsway/uwp/ArtistWayUWP`](https://github.com/ro2342/theartistsway)
(mesmo aparelho, mesma toolchain), inclusive os workarounds específicos do
Lumia 830 documentados nos comentários do código (glifos crus em vez de
`SymbolIcon`, sem `StackPanel.Spacing`, build em Debug em vez de Release).

**Aviso importante:** eu não tenho como compilar nem testar isso antes de
você rodar -- não existe toolchain UWP fora do Windows, e meu ambiente aqui
é Linux. Escrevo tudo com o máximo de cuidado (validação de XML, conferência
cruzada de `x:Name` entre XAML e code-behind), mas a validação real acontece
no primeiro build do GitHub Actions depois do push. Se algo quebrar, me cola
o log do Actions.

## Passo 1 — gerar o certificado de assinatura (uma vez só)

Todo `.appxbundle` precisa ser assinado. Isso é feito automaticamente pelo
GitHub Actions, mas a chave só existe depois que você rodar isso uma vez:

1. No navegador (ou app do GitHub), vá na aba **Actions** do repositório
2. Clique no workflow **"01 - Gerar certificado de assinatura (rodar uma vez)"**
3. Clique em **"Run workflow"** → **"Run workflow"** de novo pra confirmar
4. Espere terminar (~1-2 min). Isso salva a chave privada no repositório
   automaticamente -- os próximos builds já vão conseguir assinar sozinhos

Não precisa repetir esse passo depois, a menos que queira trocar o certificado.

## Passo 2 — build automático

Qualquer `git push` que mexa em `uwp/` (ou em `logo.svg`) já dispara o
workflow **"02 - Build do appxbundle"** sozinho.

## Passo 3 — baixar e instalar no Lumia

1. Na aba **Actions**, abra a execução mais recente do workflow 02
2. Baixe o artefato **hacompanion-appxbundle** (é um `.zip`)
3. Dentro dele, o arquivo que importa é o `.appxbundle` (dentro de uma pasta
   tipo `HaCompanionUWP_1.0.0.0_Test`)
4. Transfira esse arquivo pro Lumia (e-mail pra você mesmo, OneDrive, cabo)
5. No Lumia: **Configurações > Atualização e segurança > Para
   desenvolvedores** → ative o **Modo desenvolvedor**
6. Abra o arquivo `.appxbundle` pelo Explorador de Arquivos do próprio
   celular e toque para instalar

Se der erro de "editor não confiável", o artefato do workflow 01 também
inclui um `hacompanion-public-cert.cer` -- transfira e instale esse primeiro
(ele vai pedir pra confirmar como certificado confiável), depois tente o
`.appxbundle` de novo.

## Primeiro uso no app

1. Abrir o app pela primeira vez leva direto pra **Ajustes** (sem URL/token
   salvos ainda)
2. Preencher a **URL base** do Home Assistant (ex: `http://192.168.x.x:8123`
   -- precisa ser um endereço alcançável pelo Lumia na mesma rede Wi-Fi) e o
   **Long-Lived Access Token** (gerado no seu perfil do HA: Perfil > Segurança
   > Long-Lived Access Tokens > Criar Token)
3. **Testar conexão** pra confirmar antes de salvar
4. **Salvar** -- o app cai na tela de Favoritos (vazia no primeiro uso)
5. Em Ajustes > **Escolher favoritos**, marcar quais luzes/sensores/scripts
   aparecem na tela inicial

## Por que C# e não JavaScript puro / por que essas versões de plataforma

Mesmo raciocínio do artistsway: o tipo de projeto UWP "só JavaScript"
(`.jsproj`) foi descontinuado, e o `TargetPlatformMinVersion=10.0.14393.0`
(Anniversary Update) é a base real do Lumia 830 -- por isso nada aqui usa
`StackPanel.Spacing`/`Grid.ColumnSpacing`/`RowSpacing` (só existem a partir
do Fall Creators Update, 10.0.16299); espaçamento é sempre via `Margin`.

## Estrutura

```
uwp/HaCompanionUWP/
├── HaCompanionUWP.csproj      ← projeto C#
├── Package.appxmanifest        ← manifesto do app (nome, ícone, TargetDeviceFamily)
├── App.xaml / App.xaml.cs      ← estilos compartilhados + tratamento de erro global
├── MainPage.xaml(.cs)          ← shell nativo: hambúrguer + SplitView + Frame
├── Models/HaEntityState.cs     ← espelha um item de GET /api/states
├── Services/
│   ├── CredentialStore.cs      ← PasswordVault (token) + LocalSettings (URL, favoritos)
│   ├── HaApiService.cs         ← HttpClient fino pra /api/states e /api/services/...
│   ├── EntityCardTemplateSelector.cs  ← escolhe o card certo por domínio
│   └── ThemeHelper.cs          ← cor de destaque do sistema
├── Views/                      ← Favoritos, Luzes, Sensores, Scripts, Escolher favoritos, Ajustes
└── Assets/                     ← ícones do app, gerados por generate_tile_assets.py a partir de ../../logo.svg
```

## O que fazer se der erro de `Windows.*` não encontrado no workflow 02

Mesmo problema que o artistsway já resolveu: o runner `windows-latest` do
GitHub não vem mais com os metadados do UWP por padrão. Se voltar a
acontecer, tentar uma versão diferente do SDK (`sdk-version: 18362` ou
`19041`) no passo `GuillaumeFalourd/setup-windows10-sdk-action`.
