# UniRota — Instruções para agentes de código

## Objetivo do projeto
O UniRota é um MVP acadêmico de um aplicativo de caronas para estudantes da Facens.

O fluxo obrigatório do MVP é:

1. Cadastro/Login
2. Rotas semanais
3. Matching determinístico
4. Preço sugerido
5. Validação acadêmica do fluxo principal

O foco é demonstrar o conceito com simplicidade, estabilidade, rastreabilidade e coerência com a documentação do projeto.

## Stack definida

- .NET MAUI
- .NET 8
- C#
- XAML
- MVVM simples
- Dependency Injection
- Firebase Authentication
- Cloud Firestore
- GitHub para versionamento
- Trello para gestão do projeto

## Arquitetura

Manter um único projeto .NET MAUI.

Estrutura esperada:

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
│   └── Firebase/
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
└── MauiProgram.cs
```

A arquitetura deve permanecer simples e adequada ao MVP.

## Responsabilidades técnicas principais

### Autenticação
- Firebase Authentication
- cadastro
- login
- verificação de sessão
- logout
- acesso à área autenticada apenas para usuários logados
- persistência do perfil básico em `users`

### Rotas semanais
- cadastro de origem
- destino
- dias da semana
- horário
- papel do usuário na rota
- vagas, quando aplicável
- persistência em `weeklyRoutes`

### Matching
O matching do MVP deve ser determinístico e implementado em C#.

Critérios esperados:
- papéis opostos entre motorista e passageiro
- compatibilidade de dia
- compatibilidade de horário
- compatibilidade de origem
- compatibilidade de destino

Não calcular rota real, distância geográfica ou desvio em mapa no MVP.

### Precificação
A precificação deve:
- ser implementada em C#
- utilizar fórmula simples
- ser transparente
- ser documentável
- produzir resultado reproduzível

## Papel do usuário
Motorista e passageiro devem ser tratados preferencialmente como papéis associados à rota semanal, e não como um tipo permanente de conta.

O mesmo usuário pode ser motorista em determinados dias e passageiro em outros.

## Firestore

Coleções previstas:

- `users`
- `weeklyRoutes`
- `matches` — opcional no início
- `pricingCalculations` — opcional no início
- `feedback` — evolução futura

Criar apenas as coleções necessárias ao incremento atual.

## Ordem de desenvolvimento

1. Autenticação
2. Rotas semanais
3. Matching determinístico
4. Preço sugerido
5. Validação acadêmica

Não iniciar um incremento posterior antes que o anterior esteja funcional e demonstrável, salvo quando houver solicitação explícita.

## Primeiro incremento: autenticação

Implementar:

- `LoginPage`
- `RegisterPage`
- `HomePage` protegida
- modelo `User`
- `IAuthService`
- implementação Firebase do serviço de autenticação
- configuração de Dependency Injection
- navegação entre login, cadastro e Home
- verificação de sessão
- logout
- persistência do perfil básico no Firestore

Critério de pronto:

> O usuário consegue criar conta, entrar, acessar a área autenticada, fechar e reabrir o aplicativo com a sessão reconhecida quando aplicável e sair retornando ao fluxo de autenticação.

## Regras para alterações

Ao trabalhar no código:

- leia este arquivo antes de alterar o projeto;
- inspecione o estado atual do repositório;
- altere somente arquivos necessários para a tarefa;
- não faça refatorações fora do escopo;
- não adicione camadas arquiteturais sem necessidade real;
- não crie backend próprio;
- não crie API REST separada;
- não adicione SQL/PostgreSQL;
- não implemente mapas avançados;
- não implemente cálculo real de rota/desvio;
- não implemente chat;
- não implemente pagamento;
- não implemente reputação;
- não implemente Machine Learning ou IA no MVP sem solicitação explícita;
- não introduza microserviços ou arquitetura distribuída;
- preserve a compilação do projeto sempre que possível;
- ao concluir, informe arquivos alterados, decisões tomadas, suposições e etapas manuais necessárias.

## Regra de decisão

Antes de adicionar qualquer recurso, aplicar a pergunta:

> Isso melhora de forma clara a demonstração, a validação ou a documentação da entrega?

Se não, tratar como evolução futura.

## Padrão de trabalho recomendado

Para cada tarefa:

1. Ler o estado atual do repositório.
2. Ler este `AGENTS.md`.
3. Propor um plano curto.
4. Implementar apenas o escopo aprovado.
5. Validar build e erros causados pela alteração.
6. Informar claramente o que mudou.
7. Não avançar automaticamente para o próximo incremento.

## Commits sugeridos

Usar commits pequenos e semânticos, por exemplo:

```text
feat(auth): add authentication views and navigation
feat(auth): integrate Firebase authentication
feat(auth): persist user profile in Firestore
fix(auth): handle invalid credentials and loading state
feat(routes): add weekly route registration
feat(matching): add deterministic matching rules
feat(pricing): add suggested price calculation
```
