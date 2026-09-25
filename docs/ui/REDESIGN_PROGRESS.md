# Progresso do redesign UniRota

## Estado geral
- Bloco atual: 2 — Shell TabBar, Home e Minhas Rotas.
- Etapa atual: inspeção da navegação antes da alteração estrutural.
- Último checkpoint: Bloco 1 aprovado em compilação Android e revisão de bindings.
- Build atual: Android Debug aprovado, 0 erros e 0 avisos.
- Testes atuais: não executados; Bloco 1 alterou apenas XAML.

## Concluído
- Fundação: estilos de conteúdo, tipografia, marca textual, cards, campos, ações, erros e espaçamentos centralizados.
- Startup, Login e Cadastro redesenhados conforme mockups 02, 03 e 04.
- Autenticação, validações e comandos preservados.

## Em andamento
- Migração da área autenticada para Shell TabBar e redesign de Home/Minhas Rotas.

## Arquivos alterados no bloco atual
- Nenhum arquivo do Bloco 2 ainda.

## Decisões tomadas
- Marca textual em container substituível; nenhum asset fictício.
- Não implementar recuperação de senha/visibilidade de senha ausentes no fluxo atual.
- Manter senha mínima de seis caracteres, conforme validação existente.
- Estilos compartilhados suportam temas claro e escuro.

## Validações executadas
- Bloco 1: dotnet build UniRota/UniRota.csproj -f net8.0-android --no-restore -v minimal: aprovado (0/0).
- Build exige execução fora do sandbox para acesso ao SDK local; tentativa inicial bloqueada antes da compilação.
- git diff --check aprovado após remover espaços finais.
- Bindings e comandos das três páginas conferidos; nenhum ViewModel/code-behind alterado.

## Pendências conhecidas
- Validação visual/interação em Android (teclado, scroll, telas pequenas) ainda pendente.
- Checkpoints 2 a 4 e revisão integrada.

## Próxima ação exata
- Ler AppShell, navegação/autenticação diretamente relacionada, Home/MyRoutes e mockups 01/05; implementar TabBar e validar checkpoint 2.

## Observações para retomada
- Projeto MAUI em UniRota/; testes em UniRota.Tests/.
- Separação de commit do Bloco 1: Colors.xaml, Styles.xaml, StartupPage.xaml, LoginPage.xaml, RegisterPage.xaml e este progresso.
- Execução dos quatro blocos autorizada sem confirmação intermediária.

### Etapa estrutural do Bloco 2
- TabBar nativa criada via Shell com templates DI: home/HomePage, routes/MyRoutesPage, rides/ConfirmedRoutesPage sob main.
- Login, Cadastro e Startup usam AppShell.HomeRoute; raízes de abas removidas do registro de rotas globais.
- Troca de aba conserva stacks. Ao retornar a login sem sessão, templates/stacks autenticados são recriados; navegação para main sem sessão é bloqueada.
- Home e MyRoutes redesenhados; três SVGs funcionais provisórios para abas adicionados.
- Build Android aprovado (0 erros/0 avisos), suíte .NET completa 154/154, diff --check aprovado.
- Emulador disponível: emulator-5554. APK inicialmente carregou assemblies antigos do fast deployment.
- Cache files/.__override__ do app foi renomeado para files/unirota-redesign-override-backup (preservado). Build com EmbedAssembliesIntoApk=true em andamento para smoke test real.
- Próxima ação: instalar APK autocontido, validar restauração/abas/back no emulador; registrar limitações de login/cadastro sem credenciais e encerrar checkpoint 2 antes do Bloco 3.
- Smoke real encontrou NullReferenceException em OnNavigating durante InitializeComponent. Corrigido atribuindo dependências antes do XAML e protegendo callbacks de inicialização; repetir build e smoke antes de avançar.
- Correção do callback validada: build Android autocontido aprovado 0/0, app abre e restaura sessão existente.
- Smoke Android: Início/Rotas/Caronas renderizam; Nova Rota abre; alternar Rotas → Caronas → Rotas conserva formulário; back retorna à lista sem duplicar raiz.
- Login/Cadastro/logout seguidos de novo login: validação interativa pendente por ausência de credenciais fornecidas. Comandos e redirecionamentos conferidos estaticamente; não houve alteração na autenticação.
- Checkpoint 2 aprovado nos checks disponíveis, sem regressão conhecida; limitações manuais registradas.

### Início do Bloco 3
- Etapa atual: consulta dos mockups 06, 08 e 12 e dos quatro XAMLs correspondentes.
- Próxima ação exata: redesenhar NewRoute, FindRide, MatchResults e RouteDetails preservando handlers, comandos, nomes dos controles de mapa e bindings geográficos.
