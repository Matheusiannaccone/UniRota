# UniRota — Instruções para agentes de código

## 1. Objetivo atual do projeto

O UniRota é um aplicativo acadêmico de caronas para estudantes da Facens.

O MVP funcional já está concluído e validado no Android. O fluxo consolidado é:

1. Cadastro/Login
2. Rotas semanais com endereços reais
3. Matching determinístico e geográfico
4. Solicitação de carona Once/Weekly
5. Aceite/rejeição e consumo de vagas
6. Rotas confirmadas
7. Preço sugerido
8. Visualização do trajeto em mapa

A fase atual é o **redesign completo da interface e da experiência de uso**, seguindo os mockups aprovados pelo grupo.

Os mockups **não são apenas referências estéticas**. Eles representam decisões de experiência do usuário, hierarquia de informação, navegação e organização das telas. A implementação deve reproduzir sua intenção de UX com fidelidade, adaptando apenas o necessário para respeitar os dados e comportamentos reais do aplicativo.

O objetivo final desta fase é deixar **todas as 13 páginas do aplicativo devidamente estilizadas, visualmente consistentes e sem regressão funcional**.

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

Não criar novas camadas apenas por organização estética.

Não refatorar serviços ou regras de negócio durante tarefas visuais, salvo necessidade concreta para preservar um comportamento existente ou implementar a nova navegação por Shell.

---

## 3. Baseline funcional protegida

A implementação visual deve preservar integralmente os fluxos já validados.

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

Não alterar a fórmula durante o redesign.

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

Mudanças visuais não devem reimplementar essa lógica.

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

## 4. Páginas que fazem parte do redesign

O redesign contempla exatamente estas 13 páginas atuais:

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

A `RouteDetailsPage` não possui mockup próprio porque foi adicionada posteriormente com o incremento de Google Maps.

Seu visual deve ser derivado do mesmo sistema visual e dos padrões das telas de matching aprovadas, sem alterar o funcionamento do mapa.

---

## 5. Navegação — Shell TabBar é requisito desta fase

A área autenticada do UniRota deve adotar **Shell TabBar nativa** como parte da experiência definida nos mockups.

As três áreas principais devem ser:

- **Início** → `HomePage`
- **Rotas** → `MyRoutesPage`
- **Caronas** → `ConfirmedRoutesPage`

`StartupPage`, `LoginPage` e `RegisterPage` permanecem fora da TabBar.

As demais páginas autenticadas devem funcionar como rotas internas/navegação a partir das áreas principais, preservando o fluxo existente:

- `NewRoutePage`
- `FindRidePage`
- `MatchResultsPage`
- `RouteDetailsPage`
- `RideRequestPage`
- `AwaitingApprovalPage`
- `ReceivedRequestsPage`

A migração para TabBar pode exigir alterações em `AppShell.xaml` e `AppShell.xaml.cs`. Essas alterações são permitidas e fazem parte do escopo visual/UX.

Ao implementar:

- usar a navegação nativa do Shell;
- não criar uma barra inferior manual duplicada dentro de cada página;
- manter stacks de navegação coerentes por aba;
- impedir criação indevida de páginas duplicadas;
- evitar perda desnecessária de estado de navegação;
- preservar rotas nomeadas necessárias ao fluxo interno;
- garantir que autenticação e restauração de sessão continuem direcionando o usuário corretamente;
- não duplicar a TabBar em XAML de páginas individuais.

---

## 6. Sistema visual centralizado

A identidade visual existente deve ser **evoluída, não recriada**.

Arquivos centrais atuais:

```text
Resources/Styles/Colors.xaml
Resources/Styles/Styles.xaml
```

`App.xaml` deve continuar carregando os recursos globais.

### Regra principal de reutilização

Todo recurso visual reutilizado em mais de uma página deve ser centralizado.

