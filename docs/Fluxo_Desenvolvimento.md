# UniRota — Fluxo de Desenvolvimento do MVP

## 1. Objetivo

Este documento registra o fluxo de desenvolvimento do MVP do UniRota.

O objetivo técnico é implementar e validar, de forma incremental, o seguinte fluxo:

```text
Cadastro/Login
    ↓
Rotas semanais
    ↓
Matching determinístico
    ↓
Preço sugerido
    ↓
Validação acadêmica
```

A prioridade é manter o produto simples, estável, demonstrável e coerente com a documentação oficial do projeto.

---

## 2. Arquitetura técnica

### Stack

| Parte | Decisão |
|---|---|
| Aplicativo | .NET MAUI |
| Framework | .NET 8 |
| Linguagem | C# |
| Interface | XAML |
| Organização | MVVM simples |
| Injeção de dependência | Microsoft.Extensions.DependencyInjection |
| Autenticação | Firebase Authentication |
| Banco | Cloud Firestore |
| Matching | Regras determinísticas em C# |
| Precificação | Fórmula simples em C# |
| Backend próprio | Não será criado no MVP |
| IA / ML | Evolução futura |
| Versionamento | GitHub |
| Gestão | Trello |

### Estrutura conceitual

```text
.NET MAUI
    │
    ├── Firebase Authentication
    │
    ├── Cloud Firestore
    │
    ├── MatchingService
    │
    └── PricingService
```

### Estrutura sugerida do projeto

```text
UniRota/
├── Models/
│   ├── User.cs
│   ├── WeeklyRoute.cs
│   ├── MatchResult.cs
│   └── PricingResult.cs
├── Views/
│   ├── Auth/
│   │   ├── LoginPage.xaml
│   │   └── RegisterPage.xaml
│   ├── Routes/
│   │   ├── WeeklyRoutesPage.xaml
│   │   └── CreateWeeklyRoutePage.xaml
│   ├── Matching/
│   │   └── MatchResultsPage.xaml
│   └── HomePage.xaml
├── ViewModels/
├── Services/
│   ├── Interfaces/
│   │   ├── IAuthService.cs
│   │   ├── IRouteService.cs
│   │   ├── IMatchingService.cs
│   │   └── IPricingService.cs
│   ├── Firebase/
│   │   ├── FirebaseAuthService.cs
│   │   └── FirestoreRouteService.cs
│   ├── MatchingService.cs
│   └── PricingService.cs
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
└── MauiProgram.cs
```

---

## 3. Estratégia de desenvolvimento

O MVP deve ser construído em incrementos verticais.

Cada incremento deve atravessar todas as camadas necessárias para entregar uma funcionalidade real e testável.

Evitar desenvolvimento horizontal do tipo:

```text
todos os Models
→ todos os Services
→ todas as telas
→ toda a lógica
```

Preferir:

```text
uma funcionalidade completa
→ próxima funcionalidade
→ próxima funcionalidade
```

Cada incremento só deve ser considerado concluído quando puder ser demonstrado.

---

# 4. Incremento 1 — Autenticação

## Objetivo

Garantir que apenas usuários cadastrados tenham acesso ao aplicativo.

## Implementar

- `LoginPage`
- `RegisterPage`
- `HomePage`
- `LoginViewModel`
- `RegisterViewModel`
- `HomeViewModel`
- modelo `User`
- `IAuthService`
- `FirebaseAuthService`
- configuração de DI
- navegação
- verificação de sessão
- logout
- persistência do perfil básico em Firestore

## Fluxo

```text
Abre o aplicativo
        ↓
Existe sessão válida?
    ↙            ↘
  Sim            Não
   ↓              ↓
 Home           Login
                  ↓
             Criar conta
                  ↓
               Cadastro
                  ↓
          Firebase Auth
                  ↓
             Firestore
                  ↓
                Home
```

## Critério de pronto

O usuário consegue:

1. criar conta;
2. entrar;
3. acessar a Home;
4. fechar e reabrir o aplicativo com a sessão reconhecida quando aplicável;
5. sair;
6. retornar ao fluxo de autenticação.

---

# 5. Incremento 2 — Rotas semanais

## Objetivo

Permitir que o usuário autenticado registre sua rotina de deslocamento.

## Modelo principal

`WeeklyRoute`

Campos mínimos:

- `Id`
- `UserId`
- `Origin`
- `Destination`
- `Days`
- `Time`
- `Role`
- `Seats`, quando aplicável

## Papel do usuário

O papel deve estar associado à rota semanal.

Exemplo:

```text
Usuário A
├── segunda 07:00 → motorista
├── terça 18:30   → passageiro
└── quinta 07:00  → motorista
```

## Implementar

- `WeeklyRoute`
- `IRouteService`
- implementação Firestore
- tela para criação de rota
- tela/listagem de rotas
- gravação em `weeklyRoutes`
- leitura das rotas do usuário
- validação de campos

## Critério de pronto

Uma rota semanal pode ser cadastrada, persistida e recuperada.

---

# 6. Incremento 3 — Matching determinístico

## Objetivo

Identificar compatibilidade entre motorista e passageiro usando regras simples.

## Regras básicas

Uma combinação pode considerar:

```text
papéis opostos
AND
dia compatível
AND
horário compatível
AND
origem compatível
AND
destino compatível
```

## Implementar

