# UniRota — Pendências não bloqueantes

Este arquivo registra itens que ficaram pendentes durante os incrementos do MVP, mas que **não impedem o avanço do desenvolvimento**.

A ideia é continuar priorizando o fluxo principal do MVP e retornar a esta lista somente depois que todos os incrementos obrigatórios estiverem concluídos e validados.

## Regras de uso

- Adicionar aqui somente itens que não bloqueiam o incremento atual.
- Não interromper um incremento concluído apenas para resolver itens desta lista.
- Revisar esta lista ao final de todos os incrementos.
- Resolver primeiro itens que afetem demonstração, estabilidade, segurança ou documentação.
- Se o tempo não permitir, registrar claramente o que ficou como evolução futura.

---

## Incremento 1 — Autenticação

### 1. Remover configuração Firebase hardcoded do `MauiProgram.cs`
**Status:** Pendente  
**Prioridade:** Média

Atualmente, `ApiKey` e `ProjectId` estão configurados diretamente no código.

A Web API Key do Firebase não é uma credencial administrativa, mas a configuração deve ser organizada de forma mais adequada antes da entrega final, principalmente para evitar acoplamento desnecessário e facilitar manutenção.

**Possíveis ações futuras:**
- mover configuração para arquivo apropriado;
- usar configuração por ambiente;
- documentar como configurar o projeto localmente;
- revisar restrições da API Key no Google Cloud/Firebase Console.

### 2. Revisar restrições da Web API Key
**Status:** Pendente  
**Prioridade:** Média

A chave usada pelo aplicativo deve ter restrições adequadas quando possível.

**Possíveis ações futuras:**
- revisar as APIs habilitadas;
- restringir a chave ao necessário para o projeto;
- documentar que a segurança dos dados continua dependendo de Authentication e Firestore Security Rules.

### 3. Refatorar `FirebaseAuthService` apenas se ele crescer além do escopo atual
**Status:** Pendente / condicional  
**Prioridade:** Baixa

O serviço concentra chamadas REST, DTOs internos, tratamento de erros, sessão e acesso ao documento `users/{uid}`.

No momento, isso continua aceitável porque mantém a implementação coesa e evita abstrações excessivas.

**Revisar somente se:**
- novas responsabilidades forem adicionadas ao arquivo;
- a manutenção se tornar difícil;
- houver duplicação relevante com outros serviços Firebase.

**Não fazer apenas para reduzir número de linhas.**

### 4. Revisar o uso de `CreatedAtUtc` baseado no relógio do dispositivo
**Status:** Pendente  
**Prioridade:** Baixa

Campos de criação usam `DateTimeOffset.UtcNow` no dispositivo.

Isso é suficiente para o MVP, mas futuramente pode ser substituído por timestamp gerado pelo servidor/Firestore para maior confiabilidade e ordenação consistente entre dispositivos.

### 5. Tratar conta criada no Authentication sem perfil no Firestore
**Status:** Pendente  
**Prioridade:** Baixa

Se o Firebase Authentication criar a conta e a gravação de `users/{uid}` falhar, a conta poderá permanecer sem documento de perfil.

O MVP não implementa rollback para manter a solução simples.

**Possíveis ações futuras:**
- excluir a conta recém-criada em caso de falha;
- permitir recuperação/criação automática do perfil;
- criar mecanismo de consistência durante login.

### 6. Testar autenticação e fluxo principal em iOS
**Status:** Pendente  
**Prioridade:** Média

O fluxo foi validado funcionalmente no Android.

O projeto mantém target iOS e usa código compartilhado, mas a validação completa depende de ambiente Mac/Xcode.

**Validar futuramente:**
- inicialização;
- login e cadastro;
- SecureStorage e restauração de sessão;
- logout;
- Firestore via REST;
- rotas, matching e solicitações.

### 7. Melhorar acabamento visual das telas
**Status:** Pendente  
**Prioridade:** Baixa

As telas foram implementadas com foco em funcionalidade e estabilidade.

