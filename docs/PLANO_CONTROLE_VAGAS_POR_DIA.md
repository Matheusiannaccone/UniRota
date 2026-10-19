# UniRota — Controle de vagas por dia da semana e data

Atualização documental em 19/10/2026: todas as decisões P1–P6 estão RESOLVIDAS. O plano incorpora matching simplificado, vigência Weekly a partir do aceite, autoridade temporal do servidor e aceite parcial por `AcceptedDays`. Blocos 1 e 2 mantêm seu histórico; a implementação das decisões finais segue o planejamento das seções seguintes. Esta atualização não altera código, testes, Rules ou seed. As seções 1 e 15 preservam a auditoria histórica do commit indicado, e a seção 17 registra a entrega original do Bloco 0; esses registros não descrevem o estado atual do código.

## 1. Problema do modelo global — auditoria histórica do Bloco 0

Bloco 0: arquitetura e documentação, sem implementação funcional. Auditoria em 14/10/2026, sobre o commit `13103b7`, na branch `feature/corrige-vagas`, que já existia e estava ativa. O repositório estava limpo antes do trabalho. Este documento revisa e substitui o plano anteriormente chamado `docs/Plano_Controle_Vagas_Por_Dia.md`, sem manter cópia paralela.

No estado auditado em 14/10/2026, o código utilizava `WeeklyRoute.AvailableSeats` (`int?`) e o campo Firestore `availableSeats`. Existe um único saldo por rota, sem distinção de dia ou data. Embora o AGENTS.md já descreva o modelo aprovado, ele ainda não corresponde ao comportamento implementado.

Consequências verificadas naquele estado histórico do código:

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

Definir o contrato dos campos, suas representações, cálculos e invariantes; preservar o inventário histórico; consolidar as decisões finais P1–P6 e organizar os blocos de implementação restantes. Decisão resolvida não significa implementação concluída.

Preservar .NET MAUI/.NET 8, MVVM, DI, Firebase, integração de mapas, matching geográfico, preço e snapshot de `SuggestedPrice`, autenticação e estados Pending/Accepted/Rejected.
    
No Bloco 0, somente este documento foi alterado. O Bloco 1 implementou modelos e serialização. O Bloco 2 tratou de criação/edição e sua apresentação ao motorista. O planejamento restante consolida disponibilidade e referência temporal, aceite parcial, concorrência, Rules, integração, seed e visão do passageiro. Não planejar mudança funcional do matching neste ciclo.

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

Este exemplo representa uma Weekly com `AcceptedDays = [Monday, Tuesday]` e uma Once aceita para 20/10/2026. Na criação, antes dos aceites, todos os três saldos seriam 3 e a lista seria vazia. Os exemplos mostram os valores lógicos, sem os wrappers da API REST do Firestore.

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

| Campo persistido | Tipo Firestore | Representação C# / contrato final | Significado |
| --- | --- | --- | --- |
| `seatCapacity` | integer para driver; null para passenger | `int? SeatCapacity` | Capacidade única informada pelo motorista; base comum aos dias selecionados. |
| `weeklyAvailableSeats` | map de nome do dia para integer | `IReadOnlyDictionary<DayOfWeek, int> WeeklyAvailableSeats` | Saldo recorrente de cada dia após ocupações semanais aceitas. |
| `oneTimeReservations` | array de maps | `IReadOnlyList<OneTimeReservation> OneTimeReservations` | Reservas pontuais aceitas, uma entrada por solicitação, na própria rota. |
| `requestId` da reserva | string não vazia | `string RequestId` | ID da RideRequest Once aceita; rastreabilidade e unicidade lógica. |
| `date` da reserva | string `yyyy-MM-dd` | `DateOnly Date` | Data civil exata, igual a `RequestedDate`; sem horário ou conversão para UTC. |
| `dayOfWeek` da reserva | string com nome de `DayOfWeek` | `DayOfWeek DayOfWeek` | Valor derivado de `Date.DayOfWeek`, nunca uma fonte independente. |

