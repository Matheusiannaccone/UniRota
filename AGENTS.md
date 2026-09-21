# UniRota — Instruções para agentes de código

## Objetivo do projeto

O UniRota é um aplicativo acadêmico de caronas para estudantes da Facens.

O MVP funcional original foi concluído com o seguinte fluxo:

1. Cadastro/Login
2. Rotas semanais
3. Matching determinístico
4. Solicitação de carona Once/Weekly
5. Aceite/rejeição e consumo de vagas
6. Rotas confirmadas
7. Preço sugerido

A partir deste ponto, o projeto está em evolução funcional. O incremento atual adiciona endereços reais, cálculo de rotas e matching geográfico usando Google Maps Platform.

O foco continua sendo simplicidade, estabilidade, rastreabilidade, baixo acoplamento e coerência com a documentação do projeto.

---

## Stack definida

- .NET MAUI
- .NET 8
- C#
- XAML
- MVVM simples
- Dependency Injection
- Firebase Authentication
- Cloud Firestore
- Google Maps Platform no incremento geográfico
- GitHub para versionamento
- Trello para gestão do projeto

---

## Arquitetura

Manter um único projeto .NET MAUI.

Estrutura principal esperada:

```text
UniRota/
├── Models/
├── Views/
│   ├── Auth/
│   ├── Routes/
│   └── Matching/
├── ViewModels/
├── Services/
│   ├── Interfaces/
│   ├── Firebase/
│   └── GoogleMaps/
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
└── MauiProgram.cs
```

A arquitetura deve permanecer simples e adequada ao estágio atual do projeto.

Não criar novas camadas apenas por organização estética.

---

## Responsabilidades técnicas principais

### Autenticação

- Firebase Authentication
- cadastro
- login
- verificação de sessão
- logout
- acesso à área autenticada apenas para usuários logados
- persistência do perfil básico em `users`

O fluxo atual de autenticação deve ser preservado.

### Rotas semanais

Uma rota semanal deve representar a rotina do usuário e conter, conforme aplicável:

- origem
- destino
- referência geográfica dos endereços
- dias da semana
- horário
- papel do usuário na rota
- vagas, quando motorista
- dados necessários ao cálculo de distância

A persistência continua em `weeklyRoutes`.

### Matching

O matching continua determinístico e implementado em C#.

O incremento atual substitui a comparação textual de origem/destino por análise geográfica.

O fluxo esperado deve preservar primeiro os filtros baratos já existentes:

- candidato deve ser motorista
- candidato não pode pertencer ao mesmo usuário
- deve possuir vaga disponível
- deve possuir ao menos um dia compatível
- diferença de horário deve permanecer dentro da tolerância definida

Somente depois desses filtros devem ser executados cálculos geográficos.

A compatibilidade final poderá considerar:

- distância da rota original do motorista
- duração da rota original
- distância da rota com passagem pelo passageiro
- duração da rota compartilhada
- desvio adicional em quilômetros
- desvio adicional em minutos

Evitar chamadas desnecessárias à API do Google.

### Precificação

A precificação deve continuar:

- implementada em C#
- simples
- transparente
- reproduzível
- desacoplada do fornecedor de mapas

A fórmula atual não deve ser alterada durante o incremento geográfico sem solicitação explícita.

A principal mudança deste incremento é substituir a distância informada manualmente por distância calculada.

O `SuggestedPrice` persistido na solicitação deve continuar funcionando como snapshot.

### Solicitações e vagas

O fluxo já validado deve ser preservado:

- criação de solicitação
- Once/Weekly
- Pending
- Accepted
- Rejected
- consumo de vagas
- rejeição de concorrentes quando aplicável
- `requestRevision`
- prevenção de inconsistências em concorrência

O incremento de mapas não deve alterar essa lógica sem necessidade concreta e aprovação explícita.

---

## Incremento atual — Google Maps e matching geográfico

### Objetivo

Permitir que o usuário selecione endereços reais e que o UniRota determine compatibilidade de carona com base em trajetos reais.

Fluxo esperado:

```text
Usuário digita endereço
    ↓
Google Places sugere endereços
    ↓
Usuário seleciona endereço válido
    ↓
Rota salva com referência geográfica
    ↓
Passageiro procura carona
    ↓
Filtros determinísticos locais
    ↓
Google Routes calcula trajetos necessários
    ↓
UniRota calcula distância, duração e desvio
    ↓
Candidatos fora dos limites são removidos
    ↓
Resultados são ordenados
    ↓
Passageiro solicita carona
    ↓
Preço usa distância calculada
```

---

## Google Maps Platform

O provedor inicial aprovado é Google Maps Platform.

Serviços previstos:

- Places API (New) para pesquisa/autocomplete de endereços
- Routes API para cálculo de distância, duração e trajeto
- Maps SDK apenas quando houver necessidade de visualização do mapa

Não implementar Navigation SDK ou navegação turn-by-turn.