**Possíveis melhorias futuras:**
- identidade visual do UniRota;
- tipografia;
- cores;
- espaçamentos;
- ícones;
- feedback visual;
- acessibilidade;
- refinamento de estados vazios, loading e mensagens.

Fazer somente depois que o fluxo principal do MVP estiver completo.

### 8. Centralizar estilos e cores do aplicativo
**Status:** Pendente  
**Prioridade:** Baixa

Durante o desenvolvimento, parte dos estilos e cores ficou definida localmente nas páginas.

No futuro, pode ser útil criar estilos globais próprios e garantidamente carregados.

**Objetivo futuro:**
- reduzir repetição;
- manter consistência visual;
- facilitar manutenção;
- evitar dependência de estilos do template padrão do MAUI.

### 9. Revisar identificador definitivo do aplicativo
**Status:** Pendente  
**Prioridade:** Média

O projeto ainda utiliza o identificador padrão/placeholder:

`com.companyname.unirota`

Antes de uma distribuição mais formal, esse identificador deve ser substituído por um identificador definitivo.

---

## Incremento 2 — Rotas semanais

### 1. Revisar Firestore Security Rules antes da validação com usuários reais
**Status:** Pendente  
**Prioridade:** Alta

Durante o desenvolvimento, as Firestore Security Rules foram temporariamente configuradas de forma aberta.

Antes de qualquer validação com usuários reais, as regras devem proteger pelo menos:

- `users/{uid}`;
- `weeklyRoutes/{routeId}`;
- `rideRequests/{requestId}`;
- propriedade das rotas por `userId`;
- autoria de solicitações por `passengerUserId`;
- decisão de solicitações somente pelo `driverUserId`;
- alterações permitidas em `AvailableSeats`;
- incremento controlado de `requestRevision`;
- campos que não podem ser alterados arbitrariamente pelo cliente.

A implementação cliente já faz verificações de propriedade, mas isso **não substitui** regras de segurança no Firestore.

### 2. Limitar quantidade máxima de vagas oferecidas
**Status:** Pendente  
**Prioridade:** Baixa

Atualmente, o cadastro de uma rota de motorista valida apenas que a quantidade de vagas seja maior que zero. Isso permite informar valores incompatíveis com um veículo de passeio, como dezenas ou centenas de vagas.

**Possíveis ações futuras:**
- definir um limite máximo simples para o MVP, por exemplo entre 1 e 7 vagas;
- validar o limite tanto no ViewModel quanto no serviço;
- futuramente relacionar a quantidade máxima de vagas à capacidade cadastrada do veículo.

### 3. Tratar redução de vagas abaixo da quantidade já ocupada
**Status:** Pendente  
**Prioridade:** Alta

Atualmente, um motorista pode editar a quantidade de vagas da `WeeklyRoute` sem uma regra específica que considere passageiros já aceitos.

Exemplo: se uma rota possui 4 vagas e as 4 já estão ocupadas, reduzir a capacidade para 3 gera uma inconsistência entre capacidade e caronas confirmadas.

**Decisão futura desejada:**
- detectar quando a nova quantidade de vagas é menor que a quantidade já comprometida;
- exibir um alerta antes de concluir a edição;
- exigir que o motorista escolha qual passageiro será removido da rota;
- definir como a solicitação/caronas afetadas serão atualizadas;
- definir se a vaga deve ser restaurada em outros cenários de remoção/cancelamento;
- garantir atualização atômica/coerente no Firestore.

Essa regra não deve ser implementada de forma silenciosa.

---

## Incremento 3 — Matching e solicitações de carona

### 1. Adicionar cooldown após uma solicitação rejeitada
**Status:** Pendente  
**Prioridade:** Média

Atualmente, quando o motorista rejeita uma solicitação, o par `PassengerRouteId + DriverRouteId` volta a aparecer imediatamente em “Rotas compatíveis”.

Para o MVP atual, `Rejected` não bloqueia uma nova solicitação.

**Decisão futura:**
- adicionar um cooldown de alguns dias antes que o mesmo passageiro possa enviar uma nova solicitação para o mesmo par de rotas.

