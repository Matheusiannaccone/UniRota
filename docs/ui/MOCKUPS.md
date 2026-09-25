# UniRota — Especificação dos Mockups de Interface

## 1. Objetivo deste documento

Este documento descreve os mockups aprovados do UniRota e deve ser utilizado como referência de UX, composição visual e hierarquia de informação durante o redesign.

Os mockups não são apenas referências estéticas. Eles definem a intenção de experiência do usuário, incluindo:

- hierarquia das informações;
- agrupamento dos conteúdos;
- prioridade e posição das ações;
- densidade visual;
- navegação principal;
- estrutura de cards;
- estados visuais;
- organização das telas.

O código existente continua sendo a fonte de verdade para:

- funcionalidades;
- regras de negócio;
- bindings e Commands;
- dados realmente disponíveis;
- estados do fluxo;
- Firebase;
- Google Maps;
- matching;
- precificação;
- solicitações e vagas.

Quando houver diferença entre mockup e implementação funcional atual:

1. preservar a funcionalidade real;
2. preservar a intenção de UX do mockup;
3. adaptar o layout aos dados atuais;
4. não inventar nova regra de negócio somente para reproduzir um elemento visual.

As imagens de referência devem ser armazenadas em:

```text
docs/ui/mockups/
```

com os nomes originais usados neste documento.

---

## 2. Mapeamento entre mockups e páginas reais

> **Atenção:** os nomes dos arquivos `07_FindRide.png` e `12_ConfirmedRoutes.png` não correspondem visualmente às páginas sugeridas pelos nomes.

O mapeamento correto para a implementação deve ser:

| Mockup | Conteúdo visual | Página XAML correspondente |
|---|---|---|
| `01_Home.png` | Tela inicial autenticada | `Views/HomePage.xaml` |
| `02_Startup.png` | Inicialização/splash interno | `Views/StartupPage.xaml` |
| `03_Login.png` | Login | `Views/Auth/LoginPage.xaml` |
| `04_Register.png` | Cadastro | `Views/Auth/RegisterPage.xaml` |
| `05_MyRoutes.png` | Minhas rotas | `Views/Routes/MyRoutesPage.xaml` |
| `06_NewRoute.png` | Nova rota | `Views/Routes/NewRoutePage.xaml` |
| `07_FindRide.png` | **Suas caronas / próximas e recorrentes** | `Views/Matching/ConfirmedRoutesPage.xaml` |
| `08_MatchResults.png` | Motoristas disponíveis | `Views/Matching/MatchResultsPage.xaml` |
| `09_RideRequest.png` | Confirmar solicitação de carona | `Views/Matching/RideRequestPage.xaml` |
| `10_AwaitingApproval.png` | Aguardando aceite | `Views/Matching/AwaitingApprovalPage.xaml` |
| `11_ReceivedRequests.png` | Solicitações recebidas | `Views/Matching/ReceivedRequestsPage.xaml` |
| `12_ConfirmedRoutes.png` | **Encontrar carona / selecionar rota** | `Views/Matching/FindRidePage.xaml` |

A `Views/Matching/RouteDetailsPage.xaml` é a 13ª página do redesign e não possui mockup próprio. Sua aparência deve ser derivada do sistema visual descrito neste documento e, principalmente, dos padrões usados em `08_MatchResults.png`, preservando integralmente o funcionamento do mapa.

---

# 3. Padrões globais observados nos mockups

## 3.1 Estrutura geral

As telas utilizam uma composição mobile limpa, com bastante espaço em branco e baixa densidade visual.

Padrões recorrentes:

- fundo predominantemente branco;
- conteúdo principal com margens laterais consistentes;
- títulos grandes e fortes;
- textos auxiliares curtos em cinza;
- cards com cantos arredondados;
- divisões por blocos claros;
- azul como cor principal de ação;
- cores semânticas suaves para status;
- poucos elementos decorativos;
- foco em leitura rápida;
- botões principais largos e fáceis de tocar.

A interface deve priorizar clareza e legibilidade em vez de preencher a tela com informações.

---

## 3.2 Cabeçalho autenticado

Nas telas principais autenticadas, o topo segue aproximadamente este padrão:

- marca UniRota à esquerda;
- ação de notificação à direita;
- avatar circular do usuário no canto direito;
- espaçamento confortável em relação ao conteúdo abaixo.

Como logo e imagens oficiais ainda não existem:

- reservar o espaço correspondente à marca;
- usar placeholder discreto ou representação textual temporária;
- não criar logo definitivo;
- estruturar o layout de forma que um `Image` possa substituir o placeholder sem reorganização da tela.

A presença visual de notificação nos mockups não significa que notificações reais devam ser implementadas.

---

## 3.3 Shell TabBar

A área autenticada deve utilizar Shell TabBar nativa com três destinos principais:

1. **Início**
2. **Rotas**
3. **Caronas**

Nos mockups:

- a aba ativa usa azul;
- a aba inativa usa tom neutro;
- ícone e texto identificam cada destino;
- a barra permanece simples, sem decoração excessiva.

Não reproduzir a TabBar manualmente dentro das páginas.

---

## 3.4 Cards

Cards aparecem em praticamente todo o fluxo e devem compartilhar um mesmo padrão visual base.

Características recorrentes:

- fundo branco ou tonalidade muito clara;
- borda sutil;
- cantos arredondados;
- padding interno consistente;
- pequena separação vertical entre cards;
- uso de cor suave para diferenciar tipos ou estados;
- informações organizadas por grupos, não como texto corrido.

Quando houver cards selecionáveis, o selecionado deve possuir diferenciação visual clara, preferencialmente:

- borda azul;
- fundo levemente azulado;
- indicador de seleção.

---

## 3.5 Tipografia

A hierarquia aproximada é:

1. título principal da página;
2. subtítulo/instrução;
3. título de seção;
4. título de card;
5. informação primária;
6. informação secundária/auxiliar.

Até a chegada das fontes oficiais:

- usar somente as fontes já configuradas no projeto;
- centralizar tamanhos e pesos reutilizáveis em `Styles.xaml`;
- evitar tamanhos arbitrários repetidos diretamente nas páginas.

---

## 3.6 Botões

### Botão primário

Padrão recorrente:

- largura ampla;
- fundo azul;
- texto branco;
- cantos arredondados;
- altura confortável;
- ação principal facilmente identificável.

### Botão secundário

Usado para ações menos prioritárias:

- fundo branco ou transparente;
- borda ou texto azul;
- mesmo raio visual do botão principal.

### Ações destrutivas

Exemplo: recusar solicitação.

Devem usar linguagem e cor de erro de forma moderada, sem competir visualmente com a ação principal positiva.

---

## 3.7 Estados semânticos

Os mockups usam cores leves para comunicar estado:

- azul: ação, seleção, informação;
- verde: confirmado, compatível, valor ou sucesso;
- amarelo/laranja: aguardando;
- vermelho: recusa/erro;
- cinza: conteúdo secundário e estados neutros.

Essas cores devem utilizar recursos centralizados em `Colors.xaml`.

---

# 4. Mockup 01 — Home

**Arquivo:** `docs/ui/mockups/01_Home.png`  
**Página:** `Views/HomePage.xaml`  
**Aba:** Início

## Objetivo

Dar ao usuário uma visão rápida da situação atual da conta e oferecer acesso imediato à principal ação do aplicativo: encontrar uma carona.

## Hierarquia visual

### 1. Cabeçalho

Topo com:

- marca UniRota;
- ícone/ação de notificações;
- avatar circular com inicial do usuário.

### 2. Saudação

Título destacado:

```text
Olá, Matheus 👋
```

Abaixo, uma frase curta:

```text
Para onde vamos hoje?
```

O nome deve utilizar o dado real do usuário quando disponível.

### 3. Card principal — Encontrar carona

É o elemento de maior destaque da tela.

Contém:

- ícone de carro;
- título `Encontrar carona`;
- pequena descrição;
- indicação visual de acesso;
- botão `Buscar caronas`.

A ação deve utilizar o fluxo real que leva o usuário à seleção de uma rota de passageiro para realizar matching.

### 4. Seção “Suas atividades”

Título de seção:

```text
Suas atividades
```

Grid 2 × 2 com cards-resumo.

O mockup exemplifica:

- Solicitações recebidas;
- Aguardando;
- Confirmadas;
- Minhas rotas.

Cada card mostra:

- ícone;
- número;
- descrição curta;
- cor semântica suave.

Não criar contadores fictícios. Se os dados atuais não disponibilizarem algum contador de forma simples e segura, adaptar o conteúdo mantendo a estrutura visual sem introduzir nova lógica de negócio desnecessária.

