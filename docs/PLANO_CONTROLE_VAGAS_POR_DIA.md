# UniRota — Controle de vagas por dia da semana e data

Estado em 15/10/2026: Blocos 0 e 1 aprovados; Bloco 2 integra criação, edição e visão semanal do motorista. P4 e P6 estão resolvidas conforme as decisões abaixo. P1, P2, P3 e P5 continuam pendentes. O inventário da seção 15 preserva a auditoria histórica do commit indicado; não representa as linhas atuais após implementação.

## 1. Problema do modelo atual e resultado da auditoria

Bloco 0: arquitetura e documentação, sem implementação funcional. Auditoria em 14/10/2026, sobre o commit `13103b7`, na branch `feature/corrige-vagas`, que já existia e estava ativa. O repositório estava limpo antes do trabalho. Este documento revisa e substitui o plano anteriormente chamado `docs/Plano_Controle_Vagas_Por_Dia.md`, sem manter cópia paralela.

O código atual ainda utiliza `WeeklyRoute.AvailableSeats` (`int?`) e o campo Firestore `availableSeats`. Existe um único saldo por rota, sem distinção de dia ou data. Embora o AGENTS.md já descreva o modelo aprovado, ele ainda não corresponde ao comportamento implementado.

Consequências verificadas no código:

- `MatchingService` exige saldo global positivo antes da interseção dos dias, sem disponibilidade individual por dia/data.
- `MatchResult` contém `DriverRoute` e `CompatibleDays`, mas não contém um saldo calculado para esses dias. A apresentação lê o saldo global de `DriverRoute`.
- `NewRouteViewModel` carrega, valida e salva o mesmo saldo no formulário de criação/edição; `WithCalculatedDistance` também o copia.
- `FirebaseRouteService` normaliza, serializa e desserializa o saldo global; a edição o inclui na máscara de atualização e usa precondição `updateTime`.
- `FirebaseRideRequestService.CreateAsync` verifica o saldo global no snapshot e incrementa `requestRevision` junto com a criação da solicitação. Criar uma solicitação não consome vaga.
- `AcceptAsync` usa o mesmo decremento global para Once e Weekly. O snapshot atual não carrega mapa de dias nem reservas por data. Ao chegar a zero, o serviço inclui a rejeição das demais pendências da rota no mesmo commit.
- O aceite atual já agrupa escritas com precondições de versão e até três tentativas com releitura. Essa proteção precisa ser preservada e adaptada, sem presumir que o cálculo global já atende ao novo modelo.
- `RideRequestRules` valida dias e data na criação, mas sua regra de consumo apenas subtrai uma vaga global.
- As Firestore Rules exigem o campo antigo e vinculam o aceite a um decremento global exato. O seed do emulador também grava o formato antigo.

Nenhum desses comportamentos é alterado no Bloco 0.

## 2. Objetivos e limites

Definir o contrato dos novos campos, suas representações, cálculos e invariantes; inventariar as dependências atuais; separar os próximos blocos e registrar decisões funcionais ainda necessárias.

Preservar .NET MAUI/.NET 8, MVVM, DI, Firebase, integração de mapas, matching geográfico, preço e snapshot de `SuggestedPrice`, autenticação e estados Pending/Accepted/Rejected.

No Bloco 0, somente este documento foi alterado. O Bloco 1 implementou modelos e serialização. O Bloco 2 modifica apenas criação/edição e sua apresentação ao motorista, mantendo matching, solicitações, aceite/recusa, Rules, Functions e concorrência de aceite para os blocos próprios.

## 3. Schema aprovado

Campos finais em `weeklyRoutes/{routeId}`, mantendo os demais campos existentes:

```json
{
  "role": "driver",
  "daysOfWeek": ["Monday", "Tuesday", "Wednesday"],
  "seatCapacity": 3,
  "weeklyAvailableSeats": {
    "Monday": 2,
    "Tuesday": 2,
    "Wednesday": 3
  },
  "oneTimeReservations": [
    {
      "requestId": "request-once-A",
      "date": "2026-10-20",
      "dayOfWeek": "Tuesday"
    }
  ]
}
```

Este exemplo representa uma Weekly aceita para Monday/Tuesday e uma Once aceita para 20/10/2026. Na criação, antes dos aceites, todos os três saldos seriam 3 e a lista seria vazia. Os exemplos mostram os valores lógicos, sem os wrappers da API REST do Firestore.

Contrato para passageiro, que não possui capacidade:

```json
{
  "role": "passenger",
  "seatCapacity": null,
  "weeklyAvailableSeats": {},
  "oneTimeReservations": []
}
```