**Definir antes da implementação:**
- duração exata do cooldown;
- se o prazo começa em `rejectedAt`;
- necessidade de persistir `rejectedAt` ou outro timestamp de decisão;
- comportamento caso uma das rotas seja editada durante o cooldown;
- texto apresentado ao usuário durante o bloqueio.

### 2. Evoluir capacidade de vagas por ocorrência/data
**Status:** Pendente  
**Prioridade:** Média

No modelo atual, `AvailableSeats` pertence à `WeeklyRoute` inteira.

Consequentemente:

`mesma DriverRouteId = mesma capacidade compartilhada`

Solicitações `Once` e `Weekly`, mesmo em dias diferentes, disputam a mesma quantidade de vagas.

Isso é aceitável para o MVP, mas não representa com precisão cenários reais em que a ocupação muda por dia ou por ocorrência.

**Possíveis evoluções futuras:**
- capacidade por dia da semana;
- capacidade por data concreta;
- ocorrências derivadas de uma rota semanal;
- distinção entre compromisso recorrente e viagem única;
- regras de consumo/restauração de vaga por ocorrência.

### 3. Definir cancelamento/desistência de carona confirmada
**Status:** Pendente  
**Prioridade:** Média

O MVP permite aceitar e visualizar caronas confirmadas, mas não permite cancelar/desistir depois do aceite.

Antes de implementar, definir:
- passageiro pode cancelar?
- motorista pode cancelar?
- existe prazo mínimo?
- a vaga é restaurada automaticamente?
- o outro usuário deve receber algum aviso?
- o `RideRequest` ganha novo status ou é criado outro registro?
- como o cancelamento afeta solicitações anteriormente rejeitadas por falta de vaga?

### 4. Implementar notificações
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

O aplicativo ainda não envia notificações quando:
- uma solicitação é recebida;
- uma solicitação é aceita;
- uma solicitação é rejeitada;
- concorrentes são rejeitados porque a última vaga foi ocupada;
- uma carona confirmada é alterada ou futuramente cancelada.

No MVP atual, o usuário precisa abrir as telas correspondentes para observar mudanças.

### 5. Criar histórico de solicitações rejeitadas/processadas
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

Solicitações `Rejected` permanecem persistidas no Firestore, mas não possuem uma tela de histórico.

Futuramente pode ser útil permitir consulta a:
- solicitações rejeitadas;
- data da decisão;
- motivo, caso essa funcionalidade seja adicionada;
- solicitações antigas já processadas.

Não é necessário para o fluxo principal atual.

### 6. Revisar prevenção de duplicidade de solicitações em concorrência distribuída
**Status:** Pendente para revisão técnica  
**Prioridade:** Média

A criação de `rideRequest` usa commit coordenado com `requestRevision`, e o fluxo atual protege a consistência necessária entre criação e aceite.

Antes de uma versão com uso real em múltiplos dispositivos, revisar especificamente o cenário de duas tentativas simultâneas de criação para o mesmo:

`PassengerRouteId + DriverRouteId`

e confirmar por teste concorrente que nunca permanecem duas solicitações `Pending` equivalentes.

Se necessário, reforçar a estratégia de coordenação sem introduzir uma solução que quebre o fluxo atômico já existente.

### 7. Validar e documentar índices compostos do Firestore
**Status:** Pendente  
**Prioridade:** Média

O fluxo de matching e solicitações utiliza consultas compostas em `rideRequests`, incluindo combinações de:
- `passengerUserId` + `status`;
- `driverUserId` + `status`;
- `driverRouteId` + `status`;
- consultas de solicitações ativas/aceitas.

Dependendo da configuração do projeto, o Firestore pode exigir índices compostos.

**Antes da entrega:**
- validar todas as queries no projeto Firebase real;
- criar os índices exigidos;
- documentar quais índices fazem parte da configuração necessária do UniRota.

### 8. Revisar estratégia de nomes desnormalizados
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

`WeeklyRoute` e `RideRequest` armazenam nomes como snapshots para evitar leituras adicionais.

Rotas antigas sem `userName` continuam funcionando usando fallback visual, e alterações futuras no nome do usuário não atualizam automaticamente snapshots históricos.

