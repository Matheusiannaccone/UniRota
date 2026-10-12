# UniRota — Instruções para agentes de código

## 1. Objetivo atual do projeto

O UniRota é um aplicativo acadêmico de caronas para estudantes da Facens.

O MVP funcional já está concluído, integrado e validado no Android. O fluxo consolidado é:

1. Cadastro/Login
2. Rotas semanais com endereços reais
3. Matching determinístico e geográfico
4. Solicitação de carona Once/Weekly
5. Aceite/rejeição e consumo de vagas
6. Rotas confirmadas
7. Preço sugerido
8. Visualização do trajeto em mapa

O redesign visual principal também já foi concluído e validado.

A fase atual é de **preparação para beta test e validação com usuários reais**.

A prioridade agora é:

- corrigir bugs e comportamentos inconsistentes;
- reduzir atritos de navegação e uso;
- padronizar experiências visuais que ainda estejam inconsistentes;
- melhorar clareza e previsibilidade da interface;
- preservar estabilidade;
- evitar regressões funcionais;
- preparar o aplicativo para uso por beta testers reais.

Não tratar esta fase como uma oportunidade para reescrever arquitetura, expandir escopo ou adicionar recursos grandes sem solicitação explícita.

---

## 2. Stack e arquitetura que devem ser preservadas

- .NET MAUI
- .NET 8
- C#
- XAML
- MVVM simples
- Dependency Injection
- Firebase Authentication
- Cloud Firestore
- Google Places API (New)
- Google Routes API
- Maps SDK for Android
- GitHub para versionamento

Manter um único projeto .NET MAUI e a arquitetura atual.

Não criar novas camadas apenas por organização.

Não refatorar serviços, regras de negócio ou persistência durante tarefas pontuais de UX, navegação ou visual, salvo necessidade concreta e diretamente relacionada à tarefa.

---

## 3. Baseline funcional protegida

A implementação deve preservar integralmente os fluxos já validados.

### Autenticação

Preservar:

- cadastro;
- login;
- restauração/verificação de sessão;
- logout;
- perfil em `users`;
- acesso à área autenticada somente para usuários logados.

### Rotas e Google Maps

Preservar:

- origem e destino com endereços reais;
- autocomplete do Google Places;
- Place IDs;
- invalidação da seleção quando o texto é editado manualmente;
- compatibilidade com rotas legadas;
- cálculo real de distância e duração;
- regras de chamada às APIs externas;
- tratamento de timeout, ausência de rota e erros de rede.

### Matching

Preservar:

- filtros locais baratos antes das chamadas externas;
- papéis opostos;
- exclusão do próprio usuário;
- vagas disponíveis;
- compatibilidade de dia e horário;
- matching geográfico por desvio;
- limites configurados atualmente;
- ordenação dos resultados.

Não voltar para comparação textual simples de origem/destino.

### Precificação

Preservar:

- fórmula atual;
- uso da distância compartilhada calculada;
- `SuggestedPrice` persistido como snapshot;
- ausência de nova chamada ao Google apenas para recalcular preço.

Não alterar a fórmula sem solicitação explícita.

### Solicitações, vagas e concorrência

Preservar:

- Once/Weekly;
- Pending / Accepted / Rejected;
- consumo de vagas;
- rejeição de concorrentes quando aplicável;
- `requestRevision`;
- preconditions / `updateTime`;
- commits atômicos e tentativas limitadas;
- prevenção de inconsistências em concorrência.

Mudanças visuais ou de navegação não devem reimplementar essa lógica.

### Mapa

Preservar na `RouteDetailsPage`:

- mapa;
- polyline;
- pins;
- viewport;
- distância;
- duração;
- desvio;
- estados de loading e fallback.

---

## 4. Fase atual: preparação para beta test

A regra prática desta fase é:

> “Isso melhora a estabilidade, a clareza, a navegação, a experiência de uso ou a validação real do aplicativo?”

Se não, não implementar sem solicitação explícita.

Prioridades:

1. bugs;
2. comportamentos confusos;
3. inconsistências visuais;
4. navegação;
5. validações de formulário;
6. mensagens de erro;
7. estados de loading/vazio;
8. pequenos ajustes de UX;
9. refinamentos visuais de baixo risco.

Não adicionar novas funcionalidades grandes por iniciativa própria.

---

## 5. Páginas atuais do aplicativo

### Inicialização e autenticação

1. `Views/StartupPage.xaml`
2. `Views/Auth/LoginPage.xaml`
3. `Views/Auth/RegisterPage.xaml`

### Área principal e rotas

4. `Views/HomePage.xaml`
5. `Views/Routes/MyRoutesPage.xaml`
6. `Views/Routes/NewRoutePage.xaml`

### Matching e caronas

7. `Views/Matching/FindRidePage.xaml`
8. `Views/Matching/MatchResultsPage.xaml`
9. `Views/Matching/RouteDetailsPage.xaml`
10. `Views/Matching/RideRequestPage.xaml`
11. `Views/Matching/AwaitingApprovalPage.xaml`
12. `Views/Matching/ReceivedRequestsPage.xaml`
13. `Views/Matching/ConfirmedRoutesPage.xaml`