- Cores e brushes semânticos ficam em `Colors.xaml`.
- **Estilos, dimensões, tipografia, espaçamentos e padrões visuais reutilizáveis ficam centralizados em `Styles.xaml`.**
- Não criar cópias locais do mesmo estilo em diferentes páginas.
- Não espalhar valores repetidos de cor, `CornerRadius`, `Padding`, `FontSize`, alturas ou estados visuais se representam o mesmo componente/padrão.
- Se um padrão visual aparecer em duas ou mais páginas, extrair para `Styles.xaml`.
- Recursos locais de página só são aceitáveis quando forem realmente exclusivos daquela tela.

Priorizar estilos nomeados e semânticos, por exemplo para:

- títulos de página;
- subtítulos;
- texto auxiliar;
- cards;
- cards selecionados;
- campos de formulário;
- botões primários;
- botões secundários;
- botões destrutivos;
- badges/status;
- chips de dias;
- blocos de informação;
- estados vazio/erro/loading;
- espaçamentos recorrentes.

Não criar uma biblioteca de componentes complexa sem necessidade real.

Centralização deve reduzir repetição, não aumentar abstração desnecessariamente.

---

## 7. Cores, tipografia e temas

A paleta existente inspirada na Facens deve ser preservada como base.

Não reintroduzir cores roxas do template MAUI.

Não inserir cores hexadecimais diretamente nas páginas quando já existir ou puder existir um recurso semântico correspondente.

As fontes oficiais definitivas e outros assets de marca ainda não estão disponíveis.

Até que sejam adicionados:

- usar as fontes atualmente configuradas no projeto;
- não baixar ou inventar fontes novas;
- não adicionar logos fictícios;
- não gerar imagens de marca provisórias;
- preparar layouts e espaços de forma que logo/imagens oficiais possam ser substituídos depois sem reestruturar as telas.

---

## 8. Logo, imagens e ícones ainda não oficiais

Outro integrante do grupo fornecerá futuramente o logo e os assets oficiais.

Nesta fase:

- reservar espaços adequados nos layouts quando os mockups exigirem logo ou imagem;
- usar placeholders discretos ou elementos textuais existentes somente quando necessário para manter a composição;
- não criar uma identidade visual definitiva por conta própria;
- não adicionar imagens genéricas ou stock ao repositório;
- manter o espaço adaptável para substituição posterior por `Image`/asset real;
- ícones funcionais podem usar soluções nativas ou já existentes quando necessárias à usabilidade;
- ícones provisórios não devem ser tratados como identidade final do projeto.

O layout deve continuar funcional mesmo sem os assets finais.

---

## 9. Fidelidade aos mockups

Antes de implementar cada bloco visual, ler docs/ui/MOCKUPS.md e consultar somente as imagens correspondentes às páginas daquele bloco. Os mockups são fonte de verdade para UX e composição visual, enquanto o código atual é fonte de verdade para funcionalidades, dados e comportamentos existentes.

Os mockups aprovados orientam:

- hierarquia de informação;
- densidade de conteúdo;
- agrupamento de dados;
- posição relativa das ações;
- navegação;
- prioridade dos botões;
- estrutura de cards;
- estados visuais;
- clareza e simplicidade da experiência.

Quando um mockup mostrar um dado ou recurso que **não existe funcionalmente** no aplicativo, não implementar nova regra de negócio apenas para copiar a imagem.

Exemplos:

- reputação;
- chat;
- notificações reais;
- avaliações;
- funcionalidades ainda não existentes.

Nesses casos, adaptar o layout aos dados reais disponíveis mantendo a intenção visual.

Não remover informações funcionais importantes que foram adicionadas após a criação dos mockups, como:

- distância;
- duração;
- desvio;
- dados geográficos;
- estados reais do fluxo;
- informações necessárias ao funcionamento atual.

---

## 10. Organização da implementação visual

Implementar em blocos para limitar contexto e facilitar validação.

### Bloco 1 — Fundação visual e autenticação

