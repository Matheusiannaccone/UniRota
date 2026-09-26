# Progresso do redesign UniRota

## Estado geral — fechamento em 25/09/2026

- Blocos 1, 2, 3 e 4 implementados e validados nos respectivos checkpoints. Não reimplementar.
- Fechamento: revisão integrada de código das 13 páginas e smoke nos cenários disponíveis concluídos; homologação manual completa depende dos cenários listados abaixo.
- Base revisada: `4d9adc4`, working tree inicialmente limpo.
- Nenhuma regressão funcional ou inconsistência visual que exigisse correção foi comprovada nesta revisão. Nenhum código, regra de negócio ou recurso compartilhado foi alterado.
- Android: build aprovado, **0 erros e 0 avisos**.
- .NET: **159/159 aprovados**, nenhum ignorado.
- `git diff --check`: aprovado.
- Único arquivo alterado: `docs/ui/REDESIGN_PROGRESS.md`.

## Histórico consolidado

| Bloco | Commit | Resultado |
|---|---|---|
| 1 — Fundação visual e autenticação | `71bc6dd` | Estilos semânticos, placeholders e autenticação implementados. |
| 2 — Shell, Home e Minhas Rotas | `2afa1db` | TabBar nativa Início/Rotas/Caronas e navegação interna implementadas. |
| 3 — Rotas, matching e mapa | `37fc2e3` | 157/157 .NET; Android sem erros/avisos; autocomplete, edição, seleção, busca sem resultados e teclado exercitados. |
| 4 — Solicitações e caronas | `4d9adc4` | 159/159 .NET; Android sem erros/avisos; segmentos, listas e diálogos revisados. |

Decisões preservadas:

- `RideRequest` não persiste endereços/horário: cards apresentam pessoas, dias/data, tipo, preço snapshot, status e timestamp disponível, sem novas consultas/campos.
- Caronas usa **Pontuais/Recorrentes**, derivadas de Once/Weekly. Não usar “Próximas” para uma coleção que inclui datas passadas.
- O Bloco 4 exercitou resultado compatível, resumo/preço da confirmação, calendário Once com bloqueio de dia incompatível e diálogo Weekly. Diálogos de envio/aceite/recusa foram cancelados, sem modificar registros.
- Marca textual e ícones funcionais continuam provisórios; nenhum asset fictício adicionado.

## Revisão das 13 páginas

Os 12 mockups e sua especificação foram consultados. Todos os XAMLs foram revisados; a tabela distingue execução atual de cobertura anterior/estática.

| Página | Evidência do fechamento |
|---|---|
| Startup | Loading/erro/retry revisados no código. Reabertura com sessão válida levou à Home; após logout levou ao Login. Tela transitória não capturada isoladamente. |
| Login | Tema claro, 320 dp, scroll até card auxiliar, entrada de e-mail longo e erro de campos vazios. Sem TabBar após logout e nova abertura. |
| Register | Abertura pelo Login, nome longo, foco/entrada e scroll até confirmação/ações finais em 320 dp; formulário também inspecionado a aproximadamente 411 dp. Nenhuma conta criada. |
| Home | Ações de busca, solicitações e abas acessíveis; scroll em 320 dp, restauração de sessão e logout exercitados. |
| MyRoutes | Loading com Nova rota desabilitado observado; endereços reais quebram linha; dias e Editar/Excluir acessíveis por scroll. Sem rede, erro/retry aparecem sem estado vazio simultâneo. |
| NewRoute | Digitação de endereço longo retornou sugestões reais do Places com quebra de linha e scroll. Formulário/endereço preservados ao trocar de aba e retornar. Nenhuma rota salva. |
| FindRide | Seleção existente, card, ajuda e ação de busca acessíveis em 320 dp. Binding `CanFindMatches` e notificações de estado revisados. |
| MatchResults | Resumo/lista/ações revisados; estado sem correspondência observado ao retornar do mapa. Resultado preenchido tem evidência do checkpoint 4, não foi novamente exercitado. |
| RouteDetails | Mapa real já aberto no emulador: tiles, polyline e pins visíveis; scroll até distância 14,2 km, duração 28,6 min e desvio 0 km/1,7 min. Viewport/loading/mensagens/fallback preservados no código; fallback sem polyline coberto pela suíte existente. |
| RideRequest | XAML, handlers e bindings Once/Weekly, data, preço, erro e `CanSubmit` revisados. Interação preenchida mantém evidência do checkpoint 4, não reexecutada nesta sessão. |
| AwaitingApproval | Estado vazio observado em 320 dp. Template com pessoas/status/data/dias/preço/envio revisado estaticamente; lista preenchida não disponível. |
| ReceivedRequests | Card real com nome, status, dias, preço e timestamp; scroll até Aceitar/Recusar em 320 dp/tema claro. Nenhuma transição executada. |
| ConfirmedRoutes | Recorrentes com dados reais e Pontuais vazio; troca de segmento/destaque visual observados em 320 dp/tema escuro. |