`OneTimeReservation` foi implementado no Bloco 1. Inteiros devem caber em `Int32` para leitura pelo aplicativo, ainda que o Firestore suporte inteiros maiores.

Chaves e valores de dias usam nomes canônicos de `System.DayOfWeek`: `Sunday`, `Monday`, `Tuesday`, `Wednesday`, `Thursday`, `Friday`, `Saturday`. Não usar rótulos traduzidos ou índices numéricos na persistência. Serializar a data em cultura invariável, como já ocorre com `requestedDate`.

Na API REST: inteiros usam `integerValue`; o mapa usa `mapValue.fields`; reservas usam `arrayValue.values` de `mapValue.fields`; data e nomes dos dias usam `stringValue`. O suporte a esses wrappers pertence ao schema dos serviços Firebase, sem fallback para o saldo global antigo.

### Contrato final de RideRequest — AcceptedDays (P5)

Planejar no Bloco 5 o novo campo, sem implementar nesta atualização documental:

| Campo persistido | Tipo Firestore | Tipo conceitual C# | Significado |
| --- | --- | --- | --- |
| `acceptedDays` | array de nomes canônicos de `DayOfWeek` | `IReadOnlyList<DayOfWeek> AcceptedDays` | Dias efetivamente concedidos no aceite Weekly. |

`CompatibleDays` preserva os dias originalmente compatíveis/solicitados e nunca é sobrescrito pelo aceite parcial. `AcceptedDays` representa apenas os dias concedidos, sem duplicação, dentro da mesma relação/solicitação.

- Pending: `AcceptedDays = []`.
- Rejected: `AcceptedDays = []`.
- Weekly Accepted: `AcceptedDays` contém exatamente os dias concedidos e tem ao menos um elemento.
- Once, em qualquer status: `AcceptedDays = []`; `RequestedDate` continua representando o compromisso pontual.

Exemplo lógico de Weekly aceita parcialmente:

```json
{
  "type": "weekly",
  "status": "accepted",
  "compatibleDays": ["Monday", "Tuesday", "Wednesday"],
  "acceptedDays": ["Tuesday", "Wednesday"],
  "requestedDate": null
}
```

Não adicionar capacidade, saldo ou reserva à RideRequest. O novo campo registra a concessão de dias, sem duplicar o saldo da rota.

## 5. Invariantes

1. Driver possui `seatCapacity > 0`; a UI continua recebendo uma única capacidade.
2. As chaves de `weeklyAvailableSeats` correspondem exatamente aos dias selecionados da rota, sem dias extras nem faltantes. Ausência de chave não significa capacidade zero válida.
3. Na criação, cada dia selecionado recebe exatamente `seatCapacity`, com lista de reservas vazia.
4. Cada saldo semanal é inteiro e está entre zero e `seatCapacity`.
5. Uma Weekly aceita consome uma vaga em cada dia efetivamente aceito, registrado em `AcceptedDays`, uma única vez. Os demais dias permanecem iguais.
6. Uma Once aceita acrescenta exatamente uma reserva; não altera os saldos semanais.
7. O mesmo `requestId` não pode aparecer duas vezes na lista da rota, mesmo com datas distintas. Retry não pode consumir novamente a mesma solicitação.
8. Cada reserva deve corresponder à RideRequest Once Accepted da mesma rota, com a mesma data; `dayOfWeek == date.DayOfWeek`, e o dia deve pertencer à rota.
9. Em toda data vigente afetada, as reservas pontuais não podem superar o saldo semanal daquele dia. Não aceitar saldo efetivo negativo nem ocultar inconsistência usando truncamento para zero.
10. Pending e Rejected não consomem vagas e possuem `AcceptedDays` vazio. Status Accepted, `AcceptedDays` e consumo correspondente são gravados atomicamente.
11. Passageiros usam capacidade nula, mapa vazio e lista vazia; não oferecem vagas.
12. Edição não reinicializa ocupações existentes nem sobrescreve reservas concorrentes. Mudanças que afetem compromissos exigem validação do estado atualizado.
13. Datas passadas não influenciam disponibilidade atual ou futura; continuam armazenadas até uma evolução específica de limpeza.
14. Não existe compatibilidade automática com o campo global antigo.
15. `AcceptedDays` é subconjunto de `CompatibleDays`; Weekly Accepted exige pelo menos um dia aceito, sem duplicação.
16. Once sempre possui `AcceptedDays` vazio. Seu compromisso é exclusivamente `RequestedDate`.
17. O consumo Weekly corresponde exatamente a `AcceptedDays`; preservar `CompatibleDays` original no aceite parcial.
18. Nenhuma Pending é rejeitada automaticamente apenas porque um dia/data ficou sem vaga. Sem dia Weekly aceitável ou sem vaga na data Once, Aceitar fica desabilitado e somente Recusar permanece disponível.
19. O calendário oficial é `America/Sao_Paulo`. A referência de hoje e as validações temporais de segurança têm autoridade de servidor; `RequestedDate` é a data civil escolhida pelo passageiro.