Representar explicitamente a ausência de capacidade com `null`, mapa vazio e lista vazia; não atribuir capacidade zero a passageiros. Os novos campos devem ser gravados de forma consistente no novo formato, sem fallback para `availableSeats`.

## 4. Significado e tipos dos campos

| Campo persistido | Tipo Firestore | Representação C# futura | Significado |
| --- | --- | --- | --- |
| `seatCapacity` | integer para driver; null para passenger | `int? SeatCapacity` | Capacidade única informada pelo motorista; base comum aos dias selecionados. |
| `weeklyAvailableSeats` | map de nome do dia para integer | `IReadOnlyDictionary<DayOfWeek, int> WeeklyAvailableSeats` | Saldo recorrente de cada dia após ocupações semanais aceitas. |
| `oneTimeReservations` | array de maps | `IReadOnlyList<OneTimeReservation> OneTimeReservations` | Reservas pontuais aceitas, uma entrada por solicitação, na própria rota. |
| `requestId` da reserva | string não vazia | `string RequestId` | ID da RideRequest Once aceita; rastreabilidade e unicidade lógica. |
| `date` da reserva | string `yyyy-MM-dd` | `DateOnly Date` | Data civil exata, igual a `RequestedDate`; sem horário ou conversão para UTC. |
| `dayOfWeek` da reserva | string com nome de `DayOfWeek` | `DayOfWeek DayOfWeek` | Valor derivado de `Date.DayOfWeek`, nunca uma fonte independente. |

`OneTimeReservation` foi implementado no Bloco 1. Inteiros devem caber em `Int32` para leitura pelo aplicativo, ainda que o Firestore suporte inteiros maiores.

Chaves e valores de dias usam nomes canônicos de `System.DayOfWeek`: `Sunday`, `Monday`, `Tuesday`, `Wednesday`, `Thursday`, `Friday`, `Saturday`. Não usar rótulos traduzidos ou índices numéricos na persistência. Serializar a data em cultura invariável, como já ocorre com `requestedDate`.

Na API REST futura: inteiros usam `integerValue`; o mapa usa `mapValue.fields`; reservas usam `arrayValue.values` de `mapValue.fields`; data e nomes dos dias usam `stringValue`. Os DTOs atuais de valores dos dois serviços Firebase não possuem `mapValue` e precisarão ser adaptados no Bloco 1.

Não adicionar campo de capacidade, saldo ou reserva à RideRequest neste bloco. O modelo existente já tem `Type`, `CompatibleDays` e `DateOnly? RequestedDate` para identificar o compromisso.

## 5. Invariantes

1. Driver possui `seatCapacity > 0`; a UI continua recebendo uma única capacidade.
2. As chaves de `weeklyAvailableSeats` correspondem exatamente aos dias selecionados da rota, sem dias extras nem faltantes. Ausência de chave não significa capacidade zero válida.
3. Na criação, cada dia selecionado recebe exatamente `seatCapacity`, com lista de reservas vazia.
4. Cada saldo semanal é inteiro e está entre zero e `seatCapacity`.
5. Uma Weekly aceita consome uma vaga em cada dia da solicitação, uma única vez. Dias fora dela permanecem iguais.
6. Uma Once aceita acrescenta exatamente uma reserva; não altera os saldos semanais.
7. O mesmo `requestId` não pode aparecer duas vezes na lista da rota, mesmo com datas distintas. Retry não pode consumir novamente a mesma solicitação.
8. Cada reserva deve corresponder à RideRequest Once Accepted da mesma rota, com a mesma data; `dayOfWeek == date.DayOfWeek`, e o dia deve pertencer à rota.
9. Em toda data vigente afetada, as reservas pontuais não podem superar o saldo semanal daquele dia. Não aceitar saldo efetivo negativo nem ocultar inconsistência usando truncamento para zero.
10. Pending e Rejected não consomem vagas. O status Accepted e seu consumo correspondente são gravados atomicamente.
11. Passageiros usam capacidade nula, mapa vazio e lista vazia; não oferecem vagas.
12. Edição não reinicializa ocupações existentes nem sobrescreve reservas concorrentes. Mudanças que afetem compromissos exigem validação do estado atualizado.
13. Datas passadas não influenciam disponibilidade atual ou futura; continuam armazenadas até uma evolução específica de limpeza.
14. Não existe compatibilidade automática com o campo global antigo.

## 6. Disponibilidade semanal

Para um dia selecionado `d`:

```text
ocupacaoWeekly(d) = quantidade de solicitações Weekly aceitas contendo d
weeklyAvailableSeats[d] = seatCapacity - ocupacaoWeekly(d)
```

O mapa é o saldo persistido, atualizado atomicamente no aceite; a equação é uma invariante de consistência, não uma instrução para consultar todas as solicitações a cada matching.

```text
Inicial:                  Seg 3 / Ter 3 / Qua 3
Weekly Seg + Ter aceita:  Seg 2 / Ter 2 / Qua 3
```

Reservas Once não entram nessa subtração recorrente. Para um dia que não pertence à rota, não há oferta; não inventar uma entrada no mapa.

## 7. Disponibilidade por data exata

Para uma data civil `D` atual ou futura e pertencente aos dias da rota:

```text
reservasOnce(D) = quantidade de entradas únicas com date == D
vagasEfetivas(D) = weeklyAvailableSeats[D.DayOfWeek] - reservasOnce(D)
```

O cálculo usa somente reservas daquela data, sem somar reservas de outras datas do mesmo dia da semana. Entradas inválidas ou duplicadas violam o schema; a implementação deverá rejeitá-las, não tratá-las como vagas extras.

```text
Tuesday semanal = 2
20/10/2026: uma reserva  => 1 vaga efetiva
27/10/2026: zero reserva => 2 vagas efetivas
```

Uma data anterior à referência de hoje não é elegível para nova carona e suas reservas não entram na oferta vigente. A referência de calendário/fuso está pendente na seção 11.1. Não aplicar comparações entre instantes UTC a strings de data civil.

## 8. Solicitações Weekly

- `CompatibleDays` da solicitação representa todos os dias a consumir, sem duplicação; `RequestedDate` permanece nulo.
- A criação mantém a solicitação Pending e não reserva vagas.
- No aceite, reler a rota e a solicitação e recalcular a disponibilidade de todos os dias envolvidos.
- Aceitar tudo ou nada: se qualquer dia não tiver capacidade, nenhuma vaga ou status pode mudar.
- No sucesso, decrementar exatamente uma vaga por dia solicitado e gravar Accepted no mesmo commit atômico.
- Proteger compromissos Once já confirmados: o novo saldo semanal não pode produzir disponibilidade negativa em uma data vigente afetada pela recorrência.
- A mesma solicitação não pode ser aceita duas vezes. Concorrência deve impedir overbooking mesmo entre Weekly e Once.

O início temporal da recorrência e a política para conflitos com reservas futuras exigem decisão explícita antes da implementação correspondente, conforme seção 11.1. A existência de saldo semanal positivo isoladamente não prova que todas as datas afetadas comportam uma nova Weekly.

## 9. Solicitações Once

- `RequestedDate` é a fonte de verdade e deve ser uma data elegível compatível com a rota.
- Usar `RequestedDate.DayOfWeek`, nunca o primeiro item de `CompatibleDays`.
- `CompatibleDays` pode conter outros dias do match; a Once consome somente a data solicitada.
- No aceite, reler estado atualizado e exigir `vagasEfetivas(RequestedDate) >= 1`.
- No sucesso, adicionar `{ requestId, date, dayOfWeek }` e gravar Accepted atomicamente.
- `weeklyAvailableSeats` permanece igual. Outra data do mesmo dia da semana mantém seu saldo próprio.
- Não inserir novamente uma reserva cujo `requestId` já exista.

## 10. Matching e apresentação futura

Preservar os filtros geográficos, Place IDs, tolerância de horário, métricas de trajeto e ordenação existentes. A etapa de vagas deve:

1. Obter os dias compatíveis existentes entre as rotas.
2. Calcular a disponibilidade efetiva de cada dia no contexto temporal definido para a busca.
3. Eliminar os dias com disponibilidade zero; dados inválidos não são candidatos válidos.
4. Eliminar a rota se não restar nenhum dia.
5. Expor os dias restantes e o menor saldo entre eles como vagas restantes.

```text
Saldos aplicáveis:       Monday = 3 / Tuesday = 0 / Wednesday = 2
Compatibilidade inicial: Monday + Tuesday + Wednesday
Compatibilidade final:   Monday + Wednesday
Vagas restantes:         2
```

O futuro `MatchResult` deverá transportar o saldo calculado, sem a UI recalcular regras nem voltar a ler um valor global da rota. O nome da propriedade de resultado será definido no Bloco 4; ela é derivada, sem nova persistência.