### 5. Próximo trajeto

Card discreto abaixo das atividades.

Exemplo do mockup:

```text
Seu próximo trajeto
Nenhuma carona agendada
```

Usar apenas estados que possam ser determinados com os dados atuais.

### 6. TabBar

Aba `Início` ativa.

## Intenção de UX

A Home deve ser um dashboard leve. O usuário precisa identificar rapidamente:

- o que pode fazer agora;
- se possui solicitações;
- se há algo aguardando;
- onde acessar suas rotas e caronas.

Evitar transformar a Home em uma listagem completa de dados.

---

# 5. Mockup 02 — Startup

**Arquivo:** `docs/ui/mockups/02_Startup.png`  
**Página:** `Views/StartupPage.xaml`

## Objetivo

Comunicar identidade e carregamento enquanto o aplicativo verifica o estado inicial e a sessão do usuário.

## Composição

Layout muito simples e centralizado:

- grande área em branco;
- espaço reservado para logo/ícone UniRota;
- nome `UniRota`;
- slogan `Conectando trajetos`;
- indicador de carregamento;
- detalhe discreto no rodapé relacionado à comunidade Facens;
- formas decorativas leves nas extremidades.

## Assets

Como o logo oficial ainda não existe:

- reservar a área central;
- não adicionar imagem stock;
- não criar logo definitivo;
- permitir substituição posterior por asset real.

As formas decorativas não são obrigatórias se exigirem assets específicos. Podem ser reproduzidas com elementos simples, desde que não compliquem a implementação.

## Funcionalidade protegida

Não alterar:

- verificação/restauração de sessão;
- redirecionamento para login;
- redirecionamento para área autenticada;
- inicialização de dependências.

## Intenção de UX

A tela deve transmitir carregamento calmo e rápido, sem controles desnecessários.

---

# 6. Mockup 03 — Login

**Arquivo:** `docs/ui/mockups/03_Login.png`  
**Página:** `Views/Auth/LoginPage.xaml`

## Objetivo

Permitir entrada simples e direta no aplicativo.

## Hierarquia

### 1. Marca

Área superior reservada para marca UniRota.

### 2. Título

```text
Bem-vindo
```

### 3. Texto auxiliar

Exemplo:

```text
Acesse sua conta para continuar
sua jornada na Facens.
```

### 4. Campos

Dois campos principais:

- E-mail
- Senha

Cada campo aparece dentro de container visual arredondado com:

- ícone contextual à esquerda;
- label/placeholder;
- entrada;
- ação de visualizar/ocultar senha quando esse comportamento já existir.

### 5. Recuperação de senha

O mockup mostra:

```text
Esqueci minha senha
```

Não implementar recuperação de senha somente porque aparece visualmente caso a funcionalidade ainda não exista.

### 6. Botão principal

```text
Entrar →
```

Botão azul amplo.

### 7. Cadastro

Texto auxiliar:

```text
Ainda não tenho conta? Criar conta
```

`Criar conta` deve continuar navegando para `RegisterPage`.

### 8. Card institucional

Na parte inferior, um pequeno bloco informativo:

```text
Na mesma direção
Conectando você a uma vida acadêmica
mais prática e organizada.
```

Esse bloco é informativo e pode ser adaptado sem criar funcionalidade.

## Intenção de UX

Minimizar distrações. A ação dominante deve ser entrar no aplicativo.

---

# 7. Mockup 04 — Register

**Arquivo:** `docs/ui/mockups/04_Register.png`  
**Página:** `Views/Auth/RegisterPage.xaml`

## Objetivo

Permitir criação de conta com sequência clara e compacta.

## Estrutura

### 1. Topo

- ação de voltar;
- espaço/marca UniRota.

### 2. Título

```text
Criar conta
```

### 3. Instrução

```text
Faça parte do UniRota e organize
seus deslocamentos à Facens.
```

### 4. Campos

Ordem indicada:

1. Nome completo
2. E-mail
3. Senha
4. Confirmar senha

Campos seguem o mesmo padrão visual da tela de login.

O mockup mostra orientação de quantidade mínima de caracteres na senha. Exibir apenas validações coerentes com as regras reais já implementadas.

### 5. Botão principal

```text
Criar conta
```

### 6. Login

Ação secundária:

```text
Já tenho uma conta
```

## Intenção de UX