## 6. Disponibilidade semanal

Para um dia selecionado `d`:

```text
ocupacaoWeekly(d) = quantidade de solicitações Weekly Accepted com d em AcceptedDays
weeklyAvailableSeats[d] = seatCapacity - ocupacaoWeekly(d)
```

O mapa é o saldo persistido, atualizado atomicamente no aceite; a equação é uma invariante de consistência, não uma instrução para consultar todas as solicitações a cada matching.

```text
Inicial:                  Seg 3 / Ter 3 / Qua 3
AcceptedDays Seg + Ter:   Seg 2 / Ter 2 / Qua 3
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

Uma data anterior à referência de hoje não é elegível para nova carona e suas reservas não entram na oferta vigente. Usar a referência confiável de hoje no calendário `America/Sao_Paulo`, conforme P3 na seção 11.1. Não aplicar comparações entre instantes UTC a strings de data civil.

## 8. Solicitações Weekly — criação e aceite parcial

- `CompatibleDays` preserva todos os dias originalmente compatíveis/solicitados.
- `RequestedDate` permanece nulo.
- `AcceptedDays` começa vazio e só é definido no aceite.
- A criação da solicitação não consome vagas.
- A solicitação Weekly pode ser criada se pelo menos um `CompatibleDay` ainda tiver disponibilidade.
- Mesmo que alguns dias estejam sem vaga na criação, todos os `CompatibleDays` originais são preservados.

No aceite, o sistema relê a rota e verifica individualmente cada `CompatibleDay`.

Somente os dias que ainda possuírem vaga e não causarem conflito com reservas `Once` atuais ou futuras entram em `AcceptedDays` e consomem uma vaga.

Se pelo menos um dia permanecer disponível, a solicitação é aceita parcialmente.

Exemplo:

```text
CompatibleDays:
Monday
Tuesday
Wednesday

Disponibilidade no aceite:
Monday = 0
Tuesday = 2
Wednesday = 1