**Pendência de contexto temporal:** hoje `FindMatchesAsync` recebe rotas semanais e não recebe uma data; a escolha Once/Weekly e de `RequestedDate` ocorre depois, em `RideRequestViewModel`. Não é possível selecionar reservas por data apenas com `DayOfWeek`. A regra de filtragem/mínimo está aprovada, mas a data ou horizonte a usar nessa busca deve ser decidido antes dos Blocos 3–4. Não assumir silenciosamente próxima ocorrência, semana atual ou mínimo de todas as datas futuras. Um resultado de busca também não garante disponibilidade até o aceite.

## 11. Edição e decisões arquiteturais pendentes

O motorista continua informando uma única capacidade na UI.

- Aumentar capacidade é permitido, preservando ocupações confirmadas.
- Reduzir capacidade é permitido somente quando nenhuma ocupação Weekly ou Weekly + Once ultrapassa a nova capacidade. Para edição no Bloco 2, todas as reservas armazenadas, inclusive históricas, participam da validação estrutural; não utilizar `DateTime.Today` para descartá-las.
- Para dias mantidos, preservar a ocupação: conceitualmente `novoSaldo[d] = novaCapacidade - ocupacaoWeekly(d)`, e validar as reservas por data afetadas.
- Adicionar um dia inicializa seu saldo com a capacidade única vigente.
- Remover um dia é permitido somente quando não há ocupação Weekly naquele dia nem qualquer reserva Once com esse `DayOfWeek`, inclusive histórica. A remoção com compromisso é bloqueada, sem apagar reservas.
- Não restaurar saldos ao simplesmente carregar/salvar o formulário. Conservar as proteções de versão entre edição e aceite.
- Role é imutável após criação, tanto para Driver quanto para Passenger, com proteção na UI, no ViewModel e no serviço.
- `FirebaseRouteService.UpdateAsync` relê o documento e usa essa rota como fonte de verdade para Role, capacidade anterior, saldos e reservas. Calcula `weeklyOccupied = oldSeatCapacity - oldWeeklyAvailableSeats[day]` e `newAvailable = newSeatCapacity - weeklyOccupied` para dias mantidos; dias novos recebem a nova capacidade.
- Para cada data armazenada, exige `newSeatCapacity >= weeklyOccupied[date.DayOfWeek] + quantidade de Once daquela data`. Reservas de datas distintas não se acumulam entre si.
- Toda edição válida preserva exatamente `OneTimeReservations`, além de metadados e `requestRevision`. Mantém a precondição `updateTime` existente e não adiciona retry.

### Visão semanal do motorista — P6 resolvida

Cada card de rota Driver em Minhas Rotas exibe sete datas consecutivas, **domingo a sábado**, com intervalo e botões de semana anterior/próxima. A ordem foi confirmada explicitamente pelo usuário. A semana inicial contém a data local do dispositivo apenas para navegação visual; isso não resolve P3 nem fornece relógio para matching ou aceite.

Para cada data `D` da semana exibida:

```text
dia não oferecido => "-"
dia oferecido => (SeatCapacity - WeeklyAvailableSeats[D.DayOfWeek])
                + quantidade de OneTimeReservations com Date == D
```

O número representa passageiros programados, não vagas restantes. `0` identifica dia oferecido sem passageiros. Uma Once aparece somente na semana que contém sua data exata; não afeta semanas anteriores/posteriores. Reservas históricas continuam visíveis quando sua própria semana é selecionada. A apresentação usa o estado recorrente atual da rota; não cria histórico adicional de ocupação Weekly.

Exemplo: semana 25/10/2026–31/10/2026; Thursday com capacidade 3 e saldo semanal 2 tem 1 Weekly; uma Once em 29/10/2026 faz o dia exibir `2`. A grade e a navegação são exclusivas do motorista; Passenger e MatchResults não mudam neste bloco. Não adotar resumo de intervalo de vagas como `0–2 de 3`.

### 11.1. Pendências que exigem decisão antes do bloco afetado