### Abstração

O restante do aplicativo não deve depender diretamente de classes específicas do Google.

Preferir interfaces como:

```text
IPlaceService
IMapRouteService
```

e implementações específicas em:

```text
Services/GoogleMaps/
```

Isso deve permitir futura substituição do fornecedor com impacto reduzido.

---

## Endereços e Place IDs

As rotas novas devem utilizar endereços selecionados por autocomplete, e não apenas texto digitado livremente.

Adicionar e preservar referências geográficas adequadas, preferencialmente Place IDs para origem e destino.

Exemplo conceitual:

```text
Origin
OriginPlaceId

Destination
DestinationPlaceId
```

O texto continua útil para apresentação ao usuário.

O código deve tratar como inválida uma seleção quando o usuário editar manualmente o texto depois de escolher um resultado.

Rotas antigas sem referência geográfica devem continuar legíveis e não devem causar falha na aplicação.

Não tentar inferir silenciosamente Place IDs de textos antigos ambíguos.

---

## Distância e duração

A distância informada manualmente pelo motorista deve ser substituída por cálculo real.

Durante a migração, `EstimatedDistanceKm` pode ser preservado como propriedade existente para evitar mudanças desnecessárias em precificação, Firestore e testes, mas seu valor deverá passar a ser derivado do serviço de rotas.

Não renomear essa propriedade apenas por estética durante este incremento.

---

## Desvio da rota

A compatibilidade geográfica deve comparar:

```text
Rota original:
motorista origem → motorista destino
```

com:

```text
Rota compartilhada:
motorista origem
→ passageiro origem
→ passageiro destino
→ motorista destino
```

Calcular pelo menos:

```text
DetourDistanceKm
DetourDurationMinutes
```

Evitar waypoints duplicados quando motorista e passageiro compartilharem origem ou destino.

Os limites máximos de desvio devem ficar centralizados/configuráveis e não espalhados como números mágicos pelo código.

---

## Segurança de API

Não expor no repositório chaves secretas ou chaves de web service sem restrição adequada.

Não adicionar uma chave de Google Routes/Places diretamente ao código-fonte apenas para facilitar testes.

Caso seja necessário proteger chamadas de web service, uma função serverless mínima no ecossistema Firebase/Google Cloud está aprovada para este incremento.

Essa função deve:

- ser pequena
- ter propósito específico
- validar usuário quando aplicável
- expor somente operações necessárias
- não funcionar como proxy genérico para qualquer chamada Google
- manter segredos fora do aplicativo e do GitHub

Não transformar essa necessidade em backend tradicional, microserviço ou API genérica.

---

## Firestore

Coleções atuais principais:

- `users`
- `weeklyRoutes`
- `rideRequests`

Preservar o modelo atual sempre que possível.

Novos campos de localização devem ser adicionados somente onde realmente necessários.

Não criar novas coleções apenas para cache sem necessidade concreta.

Ao alterar `weeklyRoutes`:

- preservar leitura de documentos antigos quando possível
- validar novos campos
- atualizar serialização e desserialização
- revisar Firestore Security Rules
- não quebrar `requestRevision`

---

## Compatibilidade com dados legados

Documentos antigos podem não possuir campos geográficos.

O aplicativo deve:

- continuar conseguindo carregar essas rotas
- identificá-las como não preparadas para matching geográfico
- orientar o usuário a editar e selecionar endereços válidos
- não geocodificar automaticamente entradas ambíguas sem interação do usuário

Migrações destrutivas devem ser evitadas.

---

## Ordem de desenvolvimento do incremento atual

Implementar em blocos verticais.

### Bloco 1 — Fundação geográfica

- atualizar modelos
- adicionar campos de Place ID
- adaptar persistência Firestore
- criar modelos/interfaces geográficos
- manter compatibilidade com documentos antigos
- não chamar Google ainda

### Bloco 2 — Endereços reais

- integrar autocomplete
- selecionar endereço
- validar seleção
- salvar Place IDs
- tratar edição do texto após seleção

### Bloco 3 — Distância e duração

- integrar Routes API
- obter distância e duração reais
- remover entrada manual de distância
- preencher a distância calculada
- tratar falhas da API

### Bloco 4 — Matching geográfico

- preservar filtros determinísticos atuais
- remover igualdade textual de origem/destino
- calcular rota original e compartilhada
- calcular desvio
- aplicar limites configuráveis
- enriquecer `MatchResult`
- ordenar resultados

### Bloco 5 — Precificação e solicitação

- usar distância calculada
- preservar fórmula de preço
- preservar snapshot de `SuggestedPrice`
- preservar fluxo de `rideRequests`, vagas e concorrência

### Bloco 6 — Visualização no mapa

- adicionar mapa apenas depois da lógica estar funcional
- mostrar rota e pontos relevantes
- exibir distância, duração e desvio
- não implementar navegação turn-by-turn

