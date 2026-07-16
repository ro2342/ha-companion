# Companion do Home Assistant para Windows 10 Mobile

## A ideia

Um app nativo UWP, rodando no Lumia 830, que funciona como um painel de controle rápido pro Home Assistant — luzes, sensores e scripts (como o `rotina_dormir`) acessíveis direto do bolso, sem depender de navegador.

Não é uma réplica do Lovelace (a interface web do HA é feita em web components modernos, incompatível com o EdgeHTML do 830). É uma reconstrução nativa do mesmo conceito — cards por entidade — usando XAML puro.

## Por que dá pra fazer

O Home Assistant expõe uma **API REST** simples, na mesma porta do frontend (padrão 8123):

- Formato JSON em tudo, autenticação via header `Authorization: Bearer TOKEN`
- O token é um **Long-Lived Access Token**, gerado direto no seu perfil do HA — não precisa implementar o fluxo OAuth2 que o app oficial Android usa
- Endpoints principais:
  - `GET /api/states` → lista todas as entidades e seus estados atuais
  - `GET /api/states/<entity_id>` → estado de uma entidade específica
  - `POST /api/services/<domain>/<service>` → chama um serviço (ligar luz, rodar script), passando `entity_id` no corpo

Isso significa que o app inteiro pode funcionar com `HttpClient` do UWP + um token salvo no `PasswordVault`, sem WebSocket, sem SDK externo.

## Referência: app oficial do Android

Vale estudar o repo [`home-assistant/android`](https://github.com/home-assistant/android), mas com ressalva: é Kotlin/Compose puro, não porta pro UWP. O que aproveitar de lá é a **arquitetura**, não o código:

- Como eles separam models de API, autenticação e camada de rede (módulo `common`)
- Onde usam REST (ações pontuais) vs WebSocket (updates em tempo real)

Pro nosso caso, dá pra ignorar o WebSocket de saída e ficar só no REST — mais simples, e suficiente pra um companion de bolso.

## Layout — seguindo o padrão real do Windows 10 Mobile

Baseado nos apps nativos da época (MSN Weather, News), não em Material Design:

- **Barra superior sólida colorida**: ícone de hambúrguer à esquerda, título do app, lupa de busca à direita
- **Menu hambúrguer**: painel escuro sobreposto com a navegação entre seções (favoritos, luzes, sensores, scripts, configurações) — em vez de abas horizontais
- **Cards de conteúdo**: retos, sem sombra, fundo claro, cantos levemente arredondados. Cada card mostra:
  - Label pequeno em cinza (nome da entidade)
  - Valor grande em destaque (estado atual — "ligada", "21.4°C", "pronta")
  - Linha de contexto com ícone pequeno (brilho, "toque para ligar", "sensor · atualizado agora")
- **Barra de comando inferior**: poucos ícones simples (estrela, pin, "...") — sem os controles arredondados tipo switch do Android

## Mapeamento entidade → card

| Domínio HA | Comportamento no card |
|---|---|
| `light.*` | Card tocável, mostra ligado/desligado + brilho; toque dispara `light.toggle` |
| `sensor.*` | Card só leitura, mostra valor + unidade |
| `script.*` | Card com "toque para rodar"; dispara `script.turn_on` |
| `switch.*` | Igual light, sem o dado de brilho |

Essa lógica de "qual card pra qual domínio" vira um `DataTemplateSelector` no XAML — um template por domínio, todos alimentados pela mesma chamada a `/api/states`.

## Stack técnica

- UWP nativo, C#/XAML
- `HttpClient` para todas as chamadas REST
- `PasswordVault` para guardar o Long-Lived Access Token com segurança
- `NavigationView` ou painel customizado pro menu hambúrguer
- Sem dependência de WebSocket, sem SDK externo — só HTTP + JSON

## Próximos passos

1. Definir a lista de entidades que entram em "favoritos" (provavelmente: luzes do quarto/sala, sensor de temperatura, `script.rotina_dormir`)
2. Desenhar o menu hambúrguer aberto (segunda tela de referência)
3. Esboçar o `DataTemplateSelector` e os models em C# pra cada domínio
4. Implementar a chamada REST + parsing do JSON de `/api/states`
5. Implementar o toggle de luz e o disparo de script via `POST /api/services/...`