| ID | Ambiguidade observada | Decisão necessária / bloco afetado |
| --- | --- | --- |
| P1 | Matching semanal não possui data nem tipo de solicitação no momento da busca. | Definir data/horizonte e como reservas Once afetam cada dia no resultado. Bloqueia integração nos Blocos 3–4. |
| P2 | Weekly não tem início/fim de vigência persistidos; uma Once futura pode ocupar a última vaga numa ocorrência. | Definir início da recorrência e política de conflito para nova Weekly sem invalidar reservas aceitas. Não inferir aceite só do mapa semanal. Blocos 3 e 6. |
| P3 | Serviços usam `DateTime.Today`, dependente do dispositivo. | Definir calendário/fuso de referência e origem confiável de hoje, incluindo virada de dia e validação de aceite de Once vencida. Blocos 3, 5–7. |
| P4 — RESOLVIDA | Edição preserva ocupações e reservas; Role é imutável. | Redução permitida somente sem sobreocupação Weekly ou Weekly + Once em qualquer data armazenada, inclusive histórica. Dia com Weekly ou qualquer Once não pode ser removido. Serviço usa estado relido e `updateTime`; nenhuma restauração/cancelamento implícitos. Bloco 2. |
| P5 | Atualmente saldo global zero rejeita todas as outras pendências da rota. Isso não distingue dias nem datas. | Definir se e quais pendências serão rejeitadas quando um dia/data lotar, preservando solicitações ainda viáveis; incluir pedidos Weekly parcialmente inviáveis. Bloco 6. |
| P6 — RESOLVIDA | Visão semanal do motorista por data, domingo a sábado. | Exibir Weekly confirmados do dia + Once da data exata. `-` para dia não oferecido, `0` para oferecido sem passageiros; Once somente na sua semana. Navegação anterior/próxima e referência local inicial exclusivamente visual. Bloco 2. |

P4 e P6 foram aprovadas e implementadas no Bloco 2. P1, P2, P3 e P5 seguem sem definição e não são resolvidas implicitamente pela validação estrutural da edição ou pela referência visual de semana. Não iniciar os blocos seguintes automaticamente.

## 12. Concorrência e atomicidade

A referência atual é o commit REST com `currentDocument.updateTime` nas escritas de rota e solicitação e releitura em conflitos. A técnica final será adaptada no Bloco 6, mantendo consistência verificável nas Rules do Bloco 7.

Requisitos obrigatórios:

- Revalidar status Pending, propriedade, dias, data, saldo e unicidade do `requestId` no estado usado pelo aceite.
- Escrever o consumo e Accepted como uma única operação atômica; falha em qualquer precondição deixa todos os documentos inalterados.
- Em conflito, reler documentos e recalcular; não reaplicar um saldo ou uma lista calculados sobre versão antiga.
- Serializar disputas Weekly/Weekly, Once/Once na mesma data e Weekly/Once. Datas distintas não compartilham consumo pontual, embora modifiquem o mesmo documento.
- Impedir saldo negativo, duplicação lógica, Accepted sem consumo correspondente e consumo sem Accepted.
- Coordenar edição com aceite para impedir perda de reservas e violação de capacidade.
- Preservar a finalidade de `requestRevision` no controle de criação de pendências; ele não é um saldo nem substitui a validação de disponibilidade.
- As Rules futuras precisam validar as mudanças autorizadas e sua relação com a solicitação. Precondição de versão isolada não prova correção do cliente.

Não alterar estratégias, limites de retry, Rules ou Functions neste bloco. Não criar backend novo. Limitações da prova entre documentos nas Rules devem ser verificadas no Bloco 7 antes de publicar qualquer mudança de segurança.

## 13. Reservas passadas

Manter registros antigos em `oneTimeReservations` para rastreabilidade, ignorando-os na oferta atual/futura conforme a política temporal ainda a definir. Exceções explicitamente aprovadas no Bloco 2: todas as datas armazenadas protegem redução de capacidade e remoção de dias; a visão semanal mostra reservas históricas somente quando a semana da data exata é selecionada. Não deduzir uma reserva antiga de todas as ocorrências do mesmo dia da semana. Hoje não equivale a passado: uma reserva da data atual ainda ocupa essa data conforme a referência temporal a definir.

Não implementar limpeza no cliente nem Firebase Function de remoção automática. Essa possível evolução permanece fora dos Blocos 0–8 aqui definidos. O crescimento da lista deverá ser acompanhado futuramente sem introduzir limpeza neste trabalho.

## 14. Ausência de migração e compatibilidade

O ambiente de teste é descartável e será recriado após implementação e validação. Não haverá conversão automática, dupla leitura, fallback ou inferência de `seatCapacity` a partir de `availableSeats`.

No formato novo, documento de motorista com campos obrigatórios ausentes ou inválidos deve ser rejeitado como inválido. Não fabricar um mapa de saldos a partir de documentos antigos. A remoção definitiva dos leitores/escritores antigos deve ocorrer coordenada com seus consumidores, sem ampliar esta regra para refatorações de outros campos.

Fixtures e `scripts/start-emulators-seeded.js` precisarão produzir o schema novo. A recriação do ambiente e eventual descarte de usuários/rotas/solicitações pertencem à transição futura; nenhum dado é apagado agora. Não executar o seed durante o Bloco 0.

