# UniRota — Plano de Implementação: Controle de Vagas por Dia e Data

## 1. Objetivo

Substituir o modelo atual de disponibilidade global por rota por um modelo em que a disponibilidade de vagas seja controlada por **dia da semana** e, no caso de solicitações pontuais, também por **data específica**.

A mudança deve preservar o fluxo atual de criação de rotas, matching, solicitação Once/Weekly, aceite/rejeição e caronas confirmadas, alterando apenas a forma como as vagas são representadas, calculadas e consumidas.

Esta implementação é considerada sensível porque altera:

- o schema persistido de `weeklyRoutes`;
- a lógica de matching;
- a lógica de criação e aceite de `RideRequest`;
- a concorrência no consumo de vagas;
- as Firestore Rules;
- testes e dados usados na validação.

A implementação deve ser feita em blocos pequenos, com validação obrigatória antes de avançar.

---

## 2. Regras funcionais aprovadas

### 2.1. Capacidade informada pelo motorista

O motorista continua informando **uma única quantidade de vagas** ao criar a rota.

Exemplo:

```text
Dias: Monday, Tuesday, Wednesday
Capacidade informada: 3
```

O sistema deve inicializar todos os dias selecionados com a mesma capacidade:

```text
Monday: 3
Tuesday: 3
Wednesday: 3
```

O motorista não poderá, nesta etapa, definir capacidades diferentes para dias diferentes.

---

### 2.2. Solicitações semanais

Uma solicitação `weekly` consome uma vaga somente nos dias incluídos na solicitação aceita.

Exemplo:

```text
Antes:
Monday: 3
Tuesday: 3
Wednesday: 3

Solicitação semanal aceita:
Monday + Wednesday

Depois:
Monday: 2
Tuesday: 3
Wednesday: 2
```

O aceite semanal deve ocorrer somente se todos os dias envolvidos ainda possuírem vaga.

Não existe aceite parcial.

---

### 2.3. Solicitações pontuais

Uma solicitação `once` ocupa uma vaga somente na data específica solicitada.

Ela não reduz permanentemente a disponibilidade daquele dia da semana.

Exemplo:

```text
Capacidade semanal de Tuesday: 3

Reserva pontual:
2026-10-20 - Tuesday

Resultado:
2026-10-20 possui uma vaga a menos.
Outras terças continuam com a capacidade normal.
```

As reservas pontuais aceitas devem ser registradas dentro da própria `weeklyRoute`, associadas no mínimo a:

```text
requestId
date
dayOfWeek
```

Reservas pontuais no passado devem ser ignoradas no cálculo de disponibilidade.

A limpeza automática dessas reservas antigas será feita futuramente por uma Firebase Function e não faz parte desta implementação.

---

## 3. Modelo conceitual de `weeklyRoutes`

O schema final será definido no Bloco 0, mas conceitualmente a rota de motorista deverá representar:

```text
weeklyRoutes/{routeId}

seatCapacity: 3

weeklyAvailableSeats:
  Monday: 3
  Tuesday: 2
  Wednesday: 3

oneTimeReservations:
  - requestId: "requestA"
    date: "2026-10-20"
    dayOfWeek: "Tuesday"

  - requestId: "requestB"
    date: "2026-10-21"
    dayOfWeek: "Wednesday"
```

### Significado

`seatCapacity`:

- capacidade base informada pelo motorista;
- igual para todos os dias selecionados quando a rota é criada.

`weeklyAvailableSeats`:

- representa a disponibilidade recorrente após considerar passageiros semanais aceitos;
- uma solicitação semanal diminui apenas os dias envolvidos.

`oneTimeReservations`:

- representa ocupações pontuais de datas específicas;
- não altera permanentemente `weeklyAvailableSeats`;
- reservas antigas permanecem no documento por enquanto, mas são ignoradas nos cálculos.

Os nomes exatos dos campos ainda devem ser confirmados no Bloco 0 antes da implementação.

---

## 4. Cálculo de disponibilidade

Para um dia específico, a disponibilidade efetiva deve considerar:

```text
disponibilidade efetiva
=
weeklyAvailableSeats do dia
-
quantidade de reservas pontuais aceitas naquela data exata
```

Reservas de outras datas do mesmo dia da semana não devem ser somadas.

Exemplo:

```text
Tuesday semanal: 2 vagas

20/10/2026:
1 reserva pontual
=> 1 vaga disponível

27/10/2026:
0 reservas pontuais
=> 2 vagas disponíveis
```

---

## 5. Matching

O matching continua considerando os filtros geográficos e funcionais já existentes.