Manter o cadastro curto, sem solicitar informações adicionais que não sejam necessárias no modelo atual.

---

# 8. Mockup 05 — Minhas Rotas

**Arquivo:** `docs/ui/mockups/05_MyRoutes.png`  
**Página:** `Views/Routes/MyRoutesPage.xaml`  
**Aba:** Rotas

## Objetivo

Listar as rotas semanais cadastradas pelo usuário e deixar claro o papel desempenhado em cada uma.

## Estrutura

### 1. Cabeçalho autenticado

- marca;
- notificações;
- avatar.

### 2. Título e ação

Título:

```text
Minhas rotas
```

Subtítulo:

```text
Suas rotas ativas e agendadas.
```

À direita, botão compacto:

```text
+ Nova rota
```

### 3. Cards de rota

Cada rota possui card próprio.

O mockup diferencia visualmente:

- Motorista;
- Passageiro.

### Informações do card

- badge/papel;
- menu contextual de três pontos;
- origem;
- destino;
- representação vertical do trajeto;
- dias da semana;
- horário de saída;
- vagas disponíveis quando motorista.

Informações adicionadas pelo sistema atual, como estado geográfico ou outras informações relevantes, podem ser incluídas se necessárias, sem aumentar excessivamente a densidade.

### 4. TabBar

Aba `Rotas` ativa.

## Intenção de UX

Permitir que o usuário reconheça rapidamente:

- qual rota está vendo;
- se é motorista ou passageiro;
- quando ocorre;
- quantas vagas existem, quando aplicável.

---

# 9. Mockup 06 — Nova Rota

**Arquivo:** `docs/ui/mockups/06_NewRoute.png`  
**Página:** `Views/Routes/NewRoutePage.xaml`

## Objetivo

Transformar o cadastro de rota em um formulário sequencial e compreensível, sem remover as funcionalidades atuais de Google Places e Routes.

## Estrutura por seções

### 1. Topo

- ação de voltar;
- título `Nova rota`.

### 2. Seção “1. Como você vai?”

Dois cards de seleção:

- Motorista
- Passageiro

O selecionado deve possuir destaque visual claro.

As descrições curtas ajudam a explicar cada papel.

### 3. Seção “2. Trajeto”

Campos:

- Origem
- Destino

Cada campo aparece como bloco clicável/entrada com ícone.

A implementação real deve preservar:

- autocomplete do Google Places;
- Place IDs;
- seleção válida;
- invalidação após edição manual;
- tratamento de dados legados.

O texto ilustrativo do mockup é apenas exemplo.

### 4. Seção “3. Quando?”

Seleção de dias da semana com chips compactos:

```text
Seg Ter Qua Qui Sex Sáb Dom
```

Abaixo, seleção do horário de saída.

### 5. Seção “4. Detalhes”

O mockup antigo mostra:

- vagas disponíveis;
- distância aproximada.

Na implementação atual, **a distância não deve voltar a ser informada manualmente**.

A distância real é calculada pelo serviço de rotas.

Portanto:

- preservar controle de vagas para motorista;
- apresentar distância calculada apenas se fizer sentido no fluxo atual;
- não recriar entrada manual de distância.

### 6. Botão principal

```text
Salvar rota
```

## Intenção de UX

O formulário deve parecer um fluxo de quatro etapas dentro da mesma página, com separação visual clara entre decisões.

## Cuidados técnicos

Esta é uma das telas mais sensíveis do redesign.

Não quebrar:

- sugestões de endereço;
- debounce;
- cancelamento;
- Place IDs;
- validação;
- cálculo de distância/duração;
- edição de rotas;
- persistência Firestore.

---

# 10. Mockup 07 — Suas Caronas

**Arquivo físico:** `docs/ui/mockups/07_FindRide.png`  
**Página real:** `Views/Matching/ConfirmedRoutesPage.xaml`  
**Aba:** Caronas

> O nome do arquivo é histórico e não representa o conteúdo visual atual.

## Objetivo

Apresentar caronas já confirmadas/agendadas de forma simples, separando ocorrências próximas de recorrências.

## Estrutura

### 1. Cabeçalho autenticado

Marca, notificações e avatar.

### 2. Título

```text
Suas caronas
```

Subtítulo:

```text
Acompanhe suas próximas viagens e
recorrências.
```

### 3. Controle segmentado

Duas opções:

- Próximas
- Recorrentes

