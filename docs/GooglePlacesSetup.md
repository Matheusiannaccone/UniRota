# Configuração do Google Maps Platform

O UniRota usa três Firebase callable functions autenticadas:

- `placesAutocomplete`
- `placeDetails`
- `computeRoute`

As chaves do Google Places e do Google Routes não ficam no aplicativo nem no
repositório. Elas são separadas e lidas pelas funções a partir do Google Cloud
Secret Manager.

## Configuração manual

1. No projeto Google Cloud associado ao Firebase, habilite faturamento e a
   **Places API (New)** e a **Routes API**.
2. Crie duas chaves de API dedicadas:
   - restrinja a chave de Places somente à Places API (New);
   - restrinja a chave de Routes somente à Routes API.
3. Instale/atualize o Firebase CLI e autentique-se.
4. Na raiz do repositório, instale as dependências das funções:

   ```text
   npm --prefix functions install
   ```

5. Grave a chave no Secret Manager sem adicioná-la a arquivos locais:

   ```text
   firebase functions:secrets:set GOOGLE_PLACES_API_KEY --project unirota-f0a63
   ```

   Em seguida, grave separadamente a chave de Routes:

   ```text
   firebase functions:secrets:set GOOGLE_ROUTES_API_KEY --project unirota-f0a63
   ```

6. Implante as funções necessárias:

   ```text
   firebase deploy --only functions:placesAutocomplete,functions:placeDetails,functions:computeRoute --project unirota-f0a63
   ```

As funções e o aplicativo estão configurados para a região
`southamerica-east1`. Se a região ou o projeto forem alterados, atualize
`FirebaseOptions` no aplicativo antes de gerar uma nova versão.

Nenhuma chave do Places ou do Routes deve ser adicionada ao `MauiProgram.cs`,
ao `FirebaseOptions` ou a arquivos `.env` versionados.

`computeRoute` aceita `originPlaceId`, `destinationPlaceId` e, opcionalmente,
até dois `intermediatePlaceIds`. A função exige um usuário Firebase autenticado
e solicita ao Routes API somente `routes.distanceMeters` e `routes.duration`.
O aplicativo persiste apenas a distância convertida para quilômetros em
`EstimatedDistanceKm`; duração e resultados de matching não são persistidos.

O matching geográfico usa os limites centralizados em `MatchingOptions`:

- diferença máxima entre horários: 30 minutos;
- desvio máximo de distância: 5 km;
- desvio máximo de duração: 15 minutos.

Ambos os limites de desvio precisam ser respeitados. Esses valores são iniciais
e conservadores e devem ser revistos com dados reais de uso antes de uma
distribuição ampla.

Antes de uma distribuição pública, publique termos de uso e política de
privacidade do aplicativo que incorporem os termos e a política de privacidade
do Google, conforme as políticas vigentes do Google Maps Platform.
