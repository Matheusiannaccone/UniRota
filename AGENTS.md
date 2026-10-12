# UniRota — Instruções para agentes de código

## 1. Contexto do projeto

O UniRota é um aplicativo acadêmico de caronas para estudantes da Facens.

O MVP funcional e o redesign principal já foram concluídos e validados no Android. A fase atual é de **preparação para beta test**, com foco em estabilidade, correções pontuais, melhorias de UX e pequenas evoluções aprovadas.

Fluxo principal protegido:

1. Cadastro/Login
2. Rotas semanais com endereços reais
3. Matching determinístico e geográfico
4. Solicitação de carona Once/Weekly
5. Aceite/rejeição e consumo de vagas
6. Caronas confirmadas
7. Preço sugerido
8. Visualização de trajeto em mapa

Não ampliar o escopo, reescrever arquitetura ou adicionar funcionalidades não solicitadas.

---

## 2. Stack e arquitetura

Preservar:

- .NET MAUI / .NET 8
- C# e XAML
- MVVM simples
- Dependency Injection
- Firebase Authentication
- Cloud Firestore
- Google Places API (New)
- Google Routes API
- Maps SDK for Android
- Shell para navegação

Estrutura principal do código:

- Views: `UniRota/Views`
- ViewModels: `UniRota/ViewModels`
- Services: `UniRota/Services`
- Models: `UniRota/Models`
- testes .NET: `UniRota.Tests`
- Firebase Functions: `functions`

Manter um único projeto .NET MAUI.

Não criar camadas, dependências ou abstrações sem necessidade concreta.

---

## 3. Regras funcionais protegidas

Não alterar sem solicitação explícita:

- autenticação, sessão e logout;
- estrutura e segurança do Firebase;
- modelos persistidos;
- autocomplete e Place IDs;
- cálculo real de distância/duração;
- matching geográfico e seus filtros;
- fórmula de precificação;
- snapshot de `SuggestedPrice`;
- fluxo Once/Weekly;
- estados Pending / Accepted / Rejected;
- consumo de vagas e concorrência;
- mapa, polyline, pins e viewport.

Mudanças de UI ou navegação não devem reimplementar regras de negócio.

---

## 4. Navegação e UI compartilhada

A área autenticada usa Shell TabBar com:

- **Início** → `HomePage`
- **Rotas** → `MyRoutesPage`
- **Caronas** → `ConfirmedRoutesPage`

`StartupPage`, `LoginPage` e `RegisterPage` ficam fora da TabBar.

O header autenticado é reutilizável e padronizado:

- Home: `UniRota` + `Perfil`
- Perfil: `UniRota` + `Sair` + `Voltar`
- demais telas autenticadas: `UniRota` + `Home` + `Voltar`
- `Perfil` existe somente na tela Home e direciona para Perfil
- `Sair` existe somente na tela de Perfil e usa estilo destrutivo
- `Voltar` usa o stack real do Shell
- `Home` retorna à raiz sem empilhar outra Home
- não usar navbar visual nativa do Shell nas páginas autenticadas

Recursos compartilhados:

- cores e brushes → `Resources/Styles/Colors.xaml`
- estilos, dimensões e espaçamentos → `Resources/Styles/Styles.xaml`

Evitar duplicação de estilos entre páginas.

---

## 5. Como trabalhar em cada tarefa

Antes de alterar código:

1. Ler este `AGENTS.md`.
2. Verificar a branch atual.
3. Ler somente os arquivos diretamente relacionados à tarefa.
4. Abrir arquivos adicionais apenas quando houver dependência concreta.

Diretrizes:

- alterar apenas o necessário;
- preservar bindings e commands existentes;
- não mover lógica de negócio para code-behind por conveniência;
- não refatorar áreas não relacionadas;
- não inserir segredos no repositório;
- manter compatibilidade com Android;
- parar e pedir decisão quando o requisito funcional estiver ambíguo.

### Economia de contexto

Não fazer auditoria geral do repositório a cada tarefa.

Ler:

- `AppShell.xaml/.cs` somente quando houver impacto de navegação;
- `Styles.xaml` / `Colors.xaml` somente quando houver impacto visual compartilhado;
- ViewModels, Services e Models apenas quando necessários à funcionalidade em alteração;
- Functions somente quando a tarefa envolver backend/Firebase Functions.

Relatórios finais devem ser curtos e objetivos.

---

## 6. Validação

Baseline conhecida:

- 154/154 testes .NET aprovados
- 20/20 testes Node aprovados
- build Android com 0 erros e 0 avisos
- fluxo principal validado manualmente

Para cada tarefa, executar apenas validações pertinentes.

Sempre que aplicável:

- checagem de diff: `git diff --check`
- build Android: `dotnet build UniRota/UniRota.csproj -c Release -f net8.0-android`
- testes .NET: `dotnet test UniRota.Tests/UniRota.Tests.csproj -c Release`
- testes Node/Firebase Functions: `cd functions && npm test`

Os comandos acima devem ser executados a partir da raiz do repositório. Os testes Node só são necessários quando houver impacto em `functions` ou configuração relacionada.

Executar a suíte completa quando a alteração tiver impacto transversal.

Não aceitar regressão conhecida sem causa identificada.

---

## 7. Fora do escopo sem solicitação explícita

Não implementar por iniciativa própria:

- nova fórmula de preço;
- chat;
- pagamentos;
- reputação;
- notificações reais;
- localização automática/GPS;
- rastreamento em tempo real;
- navegação turn-by-turn;
- IA/Machine Learning;
- novas regras de vagas;
- cooldown após rejeição;
- cancelamento/restauração de vaga;
- backend tradicional;
- SQL/PostgreSQL.

Pendências funcionais permanecem em `docs/PENDENCIAS.md`.

---

## 8. Commits e encerramento

Usar commits pequenos, semânticos e em português.

Exemplos:

```text
feat(perfil): adiciona edição de dados do usuário
fix(navegacao): corrige retorno entre telas
fix(ui): corrige inconsistência visual
fix(auth): corrige comportamento de sessão
```

Não misturar alterações não relacionadas no mesmo commit.

Ao concluir uma tarefa, informar apenas:

- arquivos alterados;
- comportamento implementado;
- validações executadas;
- resultado do build;
- pontos que precisam de teste manual.

Não iniciar outra pendência automaticamente.

---

## Regra de decisão

Antes de qualquer alteração, perguntar:

> Isso faz parte do requisito solicitado e preserva a estabilidade do fluxo existente?

Se não, não implementar.