- `MatchResult`
- `IMatchingService`
- `MatchingService`
- regras de comparação
- tolerância de horário definida pelo projeto
- tela de resultado

## Não implementar

- cálculo real de rota
- GPS
- geolocalização avançada
- desvio de rota
- inteligência artificial

## Critério de pronto

Cenários compatíveis e incompatíveis previamente preparados produzem resultados coerentes e reproduzíveis.

---

# 7. Incremento 4 — Preço sugerido

## Objetivo

Exibir um valor estimado por meio de uma fórmula simples e transparente.

## Implementar

- `PricingResult`
- `IPricingService`
- `PricingService`
- fórmula documentada
- parâmetros de entrada
- exibição do resultado

A persistência em `pricingCalculations` é opcional no início e deve ser feita apenas se contribuir para rastreabilidade ou validação.

## Critério de pronto

O preço exibido pode ser reproduzido manualmente usando a mesma fórmula e os mesmos parâmetros.

---

# 8. Incremento 5 — Validação acadêmica

## Objetivo

Demonstrar que o MVP atende ao objetivo proposto.

## Preparar

- dados reais limitados ou dados demonstrativos;
- cenários controlados;
- entradas;
- resultado esperado;
- resultado obtido;
- falhas;
- evidências;
- screenshots.

## Casos mínimos

### Autenticação
- cadastro válido;
- login válido;
- login inválido;
- logout.

### Rotas
- criação válida;
- campos obrigatórios;
- recuperação da rota salva.

### Matching
- cenário compatível;
- cenário incompatível por dia;
- cenário incompatível por horário;
- cenário incompatível por origem/destino;
- cenário incompatível por papel.

### Preço
- cálculo com parâmetros conhecidos;
- reprodução manual da fórmula.

## Fluxo final da demonstração

```text
Criar conta
    ↓
Entrar
    ↓
Cadastrar rota semanal
    ↓
Buscar/comparar rota
    ↓
Exibir compatibilidade
    ↓
Calcular preço sugerido
```

## Critério de pronto

O fluxo completo pode ser demonstrado de ponta a ponta com estabilidade nos cenários preparados.

---

# 9. Evolução do Firestore

| Incremento | Coleção | Finalidade |
|---|---|---|
| 1 | `users` | Perfil básico e identificação funcional |
| 2 | `weeklyRoutes` | Rotas semanais |
| 3 | `matches` | Opcional; rastrear comparações/resultados |
| 4 | `pricingCalculations` | Opcional; rastrear cálculos |
| Futuro | `feedback` | Aceite, recusa, avaliações e dados para evolução futura |

Criar apenas as coleções necessárias ao incremento em desenvolvimento.

---

# 10. Itens fora do escopo prioritário

Não implementar no MVP atual, salvo decisão explícita posterior:

- backend próprio;
- API REST separada;
- SQL;
- PostgreSQL;
- mapas avançados;
- geolocalização avançada;
- cálculo real de distância;
- cálculo real de desvio;
- chat;
- pagamento;
- reputação;
- publicação em lojas;
- dashboard analítico no aplicativo;
- Machine Learning treinado;
- IA em produção;
- microserviços;
- arquitetura distribuída.

---

# 11. Inteligência artificial

No MVP, o matching utiliza regras determinísticas.

A IA deve ser tratada como evolução futura.

Possível evolução:

```text
dados reais
    ↓
histórico de matches
    ↓
aceites/recusas
    ↓
horários e trajetos
    ↓
preferências
    ↓
modelo de ranking
```

Não afirmar na documentação ou apresentação que o MVP utiliza IA se não existir um modelo efetivamente implementado.

---

# 12. Regra de decisão

Para qualquer nova funcionalidade:

> Isso melhora de forma clara a demonstração, a validação ou a documentação da entrega?

Se a resposta for não, registrar como evolução futura.

---

# 13. Definição de pronto geral

Uma tarefa técnica deve ser considerada concluída quando:

- o código está implementado;
- o projeto compila;
- o fluxo relacionado pode ser testado;
- erros principais são tratados;
- as decisões relevantes estão documentadas;
- não foram adicionadas funcionalidades fora do escopo;
- a alteração é pequena o suficiente para ser revisada;
- o commit descreve claramente a mudança.

---

# 14. Estratégia de branches

Sugestão:

```text
master
├── feature/authentication
├── feature/weekly-routes
├── feature/matching
├── feature/pricing
└── feature/validation
```

Evitar implementar diretamente em `master`.

---

# 15. Commits sugeridos

```text
feat(auth): add authentication views and navigation
feat(auth): integrate Firebase authentication
feat(auth): persist user profile in Firestore

feat(routes): add weekly route model and service
feat(routes): add weekly route registration flow

feat(matching): add deterministic matching rules
feat(matching): add match results view

feat(pricing): add suggested price calculation

test(validation): add MVP validation scenarios
```

---

# 16. Resultado esperado

Ao final do desenvolvimento, o UniRota deve demonstrar que:

- um usuário consegue criar conta e entrar;
- uma rota semanal pode ser cadastrada;
- o sistema consegue identificar ou classificar uma rota compatível;
- o sistema apresenta um preço sugerido;
- a demonstração é estável;
- a equipe consegue explicar arquitetura, dados, limitações e evolução futura;
- a documentação diferencia claramente o que foi implementado do que permanece como evolução.