Resultado:
AcceptedDays = [Tuesday, Wednesday]
```

Se nenhum dia estiver disponível:

- a solicitação não é aceita;
- nenhuma vaga é alterada;
- `AcceptedDays` permanece vazio;
- o botão Aceitar fica desabilitado;
- somente Recusar permanece disponível.

No aceite bem-sucedido, `status = Accepted`, `AcceptedDays` e `WeeklyAvailableSeats` devem ser atualizados atomicamente.

`CompatibleDays` nunca deve ser sobrescrito.

A Weekly começa a valer no momento do aceite e não possui data final automática neste MVP.

## 9. Solicitações Once

- `RequestedDate` é a fonte de verdade: data civil escolhida pelo passageiro, nunca calculada pelo servidor. Deve ser elegível no calendário oficial e compatível com a rota; `AcceptedDays` permanece vazio.
- Usar `RequestedDate.DayOfWeek`, nunca o primeiro item de `CompatibleDays`.
- `CompatibleDays` pode conter outros dias do match; a Once consome somente a data solicitada.
- No aceite, reler estado atualizado e exigir `vagasEfetivas(RequestedDate) >= 1`.
- No sucesso, adicionar `{ requestId, date, dayOfWeek }` e gravar Accepted atomicamente.
- `weeklyAvailableSeats` permanece igual. Outra data do mesmo dia da semana mantém seu saldo próprio.
- Não inserir novamente uma reserva cujo `requestId` já exista.
- Com vaga na data e solicitação elegível, Aceitar fica habilitado. Sem vaga, Aceitar fica desabilitado e somente Recusar permanece disponível. Não rejeitar automaticamente.
- Hoje e datas futuras são válidos; data anterior a hoje é passada. A decisão usa referência temporal de servidor, conforme P3.

## 10. Matching simplificado — P1 resolvida

Manter o matching simplificado nesta implementação, preservando seus filtros geográficos, Place IDs, tolerância de horário, métricas de trajeto e ordenação. Não planejar alteração funcional do matching neste ciclo nem exigir contexto temporal na busca.

O fluxo permanece:

```text
buscar rota compatível → escolher rota → escolher Weekly ou Once → enviar solicitação
```

Isso vale igualmente para Weekly e Once. O matching não antecipa disponibilidade de Once em data futura, disponibilidade futura de Weekly, horizonte temporal, próxima ocorrência ou semana futura. Não selecionar reservas Once por uma data inferida a partir de `DayOfWeek`.

A disponibilidade real é revalidada na criação da solicitação e novamente no aceite; o resultado da busca não garante vaga. O Bloco 4 limita-se a regressão e coerência com P1, sem introduzir cálculo temporal, novo saldo previsto em `MatchResult` ou exigência de exibir um mínimo de disponibilidades futuras. A apresentação não deve fabricar saldo a partir da capacidade ou do primeiro dia.

## 11. Edição e decisões arquiteturais resolvidas

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

Cada card de rota Driver em Minhas Rotas exibe sete datas consecutivas, **domingo a sábado**, com intervalo e botões de semana anterior/próxima. A ordem foi confirmada explicitamente pelo usuário. A semana inicial contém a data local do dispositivo apenas para navegação visual; isso não fornece autoridade temporal para disponibilidade ou aceite. P3 está resolvida separadamente pela referência de servidor.

Para cada data `D` da semana exibida:

```text
dia não oferecido => "-"
dia oferecido => (SeatCapacity - WeeklyAvailableSeats[D.DayOfWeek])
                + quantidade de OneTimeReservations com Date == D
