# UniRota — Pendências e fechamento do MVP

Este arquivo registra itens que permaneceram pendentes após a conclusão funcional dos incrementos do MVP do UniRota.

O fluxo principal do MVP está concluído e validado manualmente no Android:

`autenticação → rotas → matching → solicitação Once/Weekly → aceite/rejeição → consumo de vagas → rotas confirmadas → preço sugerido`

As pendências abaixo não exigem novos incrementos funcionais para o MVP. Elas devem ser tratadas conforme a prioridade, principalmente antes de validação com usuários reais ou de uma entrega mais formal.

## Regras de uso

- Priorizar primeiro segurança, consistência de dados e configuração de entrega.
- Não alterar regras funcionais já validadas sem necessidade concreta.
- Manter separado o que é obrigatório antes de usuários reais do que é evolução futura.
- Resolver melhorias visuais e refatorações somente depois dos riscos técnicos prioritários.
- Registrar como evolução futura qualquer item que não seja necessário para uma demonstração estável e coerente.

---

## Situação atual do MVP

**Status funcional:** Concluído  
**Plataforma validada:** Android

### Obrigatório antes de validação com usuários reais

1. Revisar e fechar as Firestore Security Rules.
2. Validar e documentar os índices compostos necessários.
3. Revisar a configuração final do Firebase/Firestore e remover dados de teste ou documentos incompatíveis.
4. Revisar restrições da Web API Key quando aplicável.

### Desejável antes da entrega formal

- revisar identificador definitivo do aplicativo;
- organizar configuração Firebase que ainda está hardcoded;
- executar uma rodada final de testes automatizados e build;
- revisar documentação de configuração e execução;
- aplicar acabamento visual mínimo se houver tempo.

As demais pendências deste documento podem permanecer como evolução futura sem impedir a conclusão do MVP acadêmico atual.

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
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

Campos de criação usam `DateTimeOffset.UtcNow` no dispositivo.

Isso é suficiente para o MVP, mas futuramente pode ser substituído por timestamp gerado pelo servidor/Firestore para maior confiabilidade e ordenação consistente entre dispositivos.

### 5. Tratar conta criada no Authentication sem perfil no Firestore
**Status:** Pendente / evolução futura  
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
- rotas, matching, solicitações e preço sugerido.

### 7. Melhorar acabamento visual das telas
**Status:** Pendente / desejável  
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

### 8. Centralizar estilos e cores do aplicativo
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

Durante o desenvolvimento, parte dos estilos e cores ficou definida localmente nas páginas.

No futuro, pode ser útil criar estilos globais próprios e garantidamente carregados.

**Objetivo futuro:**
- reduzir repetição;
- manter consistência visual;
- facilitar manutenção;
- evitar dependência de estilos do template padrão do MAUI.

### 9. Revisar identificador definitivo do aplicativo
**Status:** Pendente / desejável antes da entrega formal  
**Prioridade:** Média

O projeto ainda utiliza o identificador padrão/placeholder:

`com.companyname.unirota`

Antes de uma distribuição mais formal, esse identificador deve ser substituído por um identificador definitivo.

---

## Incremento 2 — Rotas semanais

### 1. Revisar Firestore Security Rules antes da validação com usuários reais
**Status:** Pendente / obrigatório antes de usuários reais  
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
- campos de preço e distância que não podem ser alterados arbitrariamente;
- campos que não podem ser alterados arbitrariamente pelo cliente.

A implementação cliente já faz verificações de propriedade, mas isso **não substitui** regras de segurança no Firestore.

### 2. Limitar quantidade máxima de vagas oferecidas
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

Atualmente, o cadastro de uma rota de motorista valida apenas que a quantidade de vagas seja maior que zero. Isso permite informar valores incompatíveis com um veículo de passeio, como dezenas ou centenas de vagas.

**Possíveis ações futuras:**
- definir um limite máximo simples, por exemplo entre 1 e 7 vagas;
- validar o limite tanto no ViewModel quanto no serviço;
- futuramente relacionar a quantidade máxima de vagas à capacidade cadastrada do veículo.

