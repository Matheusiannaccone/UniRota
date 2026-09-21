# UniRota — Fluxo técnico do MVP

## 1. Objetivo

O UniRota é um aplicativo acadêmico de caronas para estudantes da Facens. O
fluxo funcional atual é:

```text
Cadastro/Login
    ↓
Rotas semanais com endereços reais
    ↓
Matching geográfico por desvio
    ↓
Solicitação Once/Weekly e preço sugerido
    ↓
Aceite/rejeição e consumo de vagas
    ↓
Rotas confirmadas
```

O aplicativo é mobile-first, com Android como plataforma principal já validada.
iOS permanece como target estrutural, mas exige validação em macOS/Xcode.

## 2. Arquitetura

| Parte | Implementação atual |
|---|---|
| Aplicativo | .NET MAUI 8, C#, XAML e MVVM simples |
| Injeção de dependência | Microsoft.Extensions.DependencyInjection |
| Autenticação | Firebase Authentication por REST |
| Dados | Cloud Firestore por REST |
| Endereços | Places API (New), protegida por callable autenticada |
| Rotas | Routes API, protegida por callable autenticada |
| Mapa | Microsoft.Maui.Controls.Maps / Maps SDK for Android |
| Matching | Regras determinísticas em C# |
| Precificação | Fórmula determinística em C# |
| Backend auxiliar | Firebase Functions mínimas e específicas para Google Maps Platform |

As integrações Google ficam isoladas por `IPlaceService` e
`IMapRouteService`. ViewModels e modelos de domínio não conhecem endpoints,
headers, chaves nem DTOs do Google.

## 3. Autenticação e dados

O usuário cria conta ou entra com e-mail e senha. O refresh token é mantido em
`SecureStorage`; o ID token válido acompanha operações autenticadas no
Firestore e nas callable functions.

As coleções funcionais são:

- `users`: perfil básico;
- `weeklyRoutes`: rotas de motorista e passageiro;
- `rideRequests`: solicitações, status e snapshot de preço.

Não existem coleções de cache geográfico, matches ou polylines. Resultados de
matching e geometria permanecem apenas em memória.

## 4. Rotas semanais e endereços

Origem e destino são selecionados pelo autocomplete da Places API (New). Uma
rota salva contém o texto apresentado ao usuário e os respectivos Place IDs.

O autocomplete usa:

- mínimo de 3 caracteres;
- debounce de 350 ms;
- cancelamento e versão da consulta para ignorar respostas obsoletas;
- sessões independentes para origem e destino;
- encerramento da sessão por Place Details após a seleção.

Editar ou limpar o texto depois de uma seleção invalida o Place ID. Rotas
novas e rotas editadas só podem ser salvas após uma seleção válida.

Documentos legados sem Place IDs continuam legíveis e aparecem no gerenciamento
de rotas. Eles não são geocodificados automaticamente e não participam do
matching até serem editados e salvos com endereços selecionados.

Ao salvar uma rota de motorista, a Routes API calcula a distância real e
`EstimatedDistanceKm` recebe o valor em quilômetros. Rotas de passageiro
continuam com distância zero. Não há entrada manual de distância.

## 5. Matching geográfico

O matching aplica primeiro os filtros locais:

1. candidato motorista;
2. usuário diferente;
3. vaga disponível;
4. ao menos um dia em comum;
5. diferença de horário de até 30 minutos;
6. Place IDs válidos.

Somente os candidatos sobreviventes geram chamadas de rota. Para cada um, o
aplicativo compara:

```text
rota base: motorista origem → motorista destino

rota compartilhada: motorista origem
                  → passageiro origem
                  → passageiro destino
                  → motorista destino
```

Waypoints consecutivos repetidos são removidos. A execução mantém cache em
memória para não recalcular uma combinação idêntica. O processamento é
sequencial para limitar chamadas faturáveis; uma falha específica elimina
somente o candidato, enquanto falhas globais de autenticação, quota ou
configuração interrompem a busca.

Os limites centralizados em `MatchingOptions` são:

- tolerância de horário: 30 minutos;
- desvio máximo de distância: 5 km;
- desvio máximo de duração: 15 minutos.

Ambos os limites de desvio precisam ser respeitados. Diferenças negativas são
normalizadas para zero. A ordenação é determinística por menor desvio de tempo,
menor desvio de distância, menor diferença de horário, mais dias compatíveis,
horário de saída e ID da rota.

## 6. Precificação e solicitações

A fórmula permanece:

```text
distância compartilhada × 0,53 ÷ 2
```

`MatchResult.SharedDistanceKm` alimenta o cálculo. O valor arredondado exibido é
persistido em `RideRequest.SuggestedPrice` como snapshot; aceite, rejeição e
telas posteriores não recalculam o preço nem chamam Routes novamente.

Solicitações preservam os tipos `Once` e `Weekly` e os status `Pending`,
`Accepted` e `Rejected`. O aceite usa transação REST com preconditions de
`updateTime`, `requestRevision` e tentativas limitadas para consumir a vaga sem
overbooking. Quando a última vaga é ocupada, solicitações concorrentes pendentes
da rota são rejeitadas na mesma operação.

## 7. Visualização do trajeto

`computeRoute` retorna distância, duração e a polyline overview codificada na
mesma chamada usada pelo matching. O FieldMask é:

```text
routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline
```

`RouteDetailsPage` decodifica a polyline no aplicativo, calcula o viewport e
desenha o trajeto. `placeCoordinates` consulta somente `location` para até
quatro Place IDs e é usada apenas ao abrir os detalhes, para posicionar os pins
exatos. Place IDs repetidos geram um único pin com rótulos combinados.

Abrir o mapa não chama Routes, não refaz matching, não cria solicitação e não
altera preço ou vagas. O mapa não usa GPS, localização atual, tracking ou
navegação turn-by-turn.

## 8. Chaves e configuração

- `GOOGLE_PLACES_API_KEY`: Secret Manager, restrita à Places API (New);
- `GOOGLE_ROUTES_API_KEY`: Secret Manager, restrita à Routes API;
- `GOOGLE_MAPS_ANDROID_API_KEY`: injetada no build e restrita ao aplicativo
  Android e ao Maps SDK for Android.

O ApplicationId definitivo é `io.github.matheusiannaccone.unirota`. A chave
Android deve ser restrita por esse package name e pelos SHA-1 dos certificados
de debug e release aplicáveis. Consulte `GooglePlacesSetup.md` para os comandos
e passos manuais.

## 9. Validação e fechamento

Cada alteração deve manter:

- testes unitários sem acesso real ao Google;
- testes Node das Functions;
- `npm audit` sem vulnerabilidades conhecidas;
- `git diff --check` limpo;
- build Android `net8.0-android` sem erros ou avisos;
- ausência de chaves de servidor, tokens e credenciais no Git.

Firestore Security Rules não fazem parte do fechamento deste incremento. Sua
revisão e validação serão realizadas separadamente antes de usuários reais.

## 10. Fora do escopo atual

- GPS e localização atual;
- tracking ou compartilhamento em tempo real;
- navegação turn-by-turn;
- chat, pagamento e notificações;
- cache geográfico persistente;
- otimização de múltiplos passageiros;
- nova fórmula de preço;
- refatoração arquitetural ampla.