Depois de encontrar os dias compatíveis entre passageiro e motorista, o sistema deve aplicar a disponibilidade.

### Exemplo

```text
Disponibilidade:
Monday: 3
Tuesday: 2
Wednesday: 1

Dias compatíveis:
Monday + Tuesday

Resultado exibido:
2 vagas restantes
```

A quantidade apresentada ao passageiro deve ser o **menor valor de disponibilidade entre os dias compatíveis restantes**.

### Dia sem vaga

Se um dia compatível tiver disponibilidade igual a zero:

- esse dia deve ser removido da compatibilidade;
- os demais dias continuam válidos.

Exemplo:

```text
Disponibilidade:
Monday: 3
Tuesday: 0
Wednesday: 2

Compatibilidade inicial:
Monday + Tuesday + Wednesday

Compatibilidade final:
Monday + Wednesday

Exibição:
2 vagas restantes
```

Se nenhum dia compatível continuar com vaga, a rota não deve aparecer como compatível.

---

# 6. Estratégia de implementação

A implementação será dividida em blocos.

Cada bloco deve:

1. alterar somente o necessário;
2. possuir testes próprios;
3. passar pelas validações definidas;
4. gerar um commit separado;
5. ser aprovado antes do próximo bloco.

Não implementar vários blocos antecipadamente.

---

## Bloco 0 — Arquitetura e definição final do schema

### Objetivo

Definir completamente o novo modelo de dados antes de alterar o comportamento do aplicativo.

### Atividades

- revisar todos os pontos que leem ou escrevem `AvailableSeats`;
- revisar `WeeklyRoute`;
- revisar `RideRequest`;
- revisar `MatchingService`;
- revisar `FirebaseRouteService`;
- revisar `FirebaseRideRequestService`;
- revisar `RideRequestRules`;
- revisar ViewModels que exibem vagas;
- definir nomes e tipos finais dos novos campos;
- definir representação de `weeklyAvailableSeats`;
- definir representação de `oneTimeReservations`;
- definir invariantes do novo schema;
- documentar como serão tratados driver routes e passenger routes.

### Migração

Não será criada migração automática dos dados antigos.

Após a implementação e validação:

- os usuários de teste antigos serão removidos;
- suas rotas e solicitações antigas serão descartadas;
- novos usuários serão criados para testar a nova versão;
- todas as novas rotas nascerão no novo schema.

Portanto, o aplicativo poderá exigir somente o novo formato após a transição.

### Critério de conclusão

Nenhum comportamento funcional deve ser alterado ainda.

O bloco termina com um plano técnico fechado e sem ambiguidades.

---

## Bloco 1 — Modelo persistido e serialização

### Objetivo

Implementar o novo schema no domínio e na comunicação com o Firestore.

### Atividades

- atualizar `WeeklyRoute`;
- criar modelo para reserva pontual, se necessário;
- implementar serialização dos novos campos;
- implementar desserialização dos novos campos;
- remover dependência estrutural do antigo `AvailableSeats` global;
- garantir validação de dados inválidos;
- criar testes de leitura e escrita.

### Não fazer neste bloco

- não alterar matching;
- não alterar aceite;
- não alterar Firestore Rules;
- não alterar UI além do necessário para compilar.

### Critério de conclusão

O app deve conseguir criar objetos, serializar e ler corretamente o novo modelo.

---

## Bloco 2 — Criação e edição de rotas

### Objetivo

Fazer novas rotas nascerem corretamente no novo modelo.

### Criação

Quando o motorista informar:

```text
3 vagas
Monday
Tuesday
Wednesday
```

a rota deve nascer com:

```text
seatCapacity: 3

weeklyAvailableSeats:
Monday: 3
Tuesday: 3
Wednesday: 3

oneTimeReservations: []
```

### Edição

O motorista continuará manipulando uma única capacidade.

Antes de permitir redução da capacidade, deve-se respeitar reservas já confirmadas.

Se a nova capacidade for incompatível com ocupações existentes, a operação não deve gerar valores negativos nem estado inconsistente.

A UX definitiva para remoção de passageiros permanece fora do escopo caso seja necessária.

### Critério de conclusão

Criar e editar rotas deve preservar todas as invariantes do novo schema.

---

## Bloco 3 — Motor central de disponibilidade

### Objetivo

Centralizar a lógica de disponibilidade em código puro e testável.

### A lógica deve responder