A opção ativa usa destaque azul.

Se o modelo atual não permitir exatamente essa separação em todos os casos, adaptar a apresentação aos estados realmente existentes, sem inventar persistência nova.

### 4. Cards de carona

#### Próxima carona

Exibe:

- badge `Próxima carona`;
- origem → destino;
- data;
- horário;
- papel do usuário;
- nome da outra pessoa quando disponível;
- valor;
- menu contextual quando existir ação correspondente.

#### Carona recorrente

Exibe:

- badge de recorrência;
- origem → destino;
- dias;
- horário;
- papel;
- outra pessoa relacionada.

### 5. TabBar

Aba `Caronas` ativa.

## Intenção de UX

Esta é a área principal para visualizar compromissos já confirmados, não para buscar novos motoristas.

---

# 11. Mockup 08 — Motoristas Disponíveis

**Arquivo:** `docs/ui/mockups/08_MatchResults.png`  
**Página:** `Views/Matching/MatchResultsPage.xaml`

## Objetivo

Exibir resultados de matching de modo comparável e permitir que o passageiro escolha um motorista.

## Estrutura

### 1. Topo

- ação de voltar;
- título `Motoristas disponíveis`.

### 2. Resumo da rota pesquisada

Card compacto no topo com:

- identificação da rota;
- origem → destino;
- dia;
- horário;
- acesso visual aos detalhes.

### 3. Cards de motorista

Cada resultado ocupa um card próprio.

O mockup apresenta:

- avatar;
- nome;
- avaliação;
- badge de compatibilidade;
- origem;
- destino;
- dias;
- saída;
- vagas;
- botão `Solicitar`.

### Adaptação obrigatória ao modelo real

Não implementar sistema de avaliações/reputação se ele não existir.

A área destinada à avaliação pode:

- ser omitida;
- ser substituída por informação real relevante;
- permanecer estruturalmente reservada apenas se isso não causar informação fictícia.

Os resultados atuais possuem informações geográficas que não existiam originalmente nos mockups, como:

- distância;
- duração;
- desvio em km;
- desvio em minutos;
- dados da rota compartilhada.

Esses dados não devem ser removidos se forem importantes ao usuário. Devem ser integrados ao card ou à tela de detalhes sem aumentar excessivamente a densidade.

### 4. Ação principal

Cada card possui `Solicitar`.

O botão deve levar ao fluxo real de `RideRequestPage`.

### 5. TabBar

Aba `Caronas` pode permanecer visível conforme o comportamento final definido pelo Shell para rotas internas.

## Intenção de UX

Facilitar comparação entre opções sem transformar o resultado em uma tabela técnica.

As informações mais importantes devem ser:

- motorista;
- trajeto;
- compatibilidade;
- horário;
- vagas;
- impacto/desvio relevante;
- ação de solicitar.

---

# 12. Mockup 09 — Confirmar Carona

**Arquivo:** `docs/ui/mockups/09_RideRequest.png`  
**Página:** `Views/Matching/RideRequestPage.xaml`

## Objetivo

Permitir que o passageiro revise a opção escolhida e defina se a solicitação é pontual ou semanal antes de confirmar.

## Estrutura

### 1. Topo

- voltar;
- título `Confirmar carona`.

### 2. Resumo da carona

Card principal contendo:

- motorista;
- trajeto;
- horário;
- preço/contribuição por passageiro.

O preço mostrado deve utilizar o valor real calculado pelo fluxo atual.

### 3. Seção “Como deseja viajar?”

Cards de seleção:

- `Uma única vez`
- `Toda semana`

A opção selecionada recebe borda/fundo azul.

### 4. Data da viagem

Quando aplicável ao tipo `Once`, mostrar seletor de data.

A visibilidade e validação devem seguir a lógica atual da página.

### 5. Botão principal

```text
Confirmar solicitação
```

### 6. Mensagem auxiliar

Pequena mensagem abaixo do botão informando que a solicitação será enviada ao motorista.

## Funcionalidade protegida

Preservar:

- Once/Weekly;
- data;
- `SuggestedPrice`;
- snapshot de preço;
- rota do passageiro;
- rota do motorista;
- criação da solicitação;
- validações existentes.

---

# 13. Mockup 10 — Aguardando Aceite

**Arquivo:** `docs/ui/mockups/10_AwaitingApproval.png`  
**Página:** `Views/Matching/AwaitingApprovalPage.xaml`