Essas páginas fazem parte do fluxo funcional atual e devem permanecer coerentes visual e funcionalmente.

---

## 6. Navegação principal — Shell TabBar

A área autenticada utiliza **Shell TabBar nativa**.

As três áreas principais são:

- **Início** → `HomePage`
- **Rotas** → `MyRoutesPage`
- **Caronas** → `ConfirmedRoutesPage`

`StartupPage`, `LoginPage` e `RegisterPage` permanecem fora da TabBar.

As demais páginas autenticadas funcionam como rotas internas:

- `NewRoutePage`
- `FindRidePage`
- `MatchResultsPage`
- `RouteDetailsPage`
- `RideRequestPage`
- `AwaitingApprovalPage`
- `ReceivedRequestsPage`

Regras:

- usar a navegação do Shell;
- não criar uma barra inferior manual duplicada;
- manter stacks coerentes por aba;
- impedir criação indevida de páginas duplicadas;
- evitar perda desnecessária de estado;
- preservar rotas nomeadas necessárias;
- preservar autenticação e restauração de sessão;
- não duplicar a TabBar em páginas individuais.

---

## 7. Header autenticado padronizado

Todas as páginas autenticadas devem utilizar um **header visual padronizado e reutilizável**.

A barra visual nativa do Shell não deve ser usada nas páginas autenticadas.

### Home

A `HomePage` deve exibir:

- “UniRota” como identificação do aplicativo;
- ação **Sair**.

A Home:

- não exibe botão Home;
- não exibe botão Voltar;
- é a única página autenticada com ação de logout no header.

### Demais páginas autenticadas

Todas as demais páginas autenticadas devem exibir:

- “UniRota”;
- ação **Home**;
- ação **Voltar**.

### Comportamento do botão Home

O botão Home deve:

- levar o usuário à Home principal;
- utilizar a estrutura atual do Shell;
- evitar criar páginas duplicadas;
- preservar a navegação principal;
- não empilhar novas instâncias da Home.

### Comportamento do botão Voltar

O botão Voltar deve:

- retornar para a tela realmente anterior;
- usar o stack real de navegação;
- não usar destinos fixos específicos por página;
- preservar o comportamento esperado do Shell.

### Título das páginas

O header identifica o aplicativo e controla a navegação.

O título e o subtítulo específicos de cada tela devem permanecer abaixo do header.

### Fluxo não autenticado

`StartupPage`, `LoginPage` e `RegisterPage` não usam esse padrão de header autenticado.

---

## 8. Sistema visual centralizado

Arquivos centrais:

```text
Resources/Styles/Colors.xaml
Resources/Styles/Styles.xaml
```

`App.xaml` deve continuar carregando os recursos globais.

### Regra principal de reutilização

Todo recurso visual reutilizado em mais de uma página deve ser centralizado.

- Cores e brushes semânticos ficam em `Colors.xaml`.
- Estilos, dimensões, tipografia, espaçamentos e padrões visuais reutilizáveis ficam em `Styles.xaml`.
- Não criar cópias locais do mesmo estilo em diferentes páginas.
- Não espalhar valores repetidos de cor, `CornerRadius`, `Padding`, `FontSize`, alturas ou estados visuais quando representam o mesmo padrão.
- Se um padrão aparecer em duas ou mais páginas, centralizar.
- Recursos locais de página só são aceitáveis quando realmente exclusivos.

Exemplos de padrões reutilizáveis:

- títulos de página;
- subtítulos;
- texto auxiliar;
- cards;
- campos de formulário;
- botões primários;
- botões secundários;
- botões destrutivos;
- badges/status;
- chips;
- blocos de informação;
- estados vazio/erro/loading;
- header autenticado.

Centralização deve reduzir repetição sem criar abstração desnecessária.

---

## 9. Cores, tipografia e identidade visual

A paleta atual inspirada na Facens deve ser preservada.

Não reintroduzir cores roxas do template MAUI.

Não inserir cores hexadecimais diretamente nas páginas quando houver ou puder existir recurso semântico correspondente.

Não baixar fontes novas sem solicitação.

Não criar logos fictícios.

Não gerar identidade visual nova por conta própria.

Ícones funcionais podem usar soluções nativas ou assets já existentes quando necessário.

---

## 10. Estratégia de contexto para agentes

O objetivo é reduzir consumo de tokens, leituras repetidas e análise desnecessária.

Para cada tarefa:

1. Ler este `AGENTS.md`.
2. Inspecionar o estado atual da branch.
3. Ler somente os arquivos diretamente relacionados à tarefa.
4. Ler `AppShell.xaml` e `AppShell.xaml.cs` apenas quando a tarefa envolver navegação.
5. Ler `Colors.xaml` e `Styles.xaml` quando houver impacto visual compartilhado.
6. Ler os XAMLs da tarefa.
7. Ler code-behind e ViewModels apenas quando necessário para preservar bindings, commands ou navegação.
8. Inspecionar Models, Services, Firebase, Functions ou lógica de negócio somente quando uma dependência concreta exigir.
9. Não fazer auditoria geral do repositório a cada tarefa.
10. Reutilizar decisões já estabelecidas.
11. Evitar reler arquivos grandes sem relação direta com a tarefa.
12. Não produzir relatórios longos quando um resumo objetivo for suficiente.