- quantas vagas semanais existem em cada dia;
- quantas vagas existem em uma data específica;
- quais dias compatíveis ainda possuem vaga;
- qual é o menor número de vagas entre os dias compatíveis;
- se uma solicitação semanal pode ser aceita;
- se uma solicitação pontual pode ser aceita.

### Regras

- reservas pontuais passadas são ignoradas;
- reservas pontuais contam somente na data exata;
- reservas pontuais de datas diferentes não se acumulam entre si;
- um dia com zero vaga é removido da compatibilidade;
- se todos os dias chegarem a zero, não existe match válido.

### Critério de conclusão

A lógica deve possuir cobertura de testes suficiente antes de ser integrada ao matching.

---

## Bloco 4 — Matching e apresentação dos resultados

### Objetivo

Fazer o buscador considerar a disponibilidade real.

### Atividades

- integrar o motor de disponibilidade ao `MatchingService`;
- remover dias compatíveis sem vaga;
- excluir a rota se nenhum dia permanecer;
- calcular o menor número de vagas entre os dias restantes;
- expor esse valor no `MatchResult`;
- exibir a quantidade correta na tela de resultados.

### Exemplo

```text
Monday: 3
Tuesday: 2
Wednesday: 1

Compatibilidade:
Monday + Tuesday

UI:
2 vagas restantes
```

### Critério de conclusão

O resultado exibido deve representar a menor disponibilidade real dos dias efetivamente compatíveis.

---

## Bloco 5 — Criação de RideRequests

### Objetivo

Garantir que cada solicitação carregue as informações necessárias para o futuro consumo correto de vagas.

### Weekly

A solicitação deve manter os dias compatíveis que serão consumidos caso seja aceita.

### Once

A solicitação deve manter a data específica em `requestedDate`.

A data deve corresponder a um dia compatível válido da rota.

### Regra

Criar a solicitação não deve consumir vaga.

O consumo continua ocorrendo apenas no aceite.

### Critério de conclusão

Toda `RideRequest` pendente deve conter informação suficiente para determinar exatamente quais vagas seriam consumidas no aceite.

---

## Bloco 6 — Aceite e concorrência

### Objetivo

Implementar o consumo real de vagas com segurança contra overbooking.

Este é o bloco mais sensível da implementação.

### Aceite Weekly

Antes do aceite:

- recalcular a disponibilidade;
- verificar todos os dias solicitados;
- impedir aceite se qualquer dia necessário estiver sem vaga.

Ao aceitar:

- diminuir exatamente 1 vaga em cada dia envolvido;
- alterar o status da `RideRequest` para `accepted`.

### Aceite Once

Antes do aceite:

- verificar a disponibilidade da data específica.

Ao aceitar:

- adicionar uma entrada em `oneTimeReservations`;
- usar o `requestId`;
- registrar data e dia da semana;
- alterar a `RideRequest` para `accepted`.

### Concorrência

A alteração da rota e da solicitação deve permanecer atômica.

Duas pessoas não podem ocupar simultaneamente a última vaga.

O sistema deve suportar conflitos de concorrência sem produzir:

- vagas negativas;
- duas reservas para a última vaga;
- `RideRequest accepted` sem reserva correspondente;
- reserva registrada sem `RideRequest accepted`.

### Critério de conclusão

Os testes de concorrência devem estar aprovados antes de alterar as Firestore Rules.

---

## Bloco 7 — Firestore Rules e validação integrada

### Objetivo

Adaptar a segurança do banco ao novo schema e ao novo fluxo de aceite.

### As Rules devem validar

- formato completo de `weeklyRoutes`;
- capacidade válida;
- mapa de vagas por dia;
- dias válidos;
- ausência de valores negativos;
- formato das reservas pontuais;
- `requestId`;
- data;
- dia da semana;
- alterações permitidas pelo proprietário;
- alterações técnicas de aceite;
- criação e atualização de `RideRequest`;
- invariantes de concorrência que sejam comprováveis pelas Rules;
- default deny para qualquer operação não autorizada.

### Validação integrada

Testar no mínimo:

- weekly + weekly em dias iguais;
- weekly + weekly em dias diferentes;
- once + once na mesma data;
- once + once em datas diferentes do mesmo dia da semana;
- weekly + once;
- dia parcialmente ocupado;
- dia lotado;
- remoção do dia lotado do matching;
- rota totalmente lotada;
- reservas pontuais passadas;
- tentativa de overbooking;
- dois aceites concorrentes;
- tentativa de adulterar disponibilidade pelo cliente.

### Critério de conclusão

Regras publicadas somente depois da validação do comportamento esperado.

---

## Bloco 8 — Limpeza, documentação e encerramento