## 15. Inventário de dependências de AvailableSeats

### 15.1. Método e contagem reproduzível

Busca textual no código-fonte disponível, respeitando os ignores do repositório, incluindo arquivos ocultos e excluindo `.git`, `bin`, `obj`, `node_modules` e o próprio plano (em ambos os nomes). A contagem inclui declarações, parâmetros, nomes derivados, mensagens e comentário das Rules, além de leituras/escritas; não representa quantidade de chamadas em execução. Código gerado ignorado não é contado.

```powershell
rg -n --hidden -g '!.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/node_modules/**' -g '!docs/Plano_Controle_Vagas_Por_Dia.md' -g '!docs/PLANO_CONTROLE_VAGAS_POR_DIA.md' 'AvailableSeats' .
rg -n -i --hidden -g '!.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/node_modules/**' -g '!docs/Plano_Controle_Vagas_Por_Dia.md' -g '!docs/PLANO_CONTROLE_VAGAS_POR_DIA.md' 'availableseats' .
```

- Busca sensível à caixa, substring `AvailableSeats`: **44 ocorrências em 41 linhas**.
- Busca ampliada sem distinguir caixa, incluindo `availableSeats`: **114 ocorrências em 104 linhas de 21 arquivos**.
- Para contar ocorrências em vez de linhas, usar `rg -o` com os mesmos filtros; uma linha pode conter mais de uma ocorrência. As duas buscas se sobrepõem e não devem ser somadas.

### 15.2. Classificação de todas as linhas da busca ampliada

Caminhos relativos à raiz e números de linha referentes ao commit auditado. Cada grupo abaixo recebe uma categoria principal; os efeitos indiretos são descritos na coluna final. As linhas listadas cobrem as 104 linhas encontradas.

| Arquivo | Linhas | Categoria | Dependência atual |
| --- | --- | --- | --- |
| `UniRota/Models/WeeklyRoute.cs` | 25 | persistência | Declara o saldo global nullable compartilhado por driver/passenger. |
| `UniRota/Services/MatchingService.cs` | 350 | matching | Lê o saldo para excluir a rota inteira. |
| `UniRota/Services/Firebase/FirebaseRouteService.cs` | 302, 334, 335 | criação/edição | Valida motorista e normaliza/copía saldo ou null para passageiro. |
| `UniRota/Services/Firebase/FirebaseRouteService.cs` | 385, 388, 435, 440, 442, 446, 469, 943 | persistência | Escreve inteiro/null; lê/valida campo; popula o modelo; inclui campo na máscara de edição. |
| `UniRota/Services/Firebase/FirebaseRideRequestService.cs` | 812 | RideRequest | Revalida saldo global no snapshot durante criação da pendência. |
| `UniRota/Services/Firebase/FirebaseRideRequestService.cs` | 398, 422, 908, 910, 919, 921, 928 | aceite | Calcula saldo e escreve decremento global com máscara e precondição. |
| `UniRota/Services/Firebase/FirebaseRideRequestService.cs` | 1076, 1078, 1085, 1751 | persistência | Desserializa o saldo no snapshot privado usado na criação e no aceite. |
| `UniRota/Services/RideRequestRules.cs` | 16, 18, 24 | aceite | Parâmetro, validação e decremento de uma vaga global. |
| `UniRota/ViewModels/NewRouteViewModel.cs` | 60, 174, 220, 221, 470, 486, 487, 504, 740 | criação/edição | Campo gerador da propriedade observável; limpa, carrega, valida, atribui e copia saldo. |
| `UniRota/ViewModels/MatchResultsViewModel.cs` | 216, 217, 218 | UI | `MatchResultItemViewModel` formata saldo de `Match.DriverRoute`. |
| `UniRota/ViewModels/MyRoutesViewModel.cs` | 176, 177, 179, 180 | UI | Visibilidade e texto do badge global em `WeeklyRouteItemViewModel`. |
| `UniRota/ViewModels/RoutePresentationText.cs` | 118, 120, 123 | UI | Formata zero/null, singular e plural sem contexto temporal. |
| `UniRota/Views/Routes/NewRoutePage.xaml` | 102 | UI | Binding TwoWay do único campo de vagas. |
| `UniRota/Views/Routes/MyRoutesPage.xaml` | 45 | UI | Bindings do texto e visibilidade do badge. |
| `UniRota/Views/Matching/MatchResultsPage.xaml` | 37 | UI | Binding do badge nos resultados. |
| `UniRota.Tests/FirebaseRouteServiceTests.cs` | 128, 158 | teste | Objeto e JSON global usados nos testes de criação, edição e leitura. |
| `UniRota.Tests/FirebaseRideRequestServicePricingTests.cs` | 165, 302, 309, 317 | teste | Asserção do decremento para zero, objeto e fixture JSON/parametrização de saldo. |
| `UniRota.Tests/MatchingServiceTests.cs` | 68, 599, 755, 770, 785 | teste | Filtro global sem vagas, candidatos inválidos e fábricas de rotas. |
| `UniRota.Tests/NewRouteViewModelTests.cs` | 463, 548 | teste | Fixtures/fake de persistência usadas por criação, edição e apresentação. |
| `UniRota.Tests/RideRequestViewModelTests.cs` | 191, 235 | teste | Fixtures de match/driver para submissão Once/Weekly e preço. |
| `UniRota.Tests/RouteMapTests.cs` | 176 | teste | Fixture de motorista; dependência estrutural, sem alterar mapa. |
| `UniRota.Tests/RideRequestRulesTests.cs` | 106, 111, 118, 119, 122 | teste | Decremento global e rejeição de saldo nulo/zero/negativo; inclui nome de teste derivado. |
| `firestore.rules` | 250, 300, 301, 312, 373 | persistência | Allowlist/schema driver/passenger e campos editáveis pelo proprietário. |
| `firestore.rules` | 401, 402, 643, 644 | RideRequest | Exige saldo global na criação e no incremento técnico de revisão. |
| `firestore.rules` | 422, 435, 436, 438, 439, 445, 448, 449, 776, 777, 783, 786, 788, 789, 791 | aceite | Comentário e verificações do decremento técnico e da relação com Accepted. |
| `scripts/start-emulators-seeded.js` | 131, 146, 161, 176, 193, 208, 223, 238, 348 | outro | Quatro drivers, quatro passageiros e escrita dos dados de teste no schema antigo. |