- revisar/evoluir `Colors.xaml` apenas quando necessário;
- consolidar estilos semânticos em `Styles.xaml`;
- preparar placeholders de marca;
- `StartupPage`;
- `LoginPage`;
- `RegisterPage`.

### Bloco 2 — Shell TabBar, Home e Minhas Rotas

- migrar a área autenticada para Shell TabBar;
- Início;
- Rotas;
- Caronas;
- `HomePage`;
- `MyRoutesPage`;
- validar navegação e restauração de sessão.

### Bloco 3 — Criação de rota, busca, matching e mapa

- `NewRoutePage`;
- `FindRidePage`;
- `MatchResultsPage`;
- `RouteDetailsPage`;
- preservar Places, Routes, matching geográfico e Maps SDK.

### Bloco 4 — Solicitações e caronas

- `RideRequestPage`;
- `AwaitingApprovalPage`;
- `ReceivedRequestsPage`;
- `ConfirmedRoutesPage`;
- preservar Once/Weekly, preço, status, vagas e concorrência.

### Fechamento — revisão integrada

- validar as 13 páginas;
- revisar consistência visual;
- revisar TabBar e stacks de navegação;
- verificar textos longos;
- verificar estados vazios;
- verificar estados de erro;
- verificar loading;
- verificar estados disabled;
- testar teclado;
- testar scroll;
- testar telas menores;
- executar suíte completa;
- executar build Android;
- registrar qualquer limitação restante.

Quando a tarefa autorizar explicitamente a implementação completa do redesign, os blocos podem ser executados sequencialmente sem nova autorização. Cada bloco deve ser validado antes do próximo. Se houver falha de build, regressão funcional ou necessidade de decisão de produto não prevista, interromper a execução e solicitar orientação.


## 11. Estratégia de contexto para agentes

O objetivo é reduzir leituras repetidas e evitar análise desnecessária, sem impedir investigação quando realmente necessária.

Para cada bloco:

1. Ler este `AGENTS.md`.
2. Inspecionar o estado atual da branch.
3. Ler `AppShell.xaml` e `AppShell.xaml.cs` somente quando a tarefa envolver navegação.
4. Ler `Colors.xaml` e `Styles.xaml` quando houver impacto em recursos compartilhados.
5. Ler as páginas XAML do bloco atual.
6. Ler code-behind e ViewModel apenas quando necessário para preservar bindings, commands ou navegação.
7. Inspecionar Models/Services somente se um comportamento da tela não puder ser compreendido sem eles.
8. Não fazer auditoria geral do repositório a cada bloco.
9. Reutilizar decisões e estilos já estabelecidos nos blocos anteriores.
10. Evitar reler arquivos grandes que não tenham relação com a tarefa atual.

Se for necessário abrir arquivos fora do conjunto esperado, fazê-lo de forma objetiva e explicar a relação com o bloco atual.

---

## 12. Regras de alteração durante o redesign

Ao trabalhar no código:

- alterar somente arquivos necessários para o bloco atual;
- preservar bindings e commands existentes sempre que possível;
- não mover lógica de negócio para XAML ou code-behind por conveniência visual;
- não refatorar Firebase, Google Maps, matching ou precificação sem necessidade concreta;
- não mudar modelos persistidos apenas para facilitar layout;
- não remover validações existentes;
- não substituir controles funcionais por elementos apenas decorativos;
- não alterar nomes de propriedades apenas por estética;
- não introduzir dependências externas apenas para aparência sem aprovação;
- não inserir segredos/chaves no repositório;
- manter compatibilidade com Android;
- preservar compilação ao final de cada bloco.

Se uma decisão visual exigir mudança funcional ainda não definida, parar e solicitar decisão ao usuário.

---

## 13. Testes e regressão

A última baseline consolidada do incremento Google Maps registrou:

- 154/154 testes .NET aprovados;
- 20/20 testes Node aprovados;
- build Android com 0 erros e 0 avisos;
- fluxo principal validado manualmente em Android.