### Objetivo

Remover resíduos do modelo antigo e preparar a versão para testes robustos.

### Atividades

- remover código legado de `AvailableSeats` que não seja mais necessário;
- procurar referências obsoletas;
- atualizar `AGENTS.md` caso o schema final seja diferente do planejado;
- atualizar documentação técnica;
- atualizar `docs/PENDENCIAS.md` se necessário;
- executar suíte completa;
- executar build Android Release;
- preparar checklist de teste manual;
- criar PR final da implementação.

### Depois da aprovação

Somente após todos os blocos e validações:

1. remover usuários/dados antigos de teste;
2. criar novos usuários de teste;
3. criar novas rotas no novo schema;
4. executar a bateria completa de testes manuais;
5. validar o banco após cada cenário importante;
6. considerar a versão pronta para a próxima rodada de beta test.

---

# 7. Estratégia de commits

Cada bloco deve gerar seu próprio commit.

Sugestão:

```text
docs(vagas): define novo modelo de disponibilidade
feat(vagas): adiciona modelo de vagas por dia
feat(rotas): inicializa disponibilidade por dia
feat(vagas): adiciona cálculo central de disponibilidade
feat(matching): considera vagas por dia nos resultados
feat(solicitacoes): prepara consumo de vagas por tipo
feat(solicitacoes): aplica consumo atômico de vagas
test(firebase): valida regras do novo modelo de vagas
chore(vagas): remove modelo legado e atualiza documentação
```

Os nomes podem ser ajustados conforme os arquivos efetivamente alterados.

---

# 8. Gate de validação entre blocos

Não avançar automaticamente para o próximo bloco.

Após cada bloco:

```text
git diff --check
dotnet test UniRota.Tests/UniRota.Tests.csproj -c Release
```

Executar também quando pertinente:

```text
dotnet build UniRota/UniRota.csproj -c Release -f net8.0-android
```

Se houver alteração em Firebase Functions:

```text
cd functions
npm test
```

Qualquer regressão deve ser investigada antes do próximo bloco.

---

# 9. Cenários essenciais para a validação final

## Cenário A — capacidades independentes

```text
Route:
Monday: 3
Tuesday: 3
Wednesday: 3

Aceitar Weekly em Monday:
Monday: 2
Tuesday: 3
Wednesday: 3
```

## Cenário B — semanal em vários dias

```text
Aceitar Weekly em Monday + Wednesday:

Monday: 2
Tuesday: 3
Wednesday: 2
```

## Cenário C — pontual

```text
Tuesday semanal: 3

Once em 20/10:
20/10 => 2 efetivas

Outra Tuesday:
=> 3 efetivas
```

## Cenário D — duas reservas em datas diferentes

```text
20/10 Tuesday: 1 reserva
27/10 Tuesday: 1 reserva

Não interpretar como duas vagas ocupadas em todas as Tuesdays.
```

## Cenário E — matching parcial

```text
Monday: 3
Tuesday: 0
Wednesday: 2

Compatibilidade original:
Monday + Tuesday + Wednesday

Resultado:
Monday + Wednesday
2 vagas restantes
```

## Cenário F — rota indisponível

Se todos os dias compatíveis tiverem zero vaga, a rota não deve aparecer.

## Cenário G — concorrência

Com uma única vaga disponível, dois aceites simultâneos não podem resultar em dois passageiros aceitos.

---

# 10. Fora do escopo desta implementação

Não implementar junto com este incremento:

- capacidade diferente configurável por dia;
- cancelamento com restauração automática de vaga;
- expulsão de passageiro ao reduzir capacidade;
- limpeza automática de reservas pontuais antigas;
- cooldown de solicitações rejeitadas;
- mudança da fórmula de preço;
- novos recursos de chat, pagamento ou reputação.

Esses itens devem permanecer como evoluções separadas.

---

# 11. Definição de pronto

A implementação estará concluída quando:

- o novo schema estiver definido e implementado;
- rotas novas utilizarem somente o novo modelo;
- disponibilidade semanal funcionar por dia;
- reservas pontuais funcionarem por data;
- o matching remover dias sem vaga;
- a UI exibir o menor número de vagas dos dias compatíveis;
- `RideRequest` consumir as vagas corretas somente no aceite;
- concorrência impedir overbooking;
- Firestore Rules estiverem adaptadas;
- testes automatizados estiverem aprovados;
- build Android estiver aprovado;
- testes manuais com novos usuários estiverem aprovados;
- dados antigos de teste tiverem sido descartados após a validação;
- documentação final estiver atualizada.