### 15.3. Dependências auditadas sem ocorrência literal e cobertura atual

- `UniRota/Models/MatchResult.cs`: transporta rota e dias; não possui disponibilidade derivada. Ajuste futuro deve preservar métricas geográficas e polyline.
- `UniRota/Models/RideRequest.cs`: já contém `CompatibleDays`, `Type`, `RequestedDate` e status, sem campo de vagas. A suficiência temporal da Weekly é P2.
- `RideRequestRules.ValidateForCreation`: já normaliza dias, exige data para Once, rejeita passado e dia incompatível e exige data nula para Weekly. Não calcula saldo por dia/data.
- `UniRota/ViewModels/RideRequestViewModel.cs`: recebe `MatchResult`, exibe dias, escolhe tipo/data depois do matching e envia contexto e preço. Não exibe saldo diretamente, mas depende dos dias filtrados futuros.
- `FirebaseRideRequestService.AcceptAsync`, linhas 425–435: rejeição automática das pendências quando `remainingSeats == 0`, sem ocorrência literal do campo nessas linhas; revisar em P5.
- `MatchingServiceTests.FindMatchesAsync_NoSeatsDoesNotCallMapService` testa null/zero/negativo globais; não prova filtragem parcial por dia ou por data.
- `RideRequestRulesTests` testa validação Once/Weekly, estado Pending e decremento global, sem o novo mapa ou reservas.
- `FirebaseRideRequestServicePricingTests` usa HTTP simulado: comprova escritas esperadas, precondições e preservação do preço; não comprova concorrência real no Firestore. O teste de última vaga fixa a rejeição automática global e deverá ser revisto após P5.
- `FirebaseRouteServiceTests` cobre Place IDs em criação/edição/leitura com fixture global, sem validar ainda os novos campos. A tolerância histórica a Place IDs ausentes não deve virar fallback de vagas.
- `NewRouteViewModelTests`, `RideRequestViewModelTests` e `RouteMapTests` precisarão de fixtures novas, preservando seus objetivos existentes de endereços, distância, preço e mapa.
- Nenhuma ocorrência direta de `AvailableSeats`/`availableSeats` foi encontrada em `functions`. Nenhuma Function foi alterada ou executada.

## 16. Divisão dos blocos 1–8

Plano incremental: Blocos 1 e 2 implementados nos respectivos commits. Não iniciar o Bloco 3 automaticamente. Cada bloco exige escopo próprio, commit separado e validação pertinente.