```

O número representa passageiros programados, não vagas restantes. `0` identifica dia oferecido sem passageiros. Uma Once aparece somente na semana que contém sua data exata; não afeta semanas anteriores/posteriores. Reservas históricas continuam visíveis quando sua própria semana é selecionada. A apresentação usa o estado recorrente atual da rota; não cria histórico adicional de ocupação Weekly.

Exemplo: semana 25/10/2026–31/10/2026; Thursday com capacidade 3 e saldo semanal 2 tem 1 Weekly; uma Once em 29/10/2026 faz o dia exibir `2`. A grade e a navegação são exclusivas do motorista; Passenger e MatchResults não mudam neste bloco. Não adotar resumo de intervalo de vagas como `0–2 de 3`.

### 11.1. Decisões P1–P6 encerradas

| ID / estado | Decisão definitiva | Implementação / bloco afetado |
| --- | --- | --- |
| P1 — RESOLVIDA | Matching simplificado, sem previsão futura, horizonte, próxima ocorrência ou semana futura, igualmente para Weekly e Once. Escolha do tipo/data continua após selecionar a rota; disponibilidade revalidada na criação e no aceite. | Bloco 4 apenas verifica regressão e coerência, sem alteração funcional do matching. |
| P2 — RESOLVIDA | Weekly começa no momento do aceite, sem data final automática, enquanto a relação existir. Respeitar Once futuras confirmadas; reservas passadas não bloqueiam disponibilidade atual/futura. | Blocos 3, 5 e 6. Cancelamento, semestre, data final e histórico temporal completo fora do MVP. |
| P3 — RESOLVIDA | Calendário oficial America/Sao_Paulo; servidor é autoridade para data/hora atual, passado/presente/futuro, timestamps técnicos e segurança temporal. RequestedDate é a data civil escolhida pelo passageiro. | Centralizar referência temporal no Bloco 3; integrar validações nos Blocos 5–7; usar request.time nas Rules finais quando tecnicamente aplicável. |
| P4 — RESOLVIDA | Edição preserva ocupações e reservas; Role é imutável. | Redução permitida somente sem sobreocupação Weekly ou Weekly + Once em qualquer data armazenada, inclusive histórica. Dia com Weekly ou qualquer Once não pode ser removido. Serviço usa estado relido e updateTime; nenhuma restauração/cancelamento implícitos. Bloco 2. |
| P5 — RESOLVIDA | Sem rejeição automática de Pending por falta de vaga. Weekly aceita parcialmente por AcceptedDays se houver ao menos um dia viável; Once aceita somente se sua data tiver vaga. Sem disponibilidade, Aceitar desabilitado e somente Recusar disponível. | Blocos 5–6: preservar CompatibleDays; conceder/consumir somente AcceptedDays e gravar junto com Accepted atomicamente. Bloco 9 comunica a concessão parcial. |
| P6 — RESOLVIDA | Visão semanal do motorista por data, domingo a sábado. | Exibir Weekly confirmados do dia + Once da data exata. '-' para dia não oferecido, '0' para oferecido sem passageiros; Once somente na sua semana. Navegação anterior/próxima e referência local inicial exclusivamente visual. Bloco 2. |

**Todas as pendências P1–P6 estão encerradas.** P4 e P6 mantêm as decisões aprovadas e implementadas no Bloco 2. A validação estrutural histórica da edição, inclusive para reservas passadas (P4), não define a elegibilidade temporal de novos aceites (P2/P3). As decisões finais orientam os próximos blocos; não iniciar implementação automaticamente.

### 11.2. Referência temporal oficial — P3

Planejar um serviço/abstração próprio para centralizar a referência temporal confiável, evitando `DateTime.Today` espalhado pelo código. O servidor fornece autoridade sobre o instante atual; a data civil de hoje é interpretada em `America/Sao_Paulo`. O relógio do celular serve apenas à apresentação/UI, como a navegação visual de semanas.

`RequestedDate` não é calculada pelo servidor: permanece a data civil escolhida pelo passageiro. A autoridade de servidor se aplica à validação de passado/presente/futuro, aos timestamps técnicos e às validações de segurança relacionadas ao tempo. Nas Firestore Rules finais, usar `request.time` quando tecnicamente aplicável; comprovar essas verificações na integração do Bloco 7.

Para disponibilidade: `data < hoje` é passada; hoje e datas futuras continuam válidos. Uma OneTimeReservation passada permanece armazenada para rastreabilidade, mas não reduz disponibilidade atual/futura. Não converter RequestedDate em um instante UTC nem depender do relógio do celular para autorizar operações.

## 12. Concorrência e atomicidade

A referência atual é o commit REST com `currentDocument.updateTime` nas escritas de rota e solicitação e releitura em conflitos. A técnica final será adaptada no Bloco 6, mantendo consistência verificável nas Rules do Bloco 7.

Requisitos obrigatórios:

- Revalidar status Pending, propriedade, dias, data, saldo e unicidade do `requestId` no estado usado pelo aceite, com referência temporal de servidor no calendário oficial.
- Escrever status Accepted, `AcceptedDays` e consumo como uma única operação atômica; falha em qualquer precondição deixa todos os documentos inalterados. Na Once, `AcceptedDays` fica vazio e a reserva da data exata participa do mesmo commit.
- Em conflito/retry, reler a solicitação e a rota, recalcular quais `CompatibleDays` ainda são aceitáveis e reconstruir `AcceptedDays`. Não reutilizar subconjunto, saldo ou lista de reservas calculados sobre uma versão antiga.
- Serializar disputas Weekly/Weekly, Once/Once na mesma data e Weekly/Once. Datas distintas não compartilham consumo pontual, embora modifiquem o mesmo documento.
- Impedir overbooking, saldo negativo, duplicação lógica, consumo parcial fora do commit, `AcceptedDays` inconsistentes, Accepted sem consumo correspondente e consumo sem Accepted.
- Coordenar edição com aceite para impedir perda de reservas e violação de capacidade.
- Preservar a finalidade de `requestRevision` no controle de criação de pendências; ele não é um saldo nem substitui a validação de disponibilidade.
- As Rules futuras precisam validar as mudanças autorizadas e sua relação com a solicitação. Precondição de versão isolada não prova correção do cliente.

Não alterar estratégias, limites de retry, Rules ou Functions neste bloco. Não criar backend novo. Limitações da prova entre documentos nas Rules devem ser verificadas no Bloco 7 antes de publicar qualquer mudança de segurança.

## 13. Reservas passadas

Manter registros antigos em `oneTimeReservations` para rastreabilidade, ignorando-os na oferta atual/futura conforme P2/P3, com hoje fornecido pela referência temporal de servidor em `America/Sao_Paulo`. Exceções explicitamente aprovadas no Bloco 2: todas as datas armazenadas protegem redução de capacidade e remoção de dias; a visão semanal mostra reservas históricas somente quando a semana da data exata é selecionada. Não deduzir uma reserva antiga de todas as ocorrências do mesmo dia da semana. Hoje não equivale a passado: uma reserva da data atual ainda ocupa essa data conforme essa referência temporal oficial.

Não implementar limpeza no cliente nem Firebase Function de remoção automática. Essa possível evolução permanece fora dos Blocos 0–9 aqui definidos. O crescimento da lista deverá ser acompanhado futuramente sem introduzir limpeza neste trabalho.

## 14. Ausência de migração e compatibilidade

O ambiente de teste é descartável e será recriado após implementação e validação. Não haverá conversão automática, dupla leitura, fallback ou inferência de `seatCapacity` a partir de `availableSeats`.

No formato novo, documento de motorista com campos obrigatórios ausentes ou inválidos deve ser rejeitado como inválido. Não fabricar um mapa de saldos a partir de documentos antigos. A remoção definitiva dos leitores/escritores antigos deve ocorrer coordenada com seus consumidores, sem ampliar esta regra para refatorações de outros campos.

Fixtures e `scripts/start-emulators-seeded.js` precisarão produzir o schema novo. A recriação do ambiente e eventual descarte de usuários/rotas/solicitações pertencem à transição futura; nenhum dado é apagado agora. Não executar o seed durante o Bloco 0.

## 15. Inventário histórico de dependências de AvailableSeats

> Registro histórico da auditoria de 14/10/2026 sobre o commit `13103b7`. As contagens, referências, comportamentos e pendências mencionados nesta seção descrevem aquele estado, sem substituir as decisões finais das seções 5–13 e 16. Preservado integralmente para rastreabilidade.

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

## 16. Divisão dos blocos 1–9

Plano incremental atualizado com P1–P6 resolvidas. Preservar o histórico dos Blocos 1 e 2 abaixo. A reorganização dos blocos restantes consolida as decisões finais; não declara funcionalidades novas como já implementadas. Cada bloco exige escopo próprio, commit separado e validação pertinente. Não iniciar implementação nesta atualização documental.

| Bloco | Entrega prevista | Critério de conclusão / dependências |
| --- | --- | --- |
| 1 — Modelo persistido e serialização (histórico) | Alterar WeeklyRoute, definir tipo de reserva e DTOs REST, ler/escrever/validar o novo schema; sem fallback. | Implementado no respectivo commit: testes de round-trip, driver/passenger e dados inválidos; consumidores compatibilizados para compilar. Não transformar adaptações temporárias em compatibilidade permanente. |
| 2 — Criação, edição e visão semanal (histórico) | Uma capacidade inicial replicada por dia; preservação de ocupações e reservas ao editar; dias novos; Role imutável; passageiros por data na semana do motorista. | Implementado no respectivo commit, com P4 e P6 resolvidas: redução sem sobreocupação, remoção somente sem compromissos, inclusive históricos; visão domingo–sábado com Weekly + Once da data, '-'/'0' e navegação semanal. |
| 3 — Disponibilidade e referência temporal | Consolidar cálculos de saldo semanal/por data, America/Sao_Paulo, referência confiável de hoje e tratamento de reservas passadas/futuras. | Centralizar referência temporal em serviço/abstração próprio com autoridade de servidor; testar hoje, passado, futuro, virada de dia e datas distintas do mesmo weekday. P1–P3 já resolvidas; sem camadas desnecessárias. |
| 4 — Matching | Manter matching simplificado; apenas garantir regressão e coerência com P1. | Nenhuma alteração funcional ou previsão futura para Weekly/Once. Preservar fluxo de escolha posterior de tipo/data, filtros geográficos, ordenação, preço e mapas; revalidação real na criação e no aceite. |
| 5 — RideRequest e aceite parcial | Introduzir acceptedDays; Weekly parcial; Once por RequestedDate; comportamento de Aceitar/Recusar. | Preservar CompatibleDays, preço, duplicação e requestRevision. Pending/Rejected/Once com AcceptedDays vazio; Weekly Accepted com subconjunto não vazio e consumo exato. Sem rejeição automática; sem disponibilidade, somente Recusar. |
| 6 — Concorrência e idempotência | Revalidar Weekly/Once, retries, conflitos e atomicidade de status + AcceptedDays + consumo/reserva. | Reler rota e solicitação, reconstruir AcceptedDays em cada tentativa; testar disputas Weekly/Weekly, Once/Once, Weekly/Once e edição concorrente, sem overbooking, duplicação ou consumo fora do commit. |
| 7 — Firestore Rules e integração | Schema final incluindo acceptedDays, segurança, request.time quando tecnicamente aplicável e integração no Emulator. | Validar autoridade temporal e vínculos de aceite/consumo, impedir adulteração e testar Weekly parcial/Once, mesma data/datas distintas e preservação de preço. Nenhuma publicação automática. |
| 8 — Encerramento e seed | Atualizar seed para o schema final; remover resíduos; revisar fixtures/documentação; regressão completa e preparação de recriação controlada do ambiente. | Suíte pertinente completa e Android Release aprovados; teste manual do fluxo e banco. Descarte/recriação e eventual PR exigem autorização daquele bloco; sem push/merge automático. |
| 9 — Revisão da visão do passageiro | Após estabilização do sistema de disponibilidade, revisar caronas/solicitações confirmadas para apresentar Weekly aceita parcialmente por dia e Once por data. | Mostrar claramente AcceptedDays, dias solicitados não concedidos (CompatibleDays menos AcceptedDays) e datas Once. Usar a mesma relação/solicitação, sem criar rota extra por dia. Evolução de apresentação/UX posterior aos blocos principais; não implementar agora. |

A ordem é incremental. Adaptações anteriores de criação/aceite não substituem a implementação do contrato final de aceite parcial e referência temporal. Se modelos e consumidores exigirem compatibilização para compilar, explicitar a menor adaptação no escopo autorizado do bloco.

Funções de limpeza, cancelamento/restauração, vigência por semestre, data final automática, histórico temporal completo, capacidade configurável por dia, chat, pagamento, reputação e nova precificação permanecem fora deste plano.

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
