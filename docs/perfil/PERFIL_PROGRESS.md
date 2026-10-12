# Perfil — progresso

- Concluídos: Blocos 1–4. Próximo: 5 — integração/regressão e correção da verificação automática relatada pelo usuário.
- Estado: Perfil/nome prontos; e-mail exige reautenticação, envio de link e consulta real ao Auth antes de sincronizar Firestore/CurrentUser. Recuperação excepcional por senha autorizada e implementada sem persistir senha.
- Arquivos principais do último bloco: FirebaseAuthService.Profile.cs, IAuthService, ProfilePage/ViewModel, FirebaseAuthServicePasswordTests, fixture e quatro fakes existentes (somente o novo membro da interface).
- Bloco imediatamente anterior: 4 — senha atual obrigatória antes da nova senha; mínimo de 6 caracteres e confirmação idêntica; atualização só no Authentication; tokens renovados; campos limpos; erro explícito se a senha mudar mas a sessão não puder ser salva.
- Reenvios/cooldown: inicial + 2 adicionais; 60 s após envios/verificações; automação e Cancelar independem do timer. Destino/contador/prazo no SecureStorage, sem senha, preservados após recriação/reinício. Resposta de envio perdida conta como tentativa por segurança.
- Validações: Bloco 4 — 35/35 testes focados; build Android com 0 erros e 0 avisos; diff sem erros. Regressão anterior: 183/183. Usuário validou troca de e-mail via Verificar; detecção automática falhou e será corrigida no Bloco 5.
- Commits: 1 `9f5d6de`; 2 `746eef7`; 3 `b133737`; 4 `feat(perfil): adiciona alteração segura de senha`.
- Pendências/riscos: testar senha/login/restauração reais e retestar retorno automático, gesto voltar, tokens invalidados e reenvios. Cancelamento é local e não revoga links; login/restauração reconciliam alterações posteriores. Proteção contra enumeração pode ocultar endereço já cadastrado/entrega do link.
- Preservados: AGENTS.md prévio e MauiProgram.cs local fora do commit; versão, User, Functions e regras sem alteração neste bloco.
