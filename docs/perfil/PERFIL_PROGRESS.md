# Perfil — progresso

- Bloco concluído: 1 — fundação e navegação.
- Estado: Perfil registrado no Shell/DI; Home mostra Perfil; Perfil mostra Sair destrutivo e Voltar pelo stack; logout movido; avatar e seções preparados.
- Arquivos principais: ProfilePage, ProfileViewModel, AuthenticatedHeader, HomePage/HomeViewModel, AppShell, MauiProgram, Styles.xaml.
- Bloco imediatamente anterior: tela e navegação, sem edição de dados.
- Próximo: 2 — edição do nome.
- Pendências/riscos: teste manual Android; regras Firestore publicadas não disponíveis no repositório; alteração prévia em AGENTS.md preservada.
- Validações: diff sem erros; 159/159 testes .NET; build Android com 0 erros e 5 avisos NU1900 (consulta NuGet indisponível). SDK/build e Git exigiram execução fora do sandbox.
- Commit do bloco: `feat(perfil): adiciona tela de perfil e navegação`.
