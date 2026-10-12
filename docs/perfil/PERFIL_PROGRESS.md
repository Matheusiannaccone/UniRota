# Perfil — progresso

- Blocos concluídos: 1 e 2. Atual: preparação do Bloco 3, aguardando decisão sobre verificação do novo e-mail por link quando exigida pelo Firebase.
- Estado: tela, header, navegação e logout integrados; edição de nome com PATCH apenas de name, atualização de CurrentUser após sucesso e recarga da Home ao retornar. User preservado.
- Arquivos principais do último bloco: IAuthService, FirebaseAuthService, ProfileViewModel, ProfilePage, FirebaseAuthServiceTests e quatro doubles de IAuthService existentes.
- Bloco imediatamente anterior: 2 — nome obrigatório, identidade/e-mail/data preservados, falhas não substituem CurrentUser nem restauram sessão encerrada.
- Próximo: 3 — reautenticação e alteração de e-mail; depois 4 (senha) e 5 (integração).
- Pendências/riscos: decidir etapa de link, obrigatória se proteção contra enumeração estiver ativa (configuração remota não verificada); definir recuperação explícita de gravação parcial Auth/Firestore; regras Firestore remotas não disponíveis no repositório. Nenhuma regra/configuração alterada.
- Validações: Bloco 1 — 159/159 testes; Bloco 2 — 6/6 testes focados novos; builds Android dos dois blocos com 0 erros e 5 avisos NU1900; diff sem erros.
- Commits: Bloco 1 `9f5d6de`; Bloco 2 `feat(perfil): adiciona edição do nome do usuário`.
- Testes manuais pendentes: Home → Perfil → Voltar/Sair, headers nas abas, nome persistido e refletido na Home, teclado/tela pequena e permissão de atualização de users no Firebase real.
- Alteração prévia em AGENTS.md preservada fora dos commits. Sem alterações de versão, Functions ou regras.
