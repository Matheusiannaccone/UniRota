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
