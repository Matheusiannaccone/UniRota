# Perfil — progresso

- Concluídos: Blocos 1, 2 e 3. Próximo: 4 — alteração de senha; depois 5 — integração/regressão.
- Estado: Perfil/nome prontos; e-mail exige reautenticação, envio de link e consulta real ao Auth antes de sincronizar Firestore/CurrentUser. Recuperação excepcional por senha autorizada e implementada sem persistir senha.
- Arquivos principais do último bloco: FirebaseAuthService (.cs e .Profile.cs), IAuthService, PendingEmailChange, ProfilePage/ViewModel, AwaitingEmailConfirmationPage/ViewModel, AppShell, StartupViewModel, DI e testes FirebaseAuthService.
- Bloco imediatamente anterior: espera sem header/TabBar/voltar/logout; verificação automática no retorno ao primeiro plano e fallback manual; Cancelar consulta Auth e conclui se já confirmado; falhas de sincronização mantêm estado recuperável.
- Reenvios/cooldown: inicial + 2 adicionais; 60 s após envios/verificações; automação e Cancelar independem do timer. Destino/contador/prazo no SecureStorage, sem senha, preservados após recriação/reinício. Resposta de envio perdida conta como tentativa por segurança.
- Validações: 183/183 testes na regressão; 24/24 focados após ajustes finais; build Android final do bloco com 0 erros e 5 avisos NU1900; diff sem erros.
- Commits: 1 `9f5d6de`; 2 `746eef7`; 3 `feat(perfil): adiciona alteração segura de email`.
- Pendências/riscos: testes reais Android/Firebase (retorno do navegador, gesto voltar, tokens invalidados, link/reenvios, regras users). Cancelamento é local e não revoga links; login/restauração reconciliam alterações posteriores. Proteção contra enumeração pode ocultar endereço já cadastrado/entrega do link.
- Preservados: AGENTS.md prévio fora dos commits; versão, User, Functions e regras sem alteração. Senha ainda não implementada.