## Objetivo

Mostrar solicitações enviadas pelo passageiro que ainda aguardam resposta do motorista.

## Estrutura

### 1. Cabeçalho autenticado

Marca, notificações e avatar.

### 2. Título

```text
Aguardando aceite
```

Subtítulo:

```text
Solicitações enviadas que ainda aguardam resposta.
```

### 3. Cards de solicitações

Cada card mostra:

- motorista;
- status `Aguardando`;
- origem;
- destino;
- data/dias;
- horário;
- valor;
- horário/data de envio, quando disponível.

O status usa badge amarelo/laranja suave.

Não inventar timestamp específico se ele não existir no modelo atual.

### 4. TabBar

Aba `Caronas` ativa.

## Intenção de UX

O usuário deve perceber imediatamente que nenhuma ação é necessária enquanto aguarda o motorista.

---

# 14. Mockup 11 — Solicitações Recebidas

**Arquivo:** `docs/ui/mockups/11_ReceivedRequests.png`  
**Página:** `Views/Matching/ReceivedRequestsPage.xaml`

## Objetivo

Permitir que o motorista analise rapidamente solicitações recebidas e aceite ou recuse cada uma.

## Estrutura

### 1. Cabeçalho autenticado

Marca, notificações e avatar.

### 2. Título

```text
Solicitações recebidas
```

Subtítulo com quantidade ou mensagem contextual quando esse dado estiver disponível.

### 3. Card por passageiro

Informações exemplificadas:

- avatar;
- nome;
- curso;
- origem;
- destino;
- tipo da solicitação;
- horário;
- valor.

O curso aparece no mockup, mas só deve ser exibido se o dado existir realmente no perfil atual.

Não adicionar campo de curso ao modelo apenas para copiar o mockup.

### 4. Ações

Botão principal:

```text
Aceitar
```

Ação secundária/destrutiva:

```text
Recusar
```

A hierarquia deve tornar `Aceitar` clara sem esconder `Recusar`.

## Funcionalidade protegida

Não alterar:

- transição Pending → Accepted/Rejected;
- consumo de vaga;
- rejeição de concorrentes;
- preconditions;
- `requestRevision`;
- operações atômicas;
- tratamento de concorrência.

---

# 15. Mockup 12 — Encontrar Carona

**Arquivo físico:** `docs/ui/mockups/12_ConfirmedRoutes.png`  
**Página real:** `Views/Matching/FindRidePage.xaml`  
**Aba:** Caronas

> O nome do arquivo é histórico e não representa o conteúdo visual atual.

## Objetivo

Permitir que o passageiro selecione uma de suas rotas para iniciar a busca por motoristas compatíveis.

## Estrutura

### 1. Cabeçalho autenticado

Marca, notificações e avatar.

### 2. Título

```text
Encontrar carona
```

Subtítulo:

```text
Escolha uma rota para buscar motoristas
compatíveis.
```

### 3. Lista de rotas

Cards selecionáveis contendo:

- badge `Rota`;
- origem;
- destino;
- dias;
- horário.

A rota selecionada possui:

- borda azul;
- fundo levemente destacado;
- indicador circular/check azul.

Rotas não selecionadas permanecem neutras.

### 4. Ajuda contextual

Pequeno card informativo:

```text
Não encontrou sua rota? Crie uma nova em Rotas.
```

A ação deve respeitar a navegação real e pode direcionar à aba Rotas ou à criação de rota conforme a solução de Shell definida.

### 5. Botão principal

```text
Encontrar motoristas →
```

Só deve iniciar o matching quando houver uma rota válida selecionada.

### 6. TabBar

Aba `Caronas` ativa.

## Intenção de UX

Evitar que o usuário precise preencher novamente origem, destino, dias ou horário. A busca deve partir de uma rota semanal já cadastrada.

---

# 16. 13ª página — Detalhes do Trajeto

**Mockup:** inexistente  
**Página:** `Views/Matching/RouteDetailsPage.xaml`

## Objetivo

Apresentar visualmente a rota compartilhada calculada pelo matching e explicar seu impacto.

## Direção visual

Como não existe mockup próprio, utilizar os padrões globais deste documento e manter continuidade direta com `MatchResultsPage`.

### Estrutura recomendada

