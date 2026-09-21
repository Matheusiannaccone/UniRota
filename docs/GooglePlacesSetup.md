# Configuração do Google Maps Platform

O UniRota usa quatro Firebase callable functions autenticadas:

- `placesAutocomplete`
- `placeDetails`
- `placeCoordinates`
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
   firebase deploy --only functions:placesAutocomplete,functions:placeDetails,functions:placeCoordinates,functions:computeRoute --project unirota-f0a63
   ```

As funções e o aplicativo estão configurados para a região
`southamerica-east1`. Se a região ou o projeto forem alterados, atualize
`FirebaseOptions` no aplicativo antes de gerar uma nova versão.

Nenhuma chave do Places ou do Routes deve ser adicionada ao `MauiProgram.cs`,
ao `FirebaseOptions` ou a arquivos `.env` versionados.

`computeRoute` aceita `originPlaceId`, `destinationPlaceId` e, opcionalmente,
até dois `intermediatePlaceIds`. A função exige um usuário Firebase autenticado
e usa o FieldMask
`routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline`, com
qualidade `OVERVIEW`, na mesma chamada usada para distância e duração. A
polyline fica somente no `MatchResult` em memória e não é persistida. O
aplicativo persiste apenas a distância convertida para quilômetros em
`EstimatedDistanceKm`; duração, polyline e resultados de matching não são
persistidos.

`placeCoordinates` aceita no máximo quatro Place IDs, remove duplicações e
solicita somente o campo `location` do Place Details (New). Essa callable é
usada apenas ao abrir os detalhes visuais do trajeto para posicionar os pins.

## Maps SDK for Android

O mapa nativo usa uma terceira chave, dedicada ao **Maps SDK for Android**.
Ela não deve reutilizar `GOOGLE_PLACES_API_KEY` nem
`GOOGLE_ROUTES_API_KEY`.

1. Habilite **Maps SDK for Android** no Google Cloud.
2. Crie uma chave dedicada.
3. Em **Application restrictions**, selecione **Android apps**.
4. Cadastre o package name/ApplicationId definitivo:
   `io.github.matheusiannaccone.unirota`.
5. Cadastre o SHA-1 do certificado de debug usado localmente e, para uma
   distribuição, o SHA-1 do certificado de release/Google Play App Signing.
6. Em **API restrictions**, permita somente **Maps SDK for Android**.
7. Disponibilize a chave localmente antes de compilar, sem gravá-la no Git:

   ```powershell
   $env:GOOGLE_MAPS_ANDROID_API_KEY = "SUA_CHAVE_ANDROID_RESTRITA"
   dotnet build UniRota/UniRota.csproj -f net8.0-android
   ```

   Como alternativa, passe temporariamente
   `-p:GoogleMapsAndroidApiKey=SUA_CHAVE_ANDROID_RESTRITA` ao `dotnet build`.

O `AndroidManifest.xml` contém somente o placeholder
`${GOOGLE_MAPS_ANDROID_API_KEY}`. Sem configuração local, o projeto compila
com um valor sentinela, mas o Google Maps não renderiza o mapa autenticado.
Nenhuma permissão de localização é usada neste bloco.

A chave Android é injetada apenas no build e ficará visível no APK, como é
esperado para uma chave cliente. A proteção efetiva depende das restrições de
aplicativo Android (package + SHA-1) e de API descritas acima. Ela não deve ser
armazenada no Secret Manager usado pelas Functions.

No iOS, o controle mantém compatibilidade estrutural por meio do MapKit. A
configuração e validação de distribuição iOS devem ser realizadas em ambiente
macOS antes da publicação.

O matching geográfico usa os limites centralizados em `MatchingOptions`:

- diferença máxima entre horários: 30 minutos;
- desvio máximo de distância: 5 km;
- desvio máximo de duração: 15 minutos.

Ambos os limites de desvio precisam ser respeitados. Esses valores são iniciais
e conservadores e devem ser revistos com dados reais de uso antes de uma
distribuição ampla.

As chamadas upstream de Places e Routes possuem timeout de 10 segundos nas
Functions, que por sua vez têm limite de execução de 15 segundos. O aplicativo
também aplica timeout às chamadas das callables e apresenta mensagens seguras
para indisponibilidade, quota, sessão expirada e respostas inválidas.

Antes de uma distribuição pública, publique termos de uso e política de
privacidade do aplicativo que incorporem os termos e a política de privacidade
do Google, conforme as políticas vigentes do Google Maps Platform.
