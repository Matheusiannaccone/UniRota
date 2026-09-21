# UniRota — Pendências após o incremento Google Maps

O fluxo principal está concluído e foi validado manualmente no Android:

```text
autenticação → rotas com endereços reais → matching geográfico
→ solicitação Once/Weekly → aceite/rejeição → vagas
→ rotas confirmadas → preço → visualização no mapa
```

O ApplicationId definitivo é `io.github.matheusiannaccone.unirota`.

Este documento mantém somente atividades ainda abertas. Itens concluídos nos
Blocos 1–7 não são pendências futuras.

## Obrigatório antes de usuários reais

### Firestore Security Rules

**Status:** pendente, com revisão separada deste bloco.

As regras publicadas devem ser revisadas e testadas para `users`,
`weeklyRoutes` e `rideRequests`, incluindo propriedade dos documentos,
transições de status, vagas, `requestRevision`, distância e preço. As validações
do cliente não substituem regras de servidor.

O Bloco 7 não altera `firestore.rules` nem regras publicadas.

### Índices e ambiente Firestore

- executar todos os fluxos no projeto Firebase real;
- criar e documentar os índices compostos efetivamente exigidos;
- remover dados de teste inadequados;
- validar documentos legados e atuais no mesmo ambiente;
- revisar as restrições da Web API Key cliente do Firebase.

### Concorrência em múltiplos dispositivos

Repetir em dispositivos reais os cenários de última vaga, duas criações
simultâneas do mesmo par de rotas, retry de precondition e alteração da sessão
durante uma operação. A implementação atual usa `updateTime`,
`requestRevision`, commit atômico e tentativas limitadas; a validação distribuída
continua necessária antes de uso mais amplo.

### Privacidade e termos

Publicar termos de uso e política de privacidade compatíveis com Firebase e
Google Maps Platform antes de uma distribuição pública.

## Preparação de distribuição

### APK/AAB e assinatura de release

- definir o formato final de distribuição;
- configurar e proteger o certificado de release;
- cadastrar no Google Cloud o SHA-1 de release ou Google Play App Signing;
- restringir `GOOGLE_MAPS_ANDROID_API_KEY` ao ApplicationId definitivo, aos
  SHA-1 aplicáveis e somente ao Maps SDK for Android;
- executar smoke test no artefato assinado.

### Configuração Firebase

A Web API Key cliente e o ProjectId ainda são registrados no `MauiProgram.cs`.
A Web API Key não é credencial administrativa, mas suas restrições devem ser
revistas e a configuração por ambiente pode ser organizada antes de uma
distribuição formal.

As chaves de Places e Routes permanecem no Secret Manager. A chave do Maps
Android é injetada localmente no build e não deve ser commitada.

### iOS

O target permanece estruturalmente compatível, mas não foi validado em
macOS/Xcode. Testar inicialização, autenticação, Firestore, SecureStorage,
Functions e MapKit antes de declarar suporte iOS.

### Validação com usuários

Realizar uma rodada controlada com estudantes, observando clareza da seleção de
endereços, qualidade dos matches, adequação dos limites de 5 km/15 min e custo
das APIs. Não alterar os limites sem uma decisão funcional baseada nos dados.

## Regras funcionais ainda não definidas

Esses itens exigem decisão de produto antes de implementação:

- limite máximo de vagas por veículo;
- redução de capacidade quando já existem passageiros aceitos;
- capacidade por dia ou ocorrência, em vez de uma capacidade semanal única;
- cooldown após rejeição;
- cancelamento/desistência e restauração de vaga;
- conclusão e histórico de caronas `Once` após a data;
- timestamps específicos de aceite/rejeição;
- notificações e histórico de solicitações;
- terminologia final de preço/contribuição;
- custo por km configurável e eventual rateio dinâmico.

O preço atual continua sendo snapshot calculado por
`distância compartilhada × 0,53 ÷ 2`.

## Melhorias técnicas não bloqueantes

- investigar a divergência anteriormente observada no Test Explorer do Visual
  Studio se ela ainda puder ser reproduzida; `dotnet test` é a referência;
- avaliar timestamp de servidor para campos de criação;
- definir recuperação para conta criada no Authentication sem perfil em
  `users` caso a gravação seguinte falhe;
- revisar a estratégia de snapshots de nomes se edição de perfil for criada;
- adicionar monitoramento operacional de quota/latência antes de uso amplo.

## Itens já concluídos

Não devem voltar à lista de pendências sem regressão comprovada:

- ApplicationId definitivo;
- Place IDs e compatibilidade de leitura com rotas legadas;
- autocomplete Places com debounce, cancelamento e session token;
- distância/duração reais e remoção da entrada manual;
- matching por desvio com limites centralizados;
- preço usando `SharedDistanceKm` sem nova chamada Routes;
- polyline, pins e viewport na tela de detalhes;
- Maps SDK Android sem permissões de localização;
- proteção de Places/Routes por callable autenticada e Secret Manager.

## Critério de prontidão para PR

A branch pode seguir para revisão quando testes .NET e Node, `npm audit`,
`git diff --check` e build Android passarem, a documentação estiver coerente e
nenhuma credencial sensível estiver versionada. A revisão das Firestore Security
Rules continua explicitamente separada e obrigatória antes de usuários reais.