Isso é intencional no MVP.

Futuramente decidir se:
- snapshots históricos devem permanecer imutáveis;
- somente novos registros recebem o novo nome;
- ou haverá algum mecanismo explícito de sincronização/migração.

### 9. Resolver divergência do test host no Visual Studio local
**Status:** Pendente  
**Prioridade:** Média

Os testes automatizados passam no ambiente do Codex, mas em uma execução pelo Visual Studio local todos os testes falharam, enquanto o aplicativo e os testes manuais continuaram funcionando.

A divergência não bloqueou os incrementos atuais.

**Investigar futuramente:**
- configuração do Test Explorer;
- target `net8.0-windows10.0.19041.0`;
- inicialização transitiva do MAUI/Windows App SDK;
- versão/restauração dos pacotes xUnit e `Microsoft.NET.Test.Sdk`;
- diferenças entre `dotnet test` no terminal e execução pelo Visual Studio.

Não remover ou enfraquecer testes para contornar o problema.

### 10. Testar concorrência real em múltiplos dispositivos
**Status:** Pendente  
**Prioridade:** Média

O aceite usa `documents:commit`, `updateTime`, retry e `requestRevision` para impedir consumo incorreto da última vaga.

A arquitetura foi validada por build, testes de lógica e testes funcionais comuns, mas o cenário de concorrência real deve ser testado explicitamente antes de uso mais amplo.

**Cenários mínimos:**
- dois dispositivos tentando processar simultaneamente a última vaga;
- criação de solicitação enquanto um aceite está sendo processado;
- retry após falha de precondition;
- confirmação de que nenhuma vaga fica negativa;
- confirmação de que não ocorre `Accepted` sem decremento correspondente.

### 11. Adicionar timestamps de decisão às solicitações
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

Atualmente, `RideRequest` registra `CreatedAtUtc`, mas não foi definido um timestamp específico para aceite ou rejeição.

Campos futuros como `acceptedAtUtc` e `rejectedAtUtc` podem ser úteis para:
- cooldown após rejeição;
- histórico;
- ordenação;
- auditoria;
- notificações;
- métricas de tempo de resposta.

### 12. Definir tratamento de caronas `Once` após a data passar
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

Uma solicitação `Once` aceita continua aparecendo em “Rotas confirmadas” mesmo depois de `RequestedDate` ter passado.

O comportamento é intencional no MVP porque ainda não existe conceito de carona concluída.

Futuramente definir:
- quando uma carona deixa de ser ativa;
- se deve ir para histórico;
- se haverá status `Completed`;
- se a conclusão é automática pela data ou explícita.

---

## Infraestrutura, entrega e validação

### 1. Revisar configuração final do Firestore antes de usuários reais
**Status:** Pendente  
**Prioridade:** Alta

Antes de validação externa, executar uma revisão conjunta de:
- Security Rules;
- índices compostos;
- restrições da Web API Key;
- comportamento das operações atômicas;
- dados antigos ainda existentes no banco;
- documentos de teste;
- permissões efetivas para `users`, `weeklyRoutes` e `rideRequests`.

### 2. Revisar compatibilidade de documentos antigos
**Status:** Pendente / validação final  
**Prioridade:** Baixa

O projeto possui retrocompatibilidade para campos introduzidos depois, como:
- `WeeklyRoute.UserName`;
- `requestRevision`.

Antes da entrega, validar que documentos antigos relevantes continuam sendo carregados sem erro e decidir se dados de desenvolvimento devem ser mantidos, migrados ou removidos.

---

## Critério para revisão final

Ao concluir todos os incrementos obrigatórios:

1. revisar todos os itens deste arquivo;
2. classificar em:
   - obrigatório antes da entrega;
   - desejável se houver tempo;
   - evolução futura;
3. resolver primeiro riscos de estabilidade e segurança;
4. revisar regras de negócio que possam gerar inconsistência de dados;
5. depois revisar documentação, testes e qualidade visual;
6. manter como evolução futura tudo que não for necessário para uma demonstração estável e coerente.
