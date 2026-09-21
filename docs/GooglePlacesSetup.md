# Configuração do Google Places

O autocomplete do UniRota usa duas Firebase callable functions autenticadas:

- `placesAutocomplete`
- `placeDetails`

A chave do Google Places não fica no aplicativo nem no repositório. Ela é lida
pela função a partir do Google Cloud Secret Manager.

## Configuração manual

1. No projeto Google Cloud associado ao Firebase, habilite faturamento e a
   **Places API (New)**.
2. Crie uma chave de API dedicada ao serviço e restrinja-a à Places API (New).
3. Instale/atualize o Firebase CLI e autentique-se.
4. Na raiz do repositório, instale as dependências das funções:

   ```text
   npm --prefix functions install
   ```

5. Grave a chave no Secret Manager sem adicioná-la a arquivos locais:

   ```text
   firebase functions:secrets:set GOOGLE_PLACES_API_KEY --project unirota-f0a63
   ```

6. Implante somente as duas funções deste bloco:

   ```text
   firebase deploy --only functions:placesAutocomplete,functions:placeDetails --project unirota-f0a63
   ```

As funções e o aplicativo estão configurados para a região
`southamerica-east1`. Se a região ou o projeto forem alterados, atualize
`FirebaseOptions` no aplicativo antes de gerar uma nova versão.

Nenhuma chave do Places deve ser adicionada ao `MauiProgram.cs`, ao
`FirebaseOptions` ou a arquivos `.env` versionados.

Antes de uma distribuição pública, publique termos de uso e política de
privacidade do aplicativo que incorporem os termos e a política de privacidade
do Google, conforme as políticas vigentes do Google Maps Platform.
