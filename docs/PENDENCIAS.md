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
- documentar como cada integrante deve configurar o projeto localmente;
- revisar restrições da API Key no Google Cloud/Firebase Console.

### 2. Revisar restrições da Web API Key
**Status:** Pendente  
**Prioridade:** Média

A chave usada pelo aplicativo deve ter restrições adequadas quando possível.

**Possíveis ações futuras:**
- revisar as APIs habilitadas;
- restringir a chave ao necessário para o projeto;
- documentar que a segurança dos dados continua dependendo de Authentication e Firestore Security Rules.

### 3. Revisar estratégia de token para serviços Firebase futuros
**Status:** Pendente  
**Prioridade:** Média

O `FirebaseAuthService` mantém o ID token em memória, mas a interface `IAuthService` não expõe uma forma para outros serviços obterem um token válido.

Isso não é um problema no Incremento 1, mas poderá ser necessário no Incremento 2 para um futuro `FirestoreRouteService`.

**Possíveis ações futuras:**
- criar um provedor simples de sessão/token;
- evitar expor tokens diretamente para ViewModels;
- garantir renovação do ID token quando necessário.

### 4. Refatorar `FirebaseAuthService` apenas se ele crescer além do escopo atual
**Status:** Pendente / condicional  
**Prioridade:** Baixa

O serviço ficou relativamente grande por concentrar chamadas REST, DTOs internos, tratamento de erros, sessão e acesso ao documento `users/{uid}`.

No momento, isso é aceitável porque mantém a implementação coesa e evita abstrações excessivas.

**Revisar somente se:**
- novas responsabilidades forem adicionadas ao arquivo;
- rotas semanais começarem a ser implementadas dentro dele;
- a manutenção se tornar difícil.

**Não fazer apenas para reduzir número de linhas.**

### 5. Revisar o uso de `CreatedAtUtc` baseado no relógio do dispositivo
**Status:** Pendente  
**Prioridade:** Baixa

O campo `CreatedAtUtc` usa `DateTimeOffset.UtcNow` no dispositivo.

Isso é suficiente para o MVP, mas futuramente pode ser substituído por timestamp gerado pelo servidor/Firestore para maior confiabilidade.

### 6. Tratar conta criada no Authentication sem perfil no Firestore
**Status:** Pendente  
**Prioridade:** Baixa

Se o Firebase Authentication criar a conta e a gravação de `users/{uid}` falhar, a conta poderá permanecer sem documento de perfil.

O MVP não implementa rollback para manter a solução simples.

**Possíveis ações futuras:**
- excluir a conta recém-criada em caso de falha;
- permitir recuperação/criação automática do perfil;
- criar mecanismo de consistência durante login.

### 7. Testar autenticação em iOS
**Status:** Pendente  
**Prioridade:** Média

O fluxo foi validado funcionalmente no Android.

O projeto mantém target iOS e usa código compartilhado, mas a validação completa depende de ambiente Mac/Xcode.

**Validar futuramente:**
- inicialização;
- Login;
- Cadastro;
- SecureStorage;
- restauração de sessão;
- logout;
- Firestore via REST.

### 8. Melhorar acabamento visual das telas de autenticação
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
- estados vazios e mensagens.

Fazer somente depois que o fluxo principal do MVP estiver completo.

### 9. Centralizar estilos e cores do aplicativo
**Status:** Pendente  
**Prioridade:** Baixa

Durante a correção de erros de runtime, recursos globais inexistentes como `Headline`, `SubHeadline` e algumas cores foram removidos/substituídos por valores locais.

No futuro, pode ser útil criar estilos globais próprios e garantidamente carregados.

**Objetivo futuro:**
- reduzir repetição;
- manter consistência visual;
- evitar dependência de estilos do template padrão do MAUI.

### 10. Revisar identificador definitivo do aplicativo
**Status:** Pendente  
**Prioridade:** Média

O projeto ainda utiliza o identificador padrão/placeholder:

`com.companyname.unirota`

Antes de uma distribuição mais formal, esse identificador deve ser substituído por um identificador definitivo.

### 11. Revisar histórico/divergência entre `master` e `feature/authentication`
**Status:** Pendente  
**Prioridade:** Baixa

A branch de autenticação foi criada antes de commits de documentação realizados na `master`, gerando divergência no histórico.

Os arquivos relevantes já existem atualizados na branch atual, portanto isso não bloqueia o desenvolvimento.

**Resolver antes de integração final:**
- revisar diferenças;
- fazer merge/rebase conforme apropriado;
- garantir que nenhuma documentação seja perdida.


---

## Incremento 2 — Rotas semanais

### 1. Revisar Firestore Security Rules antes da validação com usuários reais
**Status:** Pendente  
**Prioridade:** Alta

Durante o desenvolvimento, as Firestore Security Rules foram temporariamente configuradas para permitir leitura e escrita sem restrições:

```text
match /{document=**} {
  allow read, write: if true;
}
```

### 2. Limitar quantidade máxima de vagas oferecidas

Status: Pendente
Prioridade: Baixa

Atualmente, o cadastro de uma rota de motorista valida apenas que a quantidade de vagas seja maior que zero. Isso permite informar valores incompatíveis com um veículo de passeio, como dezenas ou centenas de vagas.

O comportamento não impede o funcionamento do MVP, mas deve ser refinado antes de uma versão mais próxima de uso real.

Possíveis ações futuras:
- definir um limite máximo simples para o MVP, por exemplo entre 1 e 7 vagas;
- validar o limite tanto no ViewModel quanto no serviço;
- futuramente, caso dados de veículo sejam adicionados, relacionar a quantidade máxima de vagas à capacidade cadastrada do veículo.

---

## Critério para revisão final

Ao concluir todos os incrementos obrigatórios:

1. revisar todos os itens deste arquivo;
2. classificar em:
   - obrigatório antes da entrega;
   - desejável se houver tempo;
   - evolução futura;
3. resolver primeiro riscos de estabilidade e segurança;
4. depois revisar documentação e qualidade visual;
5. manter como evolução futura tudo que não for necessário para uma demonstração estável e coerente.