## Sistema visual e navegação

- `App.xaml` mantém `Colors.xaml` e `Styles.xaml` globais. Não há estilos locais duplicados nem cores hexadecimais nas 13 páginas.
- Títulos, legendas, cards, inputs, botões, badges, chips, espaçamentos e estados continuam centralizados. Dimensões locais remanescentes são específicas do placeholder de Startup e do mapa.
- Nenhum estilo/cor novo necessário. Mantidas as adaptações aprovadas nos quatro blocos.
- Conferidos recursos de tema claro/escuro, estados Disabled/seleção, bindings de loading/erro/vazio e guards de navegação dos handlers.
- TabBar única e nativa; autenticação fora dela; sete rotas internas registradas. Troca de abas preservou Nova Rota com texto digitado e stack da busca. Retorno pelo Android percorreu mapa/resultados/busca/Home.
- `CreateAuthenticatedTabs` recria raízes após logout; páginas autenticadas transientes no DI. Restauração/login/cadastro usam a rota absoluta da Home.
- Safe Area Android: conteúdo, toolbar e TabBar dentro das barras de status/navegação, sem sobreposição nas capturas inspecionadas. Não representa validação iOS.

## Validações executadas

```text
dotnet test UniRota.Tests/UniRota.Tests.csproj --no-restore
159 aprovados, 0 falhas, 0 ignorados.

dotnet build UniRota/UniRota.csproj -f net8.0-android --no-restore -p:EmbedAssembliesIntoApk=true
0 erros, 0 avisos.

git diff --check
Aprovado.
```

- O sandbox inicialmente impediu acesso do MSBuild aos SDKs locais; teste/build executados com a permissão necessária. Não foi falha do projeto.
- Sem impacto em Functions/configuração: testes Node não reexecutados, conforme AGENTS.md. Baseline anterior 20/20, sem nova alegação de execução.
- Smoke via ADB, hierarquia de UI e capturas do aplicativo já instalado no AVD API 34. APK deste build não reinstalado; não houve alteração de código.
- Principal dimensão: 840×1680 px a 420 dpi (320×640 dp). Cadastro também em 1080×2400 px (aproximadamente 411×914 dp).
- Temas claro/escuro exercitados por amostragem. Estado final: configuração inicial 840×1680, tema escuro, modo avião desligado; app no Login, campos temporários limpos após reinício.

## Limitações / homologação manual restante

1. **Fluxo completo com duas contas dedicadas:** novo login válido, cadastro efetivo, envio Once/Weekly, aceite/recusa e consumo de vagas não executados neste fechamento. Logout e restauração da sessão existente verificados. Não havia credenciais fornecidas para novo login.
2. **Mapa:** execução visual cobriu mapa real disponível, não todos os fallbacks (falha de renderização do SDK, pins parciais, ausência de polyline). Código/testes existentes cobrem parte desses casos; não substituem smoke visual individual.
3. **Listas/estados:** não foram produzidos dados artificiais no Firestore. Aguardando preenchido, matching preenchido e confirmação dependem de cenário dedicado para nova inspeção. Evidência anterior do Bloco 4 separada acima. Nem todo erro/loading/disabled foi induzido em cada página.
4. **Teclado:** foco, entrada longa e fechamento exercitados em Login/Cadastro/Nova Rota. Captura do teclado do emulador mostrou região preta em Nova Rota; não foi possível atestar visualmente todas as teclas/ações Next/Go nem composição completa com IME aberto. Validar em Android físico.
5. **Responsividade:** concentrada em retrato, fonte padrão e 320 dp, com amostra a 411 dp. Paisagem, fontes ampliadas, outros recortes/barras de sistema e dispositivos físicos ainda precisam de homologação.
6. **APK instalado:** smoke usou instalação disponível, sem comprovação de seu SHA de origem. Build/testes validam a branch revisada; instalação controlada do APK final com configuração local de Maps deve integrar a homologação.
7. Assets oficiais continuam pendentes, sem necessidade de novo redesign estrutural.

## Próxima ação exata

- Homologar somente os cenários manuais pendentes e registrar resultados; não reiniciar Blocos 1–4 nem realizar outro redesign.
- Não declarar homologação integral das 13 páginas antes de completar esses cenários.
- Commit preparado para o fechamento documental: `docs(ui): registra revisão integrada e limites de validação`.