Se for necessário abrir arquivos fora do conjunto esperado, fazer isso de forma objetiva.

---

## 11. Regras de alteração

Ao trabalhar no código:

- alterar somente arquivos necessários;
- preservar bindings e commands existentes;
- não mover lógica de negócio para XAML ou code-behind por conveniência;
- não refatorar Firebase, Google Maps, matching ou precificação sem necessidade concreta;
- não mudar modelos persistidos apenas para facilitar UI;
- não remover validações existentes;
- não substituir controles funcionais por elementos apenas decorativos;
- não alterar nomes de propriedades apenas por estética;
- não introduzir dependências externas sem aprovação;
- não inserir segredos/chaves no repositório;
- manter compatibilidade com Android;
- preservar compilação.

Se uma tarefa exigir decisão de produto não definida, interromper e solicitar orientação.

---

## 12. Testes e regressão

Baseline conhecida consolidada:

- 154/154 testes .NET aprovados;
- 20/20 testes Node aprovados;
- build Android com 0 erros e 0 avisos;
- fluxo principal validado manualmente em Android.

As alterações não devem reduzir essa baseline sem causa identificada.

Durante ajustes pontuais:

- executar testes diretamente relacionados à alteração;
- validar build quando houver impacto em XAML, navegação, DI ou code-behind;
- evitar executar suítes irrelevantes apenas por rotina.

Quando a alteração tiver impacto transversal, considerar:

- testes .NET;
- testes Node quando Functions/configuração forem afetadas;
- `git diff --check`;
- build Android;
- smoke test manual.

Fluxo mínimo de regressão quando a navegação for alterada:

```text
Startup
→ Login/Cadastro
→ Home
→ Minhas Rotas
→ Nova Rota
→ Encontrar Carona
→ Resultados
→ Detalhes
→ Solicitar carona
→ Aguardando aprovação
→ Solicitações recebidas
→ Aceitar/Rejeitar
→ Rotas confirmadas
→ Home
→ Voltar
→ Logout/Login
→ Restauração de sessão
```

---

## 13. Fora do escopo sem solicitação explícita

Não implementar por iniciativa própria:

- nova fórmula de preço;
- chat;
- pagamentos;
- reputação;
- notificações reais;
- GPS/localização atual automática;
- rastreamento em tempo real;
- navegação turn-by-turn;
- Machine Learning/IA;
- novas regras de capacidade/vagas;
- cooldown após rejeição;
- cancelamento/restauração de vaga;
- novas coleções Firestore por conveniência visual;
- backend tradicional;
- SQL/PostgreSQL;
- recursos grandes que não sejam necessários para o beta test.

As pendências funcionais continuam separadas em `docs/PENDENCIAS.md`.

---

## 14. Padrão de trabalho recomendado

Para cada tarefa:

1. Ler `AGENTS.md`.
2. Ler somente os arquivos diretamente relacionados.
3. Resumir brevemente o plano quando necessário.
4. Implementar apenas o escopo solicitado.
5. Reutilizar estilos e componentes existentes.
6. Centralizar recursos compartilhados.
7. Preservar a funcionalidade existente.
8. Executar validações pertinentes.
9. Informar ao final, de forma concisa:
   - arquivos alterados;
   - comportamento implementado;
   - testes executados;
   - resultado do build;
   - pontos para validação manual.
10. Encerrar a tarefa sem avançar automaticamente para outras pendências.

---

## 15. Commits

Usar commits pequenos, semânticos e em português.

Exemplos:

```text
fix(navegacao): padroniza header e comportamento de voltar
fix(ui): corrige inconsistências visuais em solicitações
fix(rotas): ajusta validação do formulário de rota
fix(auth): corrige fluxo de logout e restauração de sessão
feat(ui): adiciona ajuste pontual aprovado para beta test
```

Não misturar correções não relacionadas no mesmo commit.

---

## 16. Critério de qualidade da fase atual

O aplicativo está pronto para beta test quando:

- o fluxo principal permanece estável;
- a navegação é previsível;
- headers e ações principais são consistentes;
- não há regressões conhecidas;
- mensagens de erro são compreensíveis;
- estados vazio/loading/erro funcionam;
- telas menores continuam utilizáveis;
- autenticação funciona;
- rotas funcionam;
- matching funciona;
- Google Maps funciona;
- preço funciona;
- solicitações funcionam;
- vagas funcionam;
- caronas confirmadas funcionam;
- logout funciona;
- o aplicativo pode ser usado por um usuário novo sem depender de explicações constantes.

---

## Regra de decisão

Antes de qualquer alteração, perguntar:

> Isso é necessário para corrigir um problema real, reduzir atrito para o beta tester, padronizar a experiência ou preservar a estabilidade do aplicativo?

Se não, não implementar sem solicitação explícita.