1. cabeçalho/navegação de retorno;
2. título `Detalhes do trajeto`;
3. resumo compacto do motorista e/ou trajeto;
4. mapa como principal elemento visual;
5. card de métricas;
6. mensagens de loading/erro/fallback.

### Mapa

O mapa deve continuar sendo o elemento dominante.

Preservar:

- polyline;
- pins;
- viewport;
- estados de carregamento;
- placeholder em caso de falha;
- mensagens existentes.

### Card de métricas

Agrupar visualmente:

- distância;
- duração;
- desvio em distância;
- desvio em minutos.

Evitar apresentar as métricas como uma lista técnica excessivamente densa.

### Intenção de UX

Responder de forma visual à pergunta:

> “Como fica o trajeto compartilhado e qual é o desvio gerado?”

Não transformar esta tela em uma nova etapa do fluxo ou adicionar controles de navegação turn-by-turn.

---

# 17. Comportamento responsivo

Os mockups representam uma largura de telefone específica, mas a implementação deve funcionar em diferentes dimensões Android.

Ao adaptar:

- não usar posições absolutas;
- preferir Grid, VerticalStackLayout, HorizontalStackLayout e controles responsivos;
- utilizar ScrollView/CollectionView quando o conteúdo puder ultrapassar a altura;
- permitir quebra de linha em endereços longos;
- não truncar informações críticas;
- garantir que botões permaneçam tocáveis;
- testar interação com teclado em Login, Cadastro e Nova Rota;
- considerar Safe Area e TabBar.

Endereços reais podem ser significativamente maiores do que os exemplos dos mockups.

---

# 18. Estados que precisam permanecer utilizáveis

Mesmo quando não aparecem explicitamente nos mockups, todas as páginas devem prever visualmente os estados já necessários ao aplicativo:

- loading;
- lista vazia;
- erro;
- offline/falha de rede;
- botão disabled;
- dados legados;
- ausência de rota do Google;
- endereço inválido/não selecionado;
- nenhuma correspondência no matching;
- nenhuma solicitação pendente;
- nenhuma carona confirmada.

Esses estados devem reutilizar estilos globais sempre que possível.

---

# 19. Regras para recursos compartilhados

Os mockups apresentam vários padrões repetidos. A implementação deve centralizá-los.

Usar `Resources/Styles/Colors.xaml` para:

- cores principais;
- fundos;
- bordas;
- textos;
- status;
- brushes.

Usar `Resources/Styles/Styles.xaml` para padrões reutilizáveis como:

- `PageTitle`;
- `PageSubtitle`;
- `SectionTitle`;
- `PrimaryButton`;
- `SecondaryButton`;
- `DangerButton`;
- `Card`;
- `SelectableCard`;
- `InputContainer`;
- `StatusBadge`;
- `DayChip`;
- `SummaryCard`;
- `EmptyState`;
- `InfoCard`;

Os nomes finais podem seguir a convenção já existente no projeto.

Não é obrigatório criar exatamente estes nomes. O importante é evitar duplicação de propriedades visuais entre páginas.

---

# 20. Regras para placeholders e assets futuros

Ainda não existem logo e imagens oficiais definitivas.

Durante o redesign:

- reservar espaço coerente com os mockups;
- não criar assets definitivos;
- não baixar imagens externas;
- não adicionar ilustrações stock;
- não criar dependência estrutural de um placeholder temporário;
- preferir containers que possam receber futuramente um `Image`;
- manter dimensões e alinhamento previsíveis.

Quando os assets oficiais forem adicionados posteriormente, a substituição deve exigir apenas a configuração da imagem/recurso, e não um novo redesign da página.

---

# 21. Ordem de consulta recomendada para o agente

Para cada bloco de implementação:

1. ler `AGENTS.md`;
2. ler esta especificação `docs/ui/MOCKUPS.md`;
3. identificar as páginas do bloco;
4. abrir somente os mockups correspondentes ao bloco para confirmação visual;
5. ler `Styles.xaml` e `Colors.xaml`;
6. ler os XAML relevantes;
7. abrir code-behind/ViewModels apenas se necessário para preservar comportamento;
8. implementar;
9. validar;
10. seguir para o próximo bloco somente conforme autorizado no `AGENTS.md` e no prompt da tarefa.

Os textos deste documento funcionam como pré-contexto para reduzir a necessidade de reinterpretação repetida das imagens.

As imagens continuam sendo a referência visual final quando houver dúvida sobre composição, proporção ou hierarquia.