| Bloco | Entrega prevista | Critério de conclusão / dependências |
| --- | --- | --- |
| 1 — Modelo persistido e serialização | Alterar `WeeklyRoute`, definir tipo de reserva e DTOs REST, ler/escrever/validar o novo schema; sem fallback. | Testes de round-trip, driver/passenger e dados inválidos. Coordenar consumidores para manter compilação; não antecipar matching, aceite ou Rules. Não deixar adaptadores temporários como compatibilidade permanente. |
| 2 — Criação, edição e visão semanal | Uma capacidade inicial replicada por dia; preservação de ocupações e reservas ao editar; dias novos; Role imutável; passageiros por data na semana do motorista. | P4 e P6 resolvidas: redução sem sobreocupação, remoção somente sem compromissos, inclusive históricos; visão domingo–sábado com Weekly + Once da data, `-`/`0` e navegação semanal. |
| 3 — Cálculo de disponibilidade | Lógica pura e testável para saldo semanal, data exata, reservas passadas e elegibilidade de aceites. | Resolver P1–P3 conforme os cálculos envolvidos; testar datas distintas do mesmo dia, duplicação e Weekly/Once. Evitar camadas/dependências desnecessárias. |
| 4 — Matching e apresentação | Filtrar dias, eliminar rota vazia e transportar/exibir o menor saldo em `MatchResult`. | P1 decidida; P6 já implementada no Bloco 2. Manter filtros geográficos, ordenação, preço e mapas; testar match parcial e lotação. |
| 5 — Criação de RideRequests | Garantir dias Weekly e data Once válidos no contexto atual sem consumo na criação. | Preservar snapshot de preço e controle de duplicação/revisão; P3 resolvida; testes de datas e dias. |
| 6 — Aceite e concorrência | Consumo Weekly tudo-ou-nada, reserva Once por data, idempotência e escrita atômica. | Resolver P2/P3/P5; testes de conflito, última vaga e edição concorrente. Validar política de pendências sem rejeições globais indevidas. |
| 7 — Firestore Rules e integração | Validar novo schema, permissões e vínculos entre aceite e consumo; validar com emulador. | Cobrir adulteração, Weekly/Weekly, Once/Once e Weekly/Once; mesma data e datas distintas; preservação de preço e rejeição sem consumo. Nenhuma publicação automática. |
| 8 — Encerramento e dados de teste | Remover resíduos antigos, atualizar seed/fixtures/documentação, preparar recriação controlada do ambiente e checklist manual. | Suíte completa pertinente e Android Release aprovados; teste manual de criação, edição, matching, Once/Weekly, caronas confirmadas e banco. Descarte/recriação e eventual PR dependem da autorização daquele bloco; sem push/merge automático. |

A ordem é incremental. Se a dependência entre modelos e consumidores exigir compatibilização para compilar, explicitar a menor adaptação no escopo do bloco, sem implementar regras funcionais não autorizadas. Funções de limpeza, cancelamento/restauração, capacidade configurável por dia, chat, pagamento, reputação e nova precificação permanecem fora deste plano.

## 17. Definition of Done do Bloco 0

- [x] AGENTS.md lido; branch e estado do repositório verificados antes de alterações.
- [x] Branch solicitada já existente e ativa identificada; ausência de alterações locais pendentes confirmada.
- [x] Plano existente revisado e consolidado no caminho solicitado, sem documento duplicado.
- [x] Modelo atual e leitores/escritores diretos e indiretos auditados, classificados e contados.
- [x] Nomes, tipos conceituais, representação persistida e invariantes do schema documentados.
- [x] Cálculos semanal/por data, Weekly, Once, matching, edição e concorrência documentados.
- [x] Ausência de migração e de limpeza automática documentada.
- [x] Ambiguidades registradas explicitamente com os blocos que dependem delas; nenhuma política adicional implementada.
- [x] Divisão dos próximos blocos registrada; nenhum bloco funcional iniciado.
- [x] Nenhum `.cs`, XAML, teste, Rule, script ou arquivo de Functions alterado; nenhum record/tipo criado.

Validação de entrega: `git diff --check` aprovado. O inventário foi comparado automaticamente com a busca: 104 linhas reais, 104 linhas documentadas, nenhuma divergência. Conferir também o diff staged antes do commit único `docs(vagas): define novo modelo de disponibilidade`.

Como somente documentação mudou, build Android, testes .NET, testes Node e teste manual funcional não são necessários neste bloco. Não há novo resultado de build ou suíte a declarar.

O commit encerra apenas o Bloco 0, sem push, merge, publicação, limpeza de dados ou início automático de outra pendência.