O redesign não deve reduzir essa baseline sem causa identificada.

Durante os blocos:

- executar testes diretamente relacionados quando houver mudança de navegação ou code-behind;
- validar XAML/build sempre que possível;
- não transformar a fase visual em reescrita dos testes de negócio.

No fechamento:

- executar todos os testes .NET;
- executar todos os testes Node quando houver qualquer impacto em Functions/configuração;
- executar `git diff --check`;
- executar build Android;
- realizar smoke test manual do fluxo completo.

Fluxo mínimo de regressão:

```text
Startup
→ Login/Cadastro
→ Home
→ Minhas Rotas
→ Nova Rota com autocomplete
→ Encontrar Carona
→ Resultados
→ Detalhes no mapa
→ Solicitar carona Once/Weekly
→ Aguardando aprovação
→ Solicitações recebidas
→ Aceitar/Rejeitar
→ Rotas confirmadas
→ Logout/Login
→ Restauração de sessão
```

---

## 14. Fora do escopo desta fase

Não implementar sem solicitação explícita:

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
- recursos de produto não existentes apenas porque aparecem como decoração em mockups.

As pendências funcionais continuam sendo tratadas separadamente em `docs/PENDENCIAS.md`.

---

## 15. Padrão de trabalho recomendado

Para cada bloco:

1. Ler `AGENTS.md` e os arquivos diretamente relacionados.
2. Resumir em poucas linhas os arquivos que pretende alterar.
3. Implementar somente o bloco atual.
4. Centralizar recursos visuais reutilizáveis em `Styles.xaml`.
5. Centralizar cores e brushes em `Colors.xaml`.
6. Preservar toda a funcionalidade existente.
7. Executar validações pertinentes.
8. Informar ao final:
   - arquivos alterados;
   - estilos/recursos compartilhados adicionados;
   - decisões de UX tomadas;
   - testes executados;
   - resultado do build;
   - limitações ou pontos para validação manual.
9. Parar e aguardar aprovação antes do próximo bloco.

---

## 16. Commits

Usar commits pequenos e semânticos, em português, seguindo o padrão do projeto.

Exemplos:

```text
feat(ui): consolida estilos visuais do UniRota
feat(ui): redesenha telas de autenticação
feat(navegacao): adota Shell TabBar na área autenticada
feat(ui): redesenha home e rotas
feat(ui): redesenha fluxo de matching e mapa
feat(ui): redesenha solicitações e caronas
fix(ui): corrige inconsistências visuais e regressões de navegação
```

Não misturar correções funcionais não relacionadas no mesmo commit de UI.

---

## 17. Critério de conclusão do redesign

A fase visual estará concluída quando:

- as 13 páginas estiverem estilizadas de acordo com os mockups e o sistema visual comum;
- a área autenticada utilizar Shell TabBar com Início, Rotas e Caronas;
- recursos visuais repetidos estiverem centralizados, sem duplicação desnecessária por página;
- espaços para logo/imagens oficiais estiverem preparados sem assets fictícios definitivos;
- todas as informações funcionais atuais continuarem acessíveis;
- autocomplete continuar funcionando;
- Google Maps continuar funcionando;
- matching continuar funcionando;
- preço continuar funcionando;
- solicitações continuarem funcionando;
- consumo de vagas continuar funcionando;
- autenticação continuar funcionando;
- não houver regressão conhecida no fluxo principal;
- testes relevantes estiverem aprovados;
- build Android estiver aprovado;
- o aplicativo estiver pronto para receber posteriormente os assets oficiais sem novo redesign estrutural.

---

## Regra de decisão

Antes de qualquer alteração, perguntar:

> Isso é necessário para reproduzir a experiência aprovada nos mockups, centralizar corretamente o sistema visual ou preservar a funcionalidade existente?

Se não, não implementar nesta fase.