### 3. Tratar redução de vagas abaixo da quantidade já ocupada
**Status:** Pendente / evolução futura importante  
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
**Status:** Pendente / evolução futura  
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
**Status:** Pendente / evolução futura  
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
**Status:** Pendente / evolução futura  
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
**Status:** Pendente para revisão técnica antes de uso mais amplo  
**Prioridade:** Média

A criação de `rideRequest` usa commit coordenado com `requestRevision`, e o fluxo atual protege a consistência necessária entre criação e aceite.

Antes de uma versão com uso real em múltiplos dispositivos, revisar especificamente o cenário de duas tentativas simultâneas de criação para o mesmo:

`PassengerRouteId + DriverRouteId`

e confirmar por teste concorrente que nunca permanecem duas solicitações `Pending` equivalentes.

Se necessário, reforçar a estratégia de coordenação sem introduzir uma solução que quebre o fluxo atômico já existente.

### 7. Validar e documentar índices compostos do Firestore
**Status:** Pendente / obrigatório antes de usuários reais  
**Prioridade:** Alta

O fluxo de matching e solicitações utiliza consultas compostas em `rideRequests`, incluindo combinações de:
- `passengerUserId` + `status`;
- `driverUserId` + `status`;
- `driverRouteId` + `status`;
- consultas de solicitações ativas/aceitas.

Dependendo da configuração do projeto, o Firestore pode exigir índices compostos.

**Antes da validação externa:**
- executar todos os fluxos no projeto Firebase real;
- criar os índices exigidos;
- documentar quais índices fazem parte da configuração necessária do UniRota.

### 8. Revisar estratégia de nomes desnormalizados
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

`WeeklyRoute` e `RideRequest` armazenam nomes como snapshots para evitar leituras adicionais.

Alterações futuras no nome do usuário não atualizam automaticamente snapshots históricos.

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
**Status:** Pendente antes de uso mais amplo  
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

## Incremento 4 — Preço sugerido

### 1. Substituir distância manual por distância calculada/geográfica
**Status:** Pendente / evolução futura  
**Prioridade:** Média

No MVP, `EstimatedDistanceKm` é informado manualmente pelo motorista e representa a distância estimada usada no cálculo daquela rota.

Essa decisão evita dependência de mapas, geocodificação ou APIs externas durante o MVP.

**Possíveis evoluções futuras:**
- calcular distância automaticamente a partir de origem/destino;
- integrar serviço de mapas/rotas;
- considerar desvios, pontos de embarque e trajeto real;
- validar limites plausíveis de distância.

### 2. Evoluir o custo por km fixo
**Status:** Pendente / evolução futura  
**Prioridade:** Baixa

O MVP utiliza `CostPerKm = 0,53` como parâmetro fixo e transparente.

Futuramente o valor pode ser:
- configurável;
- atualizado conforme combustível/consumo;
- diferenciado por veículo;
- mantido em configuração externa em vez de constante de código.

O cálculo atual deve permanecer estável enquanto o MVP estiver em validação para garantir reprodutibilidade.

### 3. Evoluir rateio do preço por quantidade de passageiros confirmados
**Status:** Pendente / evolução futura  
**Prioridade:** Média

No MVP, o cálculo considera sempre 2 participantes:

`motorista + 1 passageiro`

Por isso, cada `RideRequest` guarda um snapshot individual do preço sugerido no momento da solicitação.

Futuramente pode ser avaliado um rateio dinâmico entre os passageiros confirmados, mas isso exige definir:
- quando o preço pode mudar;
- como comunicar alterações aos usuários;
- como tratar novos aceites e cancelamentos;
- se solicitações antigas preservam preço original ou são recalculadas.

Não alterar essa regra sem redefinir explicitamente a semântica do snapshot.

### 4. Avaliar persistência de memória de cálculo/`pricingCalculations`
**Status:** Pendente / opcional  
**Prioridade:** Baixa

O MVP não cria coleção `pricingCalculations`.

Atualmente, o necessário para o fluxo é preservado por:
- `WeeklyRoute.EstimatedDistanceKm`;
- `RideRequest.SuggestedPrice` como snapshot.

Uma coleção separada só deve ser criada futuramente se houver necessidade real de:
- auditoria detalhada;
- histórico da fórmula utilizada;
- versionamento de parâmetros;
- métricas de precificação.