### Bloco 7 — Robustez e fechamento

- erros de rede
- timeout
- quota
- respostas sem rota
- dados legados
- testes
- build Android
- documentar a revisão separada das Firestore Rules, sem alterá-las neste bloco
- documentação

Não avançar automaticamente de um bloco para outro sem solicitação explícita.

---

## Itens fora do escopo deste incremento

Não implementar sem solicitação explícita:

- GPS/localização atual automática
- rastreamento em tempo real
- compartilhamento de localização ao vivo
- navegação turn-by-turn
- chat
- pagamento
- reputação
- notificações de aproximação
- múltiplos passageiros otimizados na mesma rota
- alteração da fórmula de preço
- Machine Learning
- IA
- microserviços
- backend tradicional
- SQL/PostgreSQL

---

## Regra para chamadas externas

Chamadas faturáveis ou sujeitas a quota devem ser feitas somente quando necessárias.

Preferir:

```text
filtros locais baratos
↓
redução de candidatos
↓
chamadas externas
```

Não consultar Google para candidatos que já seriam descartados por papel, usuário, vaga, dia ou horário.

---

## Tratamento de erros

Serviços externos devem tratar, pelo menos:

- cancelamento
- timeout
- ausência de conexão
- resposta inválida
- rota inexistente
- quota/rate limit
- erro 4xx relevante
- erro 5xx

Falhas do Google não devem corromper dados existentes.

Mensagens apresentadas ao usuário devem ser simples e úteis.

---

## Testes

Alterações relevantes devem incluir ou atualizar testes automatizados quando a lógica puder ser testada sem dependência real da rede.

Usar mocks/stubs/fakes para serviços externos.

Cobrir, conforme o bloco:

- Place IDs
- validação de seleção
- serialização Firestore
- documentos legados
- parsing de rotas
- cálculo de desvio
- filtros do matching
- limites
- ordenação
- precificação
- preservação das regras já existentes

Não transformar testes unitários em testes que dependam de chamadas reais ao Google.

---

## Regras para alterações

Ao trabalhar no código:

- ler este arquivo antes de alterar o projeto
- inspecionar o estado atual da branch
- alterar somente arquivos necessários para o bloco atual
- não fazer refatorações fora do escopo
- não adicionar abstrações sem necessidade real
- preservar todos os fluxos já validados
- preservar compilação sempre que possível
- não avançar para o bloco seguinte automaticamente
- não inserir chaves secretas no repositório
- informar claramente qualquer configuração manual necessária

Se uma alteração exigir mudar uma decisão funcional ainda não definida, parar e solicitar a decisão ao usuário em vez de inventar uma regra.

---

## Regra de decisão

Antes de adicionar qualquer recurso, perguntar:

> Isso é necessário para o bloco atual ou para preservar corretamente o fluxo existente?

Se não, não implementar agora.

---

## Padrão de trabalho recomendado para agentes

Para cada bloco:

1. Ler o estado atual da branch.
2. Ler este `AGENTS.md`.
3. Identificar exatamente os arquivos relacionados ao bloco.
4. Propor um plano curto antes de alterar.
5. Implementar somente o escopo aprovado.
6. Atualizar/adicionar testes pertinentes.
7. Executar os testes relacionados.
8. Executar build quando o ambiente permitir.
9. Informar:
   - arquivos alterados
   - decisões tomadas
   - testes executados
   - resultado do build
   - limitações
   - etapas manuais necessárias
10. Parar e aguardar aprovação antes de avançar para outro bloco.

---

## Commits

Usar commits pequenos e semânticos, em português, seguindo o padrão do projeto.

Exemplos:

```text
feat(mapas): adiciona estrutura de endereços geográficos
feat(mapas): integra autocomplete de endereços
feat(rotas): calcula distância e duração reais
feat(matching): adiciona compatibilidade por desvio de rota
feat(preco): usa distância calculada na precificação
feat(mapas): adiciona visualização do trajeto
test(mapas): adiciona cenários geográficos
docs(mapas): documenta integração com Google Maps
```

---

## Plataformas prioritárias

O UniRota é mobile-first.

Prioridades:

1. Android — plataforma principal de desenvolvimento e demonstração.
2. iOS — plataforma alvo e deve permanecer compatível quando possível.
3. Windows/MacCatalyst — não são prioridade.

Não adicionar complexidade apenas para suportar desktop.

A visualização e interação devem ser projetadas prioritariamente para smartphones.

---

## Definição de pronto de cada bloco

Um bloco só está concluído quando:

- o código do escopo foi implementado
- fluxos existentes relacionados continuam funcionando
- testes pertinentes passam
- o projeto compila no ambiente disponível
- erros principais do bloco são tratados
- nenhuma chave sensível foi adicionada ao repositório
- alterações estão pequenas o suficiente para revisão
- o agente descreveu claramente o que mudou
- nenhuma etapa do bloco seguinte foi iniciada