### 5. Definir terminologia final de preço/contribuição na interface
**Status:** Pendente / acabamento de produto  
**Prioridade:** Baixa

O MVP usa textos como:
- `Preço sugerido` para passageiro;
- `Valor estimado a receber` para motorista.

Antes de uma publicação mais formal, revisar a terminologia para deixar explícito que o aplicativo apresenta uma **estimativa de contribuição**, não realiza cobrança, pagamento ou garantia de recebimento.

---

## Infraestrutura, entrega e validação

### 1. Revisar configuração final do Firestore antes de usuários reais
**Status:** Pendente / obrigatório antes de usuários reais  
**Prioridade:** Alta

Antes de validação externa, executar uma revisão conjunta de:
- Security Rules;
- índices compostos;
- restrições da Web API Key;
- comportamento das operações atômicas;
- dados antigos ainda existentes no banco;
- documentos de teste;
- permissões efetivas para `users`, `weeklyRoutes` e `rideRequests`;
- proteção dos novos campos `estimatedDistanceKm` e `suggestedPrice`.

### 2. Limpar ou recriar documentos incompatíveis com o modelo atual
**Status:** Pendente / preparação de ambiente  
**Prioridade:** Média

Durante o desenvolvimento foram adicionados campos obrigatórios ao modelo, principalmente:
- `WeeklyRoute.EstimatedDistanceKm` para rotas de motorista;
- `RideRequest.SuggestedPrice`.

Foi decidido não implementar retrocompatibilidade para documentos antigos sem esses campos, pois os dados de teste seriam recriados.

Antes de uma validação externa:
- remover documentos antigos incompatíveis;
- recriar rotas e solicitações necessárias;
- confirmar que não existem dados legados capazes de causar erro de desserialização.

### 3. Rodar validação automatizada final pelo terminal
**Status:** Pendente / desejável antes da entrega  
**Prioridade:** Média

Durante o Incremento 4 a suíte chegou a **91 testes aprovados**, com build Android concluído com **0 erros e 0 avisos**.

Antes da entrega final, repetir:
- `dotnet test` pelo terminal no ambiente confiável;
- build `net8.0-android`;
- verificação de `git status` limpo;
- teste manual curto do fluxo principal.

A divergência conhecida do Test Explorer do Visual Studio deve ser tratada separadamente e não deve levar à remoção ou enfraquecimento dos testes.

### 4. Documentar configuração mínima do projeto Firebase
**Status:** Pendente / desejável antes da entrega  
**Prioridade:** Média

Registrar de forma curta e reproduzível:
- `ProjectId` utilizado/configurável;
- Web API Key e restrições aplicáveis;
- coleções utilizadas;
- Security Rules necessárias;
- índices compostos necessários;
- como preparar um ambiente Firebase novo para o UniRota.

---

## Classificação final

### Obrigatório antes de usuários reais

- Firestore Security Rules;
- índices compostos necessários;
- revisão de permissões e configuração final do Firestore;
- remoção de documentos antigos incompatíveis ou dados de teste inadequados.

### Desejável antes da entrega acadêmica/formal

- repetir testes automatizados e build final;
- revisar restrições da Web API Key;
- organizar configuração Firebase hardcoded;
- revisar identificador definitivo do app;
- documentar configuração mínima do Firebase;
- acabamento visual mínimo, se houver tempo.

### Evolução futura

- cooldown após rejeição;
- cancelamento/desistência;
- notificações;
- histórico;
- capacidade por ocorrência/data;
- limite realista de vagas;
- tratamento de redução de capacidade com passageiros já aceitos;
- timestamps de decisão;
- conclusão/histórico de caronas `Once` passadas;
- distância automática por mapas/API;
- rateio dinâmico do preço;
- custo por km dinâmico;
- `pricingCalculations` para auditoria;
- melhorias estruturais e visuais não necessárias ao MVP.

---

## Critério de encerramento

O MVP funcional pode ser considerado concluído.

Para considerar o ambiente pronto para validação com usuários reais, falta concluir o fechamento de segurança/configuração, com foco principal em **Firestore Security Rules** e **índices**.

Após isso, as demais atividades passam a ser preparação de entrega, documentação, acabamento ou evolução futura, sem necessidade de novo incremento funcional obrigatório.
