using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.Services.Firebase;

public sealed class FirebaseRideRequestService : IRideRequestService
{
    private const string FirestoreBaseUrl = "https://firestore.googleapis.com/v1";
    private const string CollectionName = "rideRequests";
    private const int MaxConcurrencyAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly IAuthService _authService;
    private readonly IRouteService _routeService;

    public FirebaseRideRequestService(
        HttpClient httpClient,
        FirebaseOptions options,
        IAuthService authService,
        IRouteService routeService)
    {
        _httpClient = httpClient;
        _options = options;
        _authService = authService;
        _routeService = routeService;
    }

    public async Task<RideRequest> CreateAsync(
        string passengerRouteId,
        MatchResult match,
        RideRequestType type,
        DateOnly? requestedDate,
        decimal suggestedPrice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        EnsureFirebaseIsConfigured();

        if (suggestedPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(suggestedPrice),
                "O preço sugerido deve ser maior que zero.");
        }

        var user = GetAuthenticatedUser();
        var userId = user.Id;
        var normalizedPassengerRouteId = GetRequiredRouteId(
            passengerRouteId,
            nameof(passengerRouteId));
        var driverRoute = match.DriverRoute
            ?? throw new ArgumentException(
                "O resultado selecionado não contém uma rota de motorista.",
                nameof(match));
        var normalizedDriverRouteId = GetRequiredRouteId(
            driverRoute.Id,
            nameof(match));

        ValidateDriverRoute(driverRoute, userId);

        var compatibleDays = RideRequestRules.ValidateForCreation(
            type,
            match.CompatibleDays,
            requestedDate,
            DateOnly.FromDateTime(DateTime.Today));

        await EnsurePassengerRouteBelongsToUserAsync(
            normalizedPassengerRouteId,
            userId,
            cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var requestToCreate = new RideRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            PassengerUserId = userId,
            PassengerUserName = user.Name,
            DriverUserId = driverRoute.UserId,
            DriverUserName = driverRoute.UserName?.Trim() ?? string.Empty,
            PassengerRouteId = normalizedPassengerRouteId,
            DriverRouteId = normalizedDriverRouteId,
            CompatibleDays = compatibleDays,
            Type = type,
            Status = RideRequestStatus.Pending,
            RequestedDate = requestedDate,
            SuggestedPrice = suggestedPrice,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            EnsureCurrentUserHasNotChanged(userId);

            var routeDocument = await GetDocumentAsync(
                BuildWeeklyRouteDocumentUrl(normalizedDriverRouteId),
                idToken,
                cancellationToken);
            var routeSnapshot = ConvertDriverRouteSnapshot(routeDocument);
            ValidateDriverRouteSnapshot(
                routeSnapshot,
                driverRoute,
                userId);

            var pendingRequests = await GetMyPendingRequestsCoreAsync(
                userId,
                idToken,
                cancellationToken);

            if (pendingRequests.Any(
                    request => IsSameRoutePair(
                        request,
                        normalizedPassengerRouteId,
                        normalizedDriverRouteId)))
            {
                throw new InvalidOperationException(
                    "Já existe uma solicitação pendente para estas rotas.");
            }

            var nextRevision = GetNextRequestRevision(
                routeSnapshot.RequestRevision);
            var writes = new object[]
            {
                CreateNewRideRequestWrite(requestToCreate),
                CreateRouteRevisionWrite(routeSnapshot, nextRevision)
            };
            var response = await CommitAsync(
                writes,
                idToken,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                EnsureCurrentUserHasNotChanged(userId);
                return requestToCreate;
            }

            if (IsConcurrencyConflict(response) && attempt < MaxConcurrencyAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
                continue;
            }

            if (IsConcurrencyConflict(response))
            {
                throw CreateConcurrencyException();
            }

            EnsureSuccess(response);
        }

        throw CreateConcurrencyException();
    }

    public async Task<bool> HasPendingRequestAsync(
        string passengerRouteId,
        string driverRouteId,
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var normalizedPassengerRouteId = GetRequiredRouteId(
            passengerRouteId,
            nameof(passengerRouteId));
        var normalizedDriverRouteId = GetRequiredRouteId(
            driverRouteId,
            nameof(driverRouteId));
        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var pendingRequests = await GetMyPendingRequestsCoreAsync(
            userId,
            idToken,
            cancellationToken);

        return pendingRequests.Any(
            request => IsSameRoutePair(
                request,
                normalizedPassengerRouteId,
                normalizedDriverRouteId));
    }

    public async Task<IReadOnlyList<RideRequest>> GetMyPendingRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        return await GetMyPendingRequestsCoreAsync(
            userId,
            idToken,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RideRequest>> GetMyActiveRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var requestBody = CreatePassengerActiveRequestsQuery(userId);
        var requests = await RunRideRequestQueryAsync(
            requestBody,
            idToken,
            userId,
            cancellationToken);

        foreach (var request in requests)
        {
            if (!string.Equals(
                    request.PassengerUserId,
                    userId,
                    StringComparison.Ordinal)
                || request.Status is not (
                    RideRequestStatus.Pending or RideRequestStatus.Accepted))
            {
                throw new InvalidOperationException(
                    "O Firebase retornou uma solicitação indisponível para o passageiro autenticado.");
            }
        }

        return OrderRequests(requests);
    }

    public async Task<IReadOnlyList<RideRequest>> GetReceivedPendingRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var requests = await GetDriverPendingRequestsCoreAsync(
            userId,
            idToken,
            cancellationToken);

        return OrderRequests(requests);
    }

    public async Task<IReadOnlyList<RideRequest>> GetMyAcceptedRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var passengerRequests = await RunRideRequestQueryAsync(
            CreateOwnedStatusRequestsQuery(
                "passengerUserId",
                userId,
                "accepted"),
            idToken,
            userId,
            cancellationToken);

        EnsureCurrentUserHasNotChanged(userId);

        var driverRequests = await RunRideRequestQueryAsync(
            CreateOwnedStatusRequestsQuery(
                "driverUserId",
                userId,
                "accepted"),
            idToken,
            userId,
            cancellationToken);

        var requests = passengerRequests
            .Concat(driverRequests)
            .DistinctBy(request => request.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var request in requests)
        {
            var belongsToUser = string.Equals(
                                    request.PassengerUserId,
                                    userId,
                                    StringComparison.Ordinal)
                                || string.Equals(
                                    request.DriverUserId,
                                    userId,
                                    StringComparison.Ordinal);

            if (!belongsToUser || request.Status != RideRequestStatus.Accepted)
            {
                throw new InvalidOperationException(
                    "O Firebase retornou uma solicitação confirmada que não pertence ao usuário autenticado.");
            }
        }

        return OrderRequests(requests);
    }

    public async Task RejectAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var normalizedRequestId = GetRequiredRequestId(requestId);
        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            var document = await GetDocumentAsync(
                BuildRideRequestDocumentUrl(normalizedRequestId),
                idToken,
                cancellationToken);
            var rideRequest = ConvertDocument(document);
            EnsureDriverCanProcessRequest(rideRequest, userId);
            EnsureCurrentUserHasNotChanged(userId);

            var response = await CommitAsync(
                [CreateStatusWrite(
                    document,
                    RideRequestStatus.Rejected)],
                idToken,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                EnsureCurrentUserHasNotChanged(userId);
                return;
            }

            if (IsConcurrencyConflict(response) && attempt < MaxConcurrencyAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
                continue;
            }

            if (IsConcurrencyConflict(response))
            {
                throw CreateConcurrencyException();
            }

            EnsureSuccess(response);
        }

        throw CreateConcurrencyException();
    }

    public async Task AcceptAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var normalizedRequestId = GetRequiredRequestId(requestId);
        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            var requestDocument = await GetDocumentAsync(
                BuildRideRequestDocumentUrl(normalizedRequestId),
                idToken,
                cancellationToken);
            var rideRequest = ConvertDocument(requestDocument);
            EnsureDriverCanProcessRequest(rideRequest, userId);

            var routeDocument = await GetDocumentAsync(
                BuildWeeklyRouteDocumentUrl(rideRequest.DriverRouteId),
                idToken,
                cancellationToken);
            var routeSnapshot = ConvertDriverRouteSnapshot(routeDocument);
            ValidateDriverRouteForAcceptance(
                routeSnapshot,
                rideRequest,
                userId);

            var remainingSeats = RideRequestRules
                .GetRemainingSeatsAfterAcceptance(routeSnapshot.AvailableSeats);
            var pendingRouteRequests = await GetPendingRequestsForRouteAsync(
                rideRequest.DriverRouteId,
                userId,
                idToken,
                cancellationToken);

            var selectedDocument = pendingRouteRequests.FirstOrDefault(
                item => string.Equals(
                    GetDocumentId(item.Name),
                    normalizedRequestId,
                    StringComparison.Ordinal));

            if (selectedDocument is null)
            {
                throw new InvalidOperationException(
                    "Esta solicitação já foi processada.");
            }

            var writes = new List<object>
            {
                CreateStatusWrite(
                    selectedDocument,
                    RideRequestStatus.Accepted),
                CreateAvailableSeatsWrite(routeSnapshot, remainingSeats)
            };

            if (remainingSeats == 0)
            {
                writes.AddRange(
                    pendingRouteRequests
                        .Where(document => !string.Equals(
                            GetDocumentId(document.Name),
                            normalizedRequestId,
                            StringComparison.Ordinal))
                        .Select(document => CreateStatusWrite(
                            document,
                            RideRequestStatus.Rejected)));
            }

            var response = await CommitAsync(
                writes,
                idToken,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                EnsureCurrentUserHasNotChanged(userId);
                return;
            }

            if (IsConcurrencyConflict(response) && attempt < MaxConcurrencyAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
                continue;
            }

            if (IsConcurrencyConflict(response))
            {
                throw CreateConcurrencyException();
            }

            EnsureSuccess(response);
        }

        throw CreateConcurrencyException();
    }

    private async Task<IReadOnlyList<RideRequest>> GetMyPendingRequestsCoreAsync(
        string userId,
        string idToken,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            structuredQuery = new
            {
                from = new[]
                {
                    new { collectionId = CollectionName }
                },
                where = new
                {
                    compositeFilter = new
                    {
                        op = "AND",
                        filters = new object[]
                        {
                            new
                            {
                                fieldFilter = new
                                {
                                    field = new { fieldPath = "passengerUserId" },
                                    op = "EQUAL",
                                    value = new { stringValue = userId }
                                }
                            },
                            new
                            {
                                fieldFilter = new
                                {
                                    field = new { fieldPath = "status" },
                                    op = "EQUAL",
                                    value = new { stringValue = "pending" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var requests = await RunRideRequestQueryAsync(
            requestBody,
            idToken,
            userId,
            cancellationToken);

        foreach (var rideRequest in requests)
        {
            EnsurePendingRequestBelongsToUser(rideRequest, userId);
        }

        return OrderRequests(requests);
    }

    private async Task<IReadOnlyList<RideRequest>> GetDriverPendingRequestsCoreAsync(
        string userId,
        string idToken,
        CancellationToken cancellationToken)
    {
        var requests = await RunRideRequestQueryAsync(
            CreatePendingRequestsQuery("driverUserId", userId),
            idToken,
            userId,
            cancellationToken);

        foreach (var request in requests)
        {
            if (!string.Equals(
                    request.DriverUserId,
                    userId,
                    StringComparison.Ordinal)
                || request.Status != RideRequestStatus.Pending)
            {
                throw new InvalidOperationException(
                    "O Firebase retornou uma solicitação que não pertence ao motorista autenticado.");
            }
        }

        return requests;
    }

    private async Task<IReadOnlyList<FirestoreDocumentDto>>
        GetPendingRequestsForRouteAsync(
            string driverRouteId,
            string driverUserId,
            string idToken,
            CancellationToken cancellationToken)
    {
        var documents = await RunRideRequestDocumentQueryAsync(
            CreatePendingRequestsQuery("driverRouteId", driverRouteId),
            idToken,
            driverUserId,
            cancellationToken);

        foreach (var document in documents)
        {
            var request = ConvertDocument(document);

            if (!string.Equals(
                    request.DriverRouteId,
                    driverRouteId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    request.DriverUserId,
                    driverUserId,
                    StringComparison.Ordinal)
                || request.Status != RideRequestStatus.Pending)
            {
                throw new InvalidOperationException(
                    "O Firebase retornou uma solicitação incompatível com a rota do motorista.");
            }
        }

        return documents;
    }

    private async Task<IReadOnlyList<RideRequest>> RunRideRequestQueryAsync(
        object requestBody,
        string idToken,
        string expectedUserId,
        CancellationToken cancellationToken)
    {
        var documents = await RunRideRequestDocumentQueryAsync(
            requestBody,
            idToken,
            expectedUserId,
            cancellationToken);

        return documents
            .Select(ConvertDocument)
            .ToArray();
    }

    private async Task<IReadOnlyList<FirestoreDocumentDto>>
        RunRideRequestDocumentQueryAsync(
            object requestBody,
            string idToken,
            string expectedUserId,
            CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            BuildRunQueryUrl(),
            requestBody);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        EnsureCurrentUserHasNotChanged(expectedUserId);

        return DeserializeResponse<List<RunQueryResultDto>>(response.Content)
            .Where(result => result.Document is not null)
            .Select(result => result.Document!)
            .ToArray();
    }

    private static object CreatePendingRequestsQuery(
        string ownerField,
        string ownerValue)
    {
        return CreateOwnedStatusRequestsQuery(
            ownerField,
            ownerValue,
            "pending");
    }

    private static object CreateOwnedStatusRequestsQuery(
        string ownerField,
        string ownerValue,
        string statusValue)
    {
        return CreateRequestQuery(
            new
            {
                fieldFilter = new
                {
                    field = new { fieldPath = ownerField },
                    op = "EQUAL",
                    value = new { stringValue = ownerValue }
                }
            },
            new
            {
                fieldFilter = new
                {
                    field = new { fieldPath = "status" },
                    op = "EQUAL",
                    value = new { stringValue = statusValue }
                }
            });
    }

    private static object CreatePassengerActiveRequestsQuery(string userId)
    {
        return CreateRequestQuery(
            new
            {
                fieldFilter = new
                {
                    field = new { fieldPath = "passengerUserId" },
                    op = "EQUAL",
                    value = new { stringValue = userId }
                }
            },
            new
            {
                fieldFilter = new
                {
                    field = new { fieldPath = "status" },
                    op = "IN",
                    value = new
                    {
                        arrayValue = new
                        {
                            values = new[]
                            {
                                new { stringValue = "pending" },
                                new { stringValue = "accepted" }
                            }
                        }
                    }
                }
            });
    }

    private static object CreateRequestQuery(params object[] filters)
    {
        return new
        {
            structuredQuery = new
            {
                from = new[]
                {
                    new { collectionId = CollectionName }
                },
                where = new
                {
                    compositeFilter = new
                    {
                        op = "AND",
                        filters
                    }
                }
            }
        };
    }

    private static IReadOnlyList<RideRequest> OrderRequests(
        IEnumerable<RideRequest> requests)
    {
        return requests
            .OrderByDescending(request => request.CreatedAtUtc)
            .ThenBy(request => request.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task EnsurePassengerRouteBelongsToUserAsync(
        string passengerRouteId,
        string userId,
        CancellationToken cancellationToken)
    {
        var routes = await _routeService.GetMyRoutesAsync(cancellationToken);
        var passengerRoute = routes.FirstOrDefault(
            route => string.Equals(
                route.Id,
                passengerRouteId,
                StringComparison.Ordinal));

        if (passengerRoute is null
            || !string.Equals(
                passengerRoute.UserId,
                userId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A rota de passageiro selecionada não pertence ao usuário autenticado.");
        }

        if (passengerRoute.Role != RouteRole.Passenger)
        {
            throw new InvalidOperationException(
                "A solicitação deve partir de uma rota de passageiro.");
        }
    }

    private static void ValidateDriverRoute(
        WeeklyRoute driverRoute,
        string passengerUserId)
    {
        if (driverRoute.Role != RouteRole.Driver)
        {
            throw new ArgumentException(
                "A solicitação deve ser direcionada a uma rota de motorista.",
                nameof(driverRoute));
        }

        if (string.IsNullOrWhiteSpace(driverRoute.UserId))
        {
            throw new ArgumentException(
                "A rota de motorista não possui um usuário válido.",
                nameof(driverRoute));
        }

        if (string.Equals(
                driverRoute.UserId,
                passengerUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Não é possível solicitar carona para uma rota do próprio usuário.");
        }
    }

    private static void ValidateDriverRouteSnapshot(
        DriverRouteSnapshot snapshot,
        WeeklyRoute expectedRoute,
        string passengerUserId)
    {
        if (snapshot.Role != RouteRole.Driver
            || !string.Equals(
                snapshot.Id,
                expectedRoute.Id,
                StringComparison.Ordinal)
            || !string.Equals(
                snapshot.UserId,
                expectedRoute.UserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A rota de motorista selecionada foi alterada ou não é mais válida.");
        }

        if (string.Equals(
                snapshot.UserId,
                passengerUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Não é possível solicitar carona para uma rota do próprio usuário.");
        }

        if (snapshot.AvailableSeats <= 0)
        {
            throw new InvalidOperationException(
                "A rota selecionada não possui mais vagas disponíveis.");
        }
    }

    private static void ValidateDriverRouteForAcceptance(
        DriverRouteSnapshot snapshot,
        RideRequest request,
        string driverUserId)
    {
        if (!string.Equals(
                snapshot.Id,
                request.DriverRouteId,
                StringComparison.Ordinal)
            || !string.Equals(
                snapshot.UserId,
                driverUserId,
                StringComparison.Ordinal)
            || snapshot.Role != RouteRole.Driver)
        {
            throw new InvalidOperationException(
                "A rota da solicitação não pertence ao motorista autenticado.");
        }
    }

    private static void EnsureDriverCanProcessRequest(
        RideRequest request,
        string driverUserId)
    {
        if (!string.Equals(
                request.DriverUserId,
                driverUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Esta solicitação não pertence ao motorista autenticado.");
        }

        RideRequestRules.EnsurePending(request.Status);
    }

    private static long GetNextRequestRevision(long currentRevision)
    {
        try
        {
            return checked(currentRevision + 1);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                "A rota atingiu o limite técnico de solicitações suportadas.",
                exception);
        }
    }

    private object CreateNewRideRequestWrite(RideRequest request)
    {
        return new
        {
            update = new
            {
                name = BuildRideRequestDocumentName(request.Id),
                fields = CreateFirestoreFields(request)
            },
            currentDocument = new { exists = false }
        };
    }

    private static object CreateRouteRevisionWrite(
        DriverRouteSnapshot route,
        long requestRevision)
    {
        return new
        {
            update = new
            {
                name = route.DocumentName,
                fields = new Dictionary<string, object>
                {
                    ["requestRevision"] = new
                    {
                        integerValue = requestRevision.ToString(
                            CultureInfo.InvariantCulture)
                    }
                }
            },
            updateMask = new
            {
                fieldPaths = new[] { "requestRevision" }
            },
            currentDocument = new { updateTime = route.UpdateTime }
        };
    }

    private static object CreateAvailableSeatsWrite(
        DriverRouteSnapshot route,
        int availableSeats)
    {
        return new
        {
            update = new
            {
                name = route.DocumentName,
                fields = new Dictionary<string, object>
                {
                    ["availableSeats"] = new
                    {
                        integerValue = availableSeats.ToString(
                            CultureInfo.InvariantCulture)
                    }
                }
            },
            updateMask = new
            {
                fieldPaths = new[] { "availableSeats" }
            },
            currentDocument = new { updateTime = route.UpdateTime }
        };
    }

    private static object CreateStatusWrite(
        FirestoreDocumentDto document,
        RideRequestStatus status)
    {
        return new
        {
            update = new
            {
                name = GetRequiredDocumentName(document),
                fields = new Dictionary<string, object>
                {
                    ["status"] = new { stringValue = SerializeStatus(status) }
                }
            },
            updateMask = new
            {
                fieldPaths = new[] { "status" }
            },
            currentDocument = new
            {
                updateTime = GetRequiredUpdateTime(document)
            }
        };
    }

    private static bool IsSameRoutePair(
        RideRequest request,
        string passengerRouteId,
        string driverRouteId)
    {
        return string.Equals(
                   request.PassengerRouteId,
                   passengerRouteId,
                   StringComparison.Ordinal)
               && string.Equals(
                   request.DriverRouteId,
                   driverRouteId,
                   StringComparison.Ordinal);
    }

    private static Dictionary<string, object> CreateFirestoreFields(
        RideRequest request)
    {
        return new Dictionary<string, object>
        {
            ["passengerUserId"] = new { stringValue = request.PassengerUserId },
            ["passengerUserName"] = new { stringValue = request.PassengerUserName },
            ["driverUserId"] = new { stringValue = request.DriverUserId },
            ["driverUserName"] = new { stringValue = request.DriverUserName },
            ["passengerRouteId"] = new { stringValue = request.PassengerRouteId },
            ["driverRouteId"] = new { stringValue = request.DriverRouteId },
            ["compatibleDays"] = new
            {
                arrayValue = new
                {
                    values = request.CompatibleDays
                        .Select(day => new { stringValue = day.ToString() })
                        .ToArray()
                }
            },
            ["type"] = new { stringValue = SerializeType(request.Type) },
            ["status"] = new { stringValue = SerializeStatus(request.Status) },
            ["requestedDate"] = request.RequestedDate is not null
                ? new
                {
                    stringValue = request.RequestedDate.Value.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture)
                }
                : new { nullValue = (object?)null },
            ["suggestedPrice"] = new
            {
                doubleValue = (double)request.SuggestedPrice
            },
            ["createdAtUtc"] = new
            {
                timestampValue = request.CreatedAtUtc
                    .ToUniversalTime()
                    .ToString("O")
            }
        };
    }

    private static RideRequest ConvertDocument(FirestoreDocumentDto document)
    {
        var id = GetDocumentId(document.Name);
        var fields = document.Fields;
        var compatibleDays = GetRequiredDaysOfWeekField(
            fields,
            "compatibleDays",
            id);
        var type = ParseType(GetRequiredStringField(fields, "type", id), id);
        var requestedDate = GetOptionalDateField(fields, "requestedDate", id);

        ValidatePersistedTypeAndDate(
            type,
            requestedDate,
            compatibleDays,
            id);

        return new RideRequest
        {
            Id = id,
            PassengerUserId = GetRequiredStringField(
                fields,
                "passengerUserId",
                id),
            PassengerUserName = GetOptionalStringField(
                fields,
                "passengerUserName"),
            DriverUserId = GetRequiredStringField(fields, "driverUserId", id),
            DriverUserName = GetOptionalStringField(fields, "driverUserName"),
            PassengerRouteId = GetRequiredStringField(
                fields,
                "passengerRouteId",
                id),
            DriverRouteId = GetRequiredStringField(fields, "driverRouteId", id),
            CompatibleDays = compatibleDays,
            Type = type,
            Status = ParseStatus(
                GetRequiredStringField(fields, "status", id),
                id),
            RequestedDate = requestedDate,
            SuggestedPrice = GetRequiredPositiveDecimalField(
                fields,
                "suggestedPrice",
                id),
            CreatedAtUtc = GetRequiredTimestampField(
                fields,
                "createdAtUtc",
                id)
        };
    }

    private static DriverRouteSnapshot ConvertDriverRouteSnapshot(
        FirestoreDocumentDto document)
    {
        var id = GetDocumentId(document.Name);
        var fields = document.Fields;
        var role = ParseRouteRole(
            GetRequiredStringField(fields, "role", id),
            id);
        var availableSeats = GetRequiredInt32Field(
            fields,
            "availableSeats",
            id);

        return new DriverRouteSnapshot(
            id,
            GetRequiredStringField(fields, "userId", id),
            role,
            availableSeats,
            GetOptionalNonNegativeInt64Field(
                fields,
                "requestRevision",
                id),
            GetRequiredDocumentName(document),
            GetRequiredUpdateTime(document));
    }

    private static void ValidatePersistedTypeAndDate(
        RideRequestType type,
        DateOnly? requestedDate,
        IReadOnlyList<DayOfWeek> compatibleDays,
        string documentId)
    {
        if (type == RideRequestType.Once
            && (requestedDate is null
                || !compatibleDays.Contains(requestedDate.Value.DayOfWeek)))
        {
            throw CreateInvalidDocumentException(
                documentId,
                "a data única não corresponde aos dias compatíveis");
        }

        if (type == RideRequestType.Weekly && requestedDate is not null)
        {
            throw CreateInvalidDocumentException(
                documentId,
                "uma solicitação semanal não deve possuir 'requestedDate'");
        }
    }

    private static IReadOnlyList<DayOfWeek> GetRequiredDaysOfWeekField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field.ArrayValue is null
            || field.ArrayValue.Values.Count == 0)
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo obrigatório '{fieldName}' está ausente");
        }

        var days = new List<DayOfWeek>();

        foreach (var value in field.ArrayValue.Values)
        {
            if (string.IsNullOrWhiteSpace(value.StringValue)
                || !Enum.TryParse<DayOfWeek>(
                    value.StringValue,
                    ignoreCase: true,
                    out var day)
                || !Enum.IsDefined(typeof(DayOfWeek), day))
            {
                throw CreateInvalidDocumentException(
                    documentId,
                    $"o campo '{fieldName}' contém um dia inválido");
            }

            days.Add(day);
        }

        return days
            .Distinct()
            .OrderBy(day => day)
            .ToArray();
    }

    private static string GetRequiredStringField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && !string.IsNullOrWhiteSpace(field.StringValue))
        {
            return field.StringValue;
        }

        throw CreateInvalidDocumentException(
            documentId,
            $"o campo obrigatório '{fieldName}' está ausente");
    }

    private static string GetOptionalStringField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        return fields.TryGetValue(fieldName, out var field)
            ? field.StringValue ?? string.Empty
            : string.Empty;
    }

    private static DateOnly? GetOptionalDateField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field.StringValue is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(
                field.StringValue,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw CreateInvalidDocumentException(
            documentId,
            $"o campo '{fieldName}' não contém uma data válida");
    }

    private static DateTimeOffset GetRequiredTimestampField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && field.TimestampValue is not null)
        {
            return field.TimestampValue.Value.ToUniversalTime();
        }

        throw CreateInvalidDocumentException(
            documentId,
            $"o campo obrigatório '{fieldName}' está ausente");
    }

    private static int GetRequiredInt32Field(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        var value = GetRequiredInt64Field(fields, fieldName, documentId);

        if (value is < int.MinValue or > int.MaxValue)
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' excede o intervalo suportado");
        }

        return (int)value;
    }

    private static decimal GetRequiredPositiveDecimalField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field.DoubleValue.ValueKind != JsonValueKind.Number
            || !field.DoubleValue.TryGetDecimal(out var value))
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' não contém um número decimal válido");
        }

        if (value <= 0m)
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' deve ser maior que zero");
        }

        return value;
    }

    private static long GetOptionalNonNegativeInt64Field(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.ContainsKey(fieldName))
        {
            return 0;
        }

        var value = GetRequiredInt64Field(fields, fieldName, documentId);

        if (value < 0)
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' não pode ser negativo");
        }

        return value;
    }

    private static long GetRequiredInt64Field(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field))
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo obrigatório '{fieldName}' está ausente");
        }

        if (field.IntegerValue.ValueKind == JsonValueKind.String
            && long.TryParse(
                field.IntegerValue.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stringValue))
        {
            return stringValue;
        }

        if (field.IntegerValue.ValueKind == JsonValueKind.Number
            && field.IntegerValue.TryGetInt64(out var numericValue))
        {
            return numericValue;
        }

        throw CreateInvalidDocumentException(
            documentId,
            $"o campo '{fieldName}' não contém um inteiro válido");
    }

    private static string SerializeType(RideRequestType type)
    {
        return type switch
        {
            RideRequestType.Once => "once",
            RideRequestType.Weekly => "weekly",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static RideRequestType ParseType(string value, string documentId)
    {
        return value.ToLowerInvariant() switch
        {
            "once" => RideRequestType.Once,
            "weekly" => RideRequestType.Weekly,
            _ => throw CreateInvalidDocumentException(
                documentId,
                $"o campo 'type' contém o valor inválido '{value}'")
        };
    }

    private static string SerializeStatus(RideRequestStatus status)
    {
        return status switch
        {
            RideRequestStatus.Pending => "pending",
            RideRequestStatus.Accepted => "accepted",
            RideRequestStatus.Rejected => "rejected",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static RideRequestStatus ParseStatus(
        string value,
        string documentId)
    {
        return value.ToLowerInvariant() switch
        {
            "pending" => RideRequestStatus.Pending,
            "accepted" => RideRequestStatus.Accepted,
            "rejected" => RideRequestStatus.Rejected,
            _ => throw CreateInvalidDocumentException(
                documentId,
                $"o campo 'status' contém o valor inválido '{value}'")
        };
    }

    private static RouteRole ParseRouteRole(string value, string documentId)
    {
        return value.ToLowerInvariant() switch
        {
            "driver" => RouteRole.Driver,
            "passenger" => RouteRole.Passenger,
            _ => throw CreateInvalidDocumentException(
                documentId,
                $"o campo 'role' contém o valor inválido '{value}'")
        };
    }

    private static void EnsurePendingRequestBelongsToUser(
        RideRequest request,
        string expectedUserId)
    {
        if (!string.Equals(
                request.PassengerUserId,
                expectedUserId,
                StringComparison.Ordinal)
            || request.Status != RideRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                "O Firebase retornou uma solicitação que não pertence ao passageiro autenticado.");
        }
    }

    private User GetAuthenticatedUser()
    {
        var user = _authService.CurrentUser
            ?? throw new InvalidOperationException(
                "Não há uma sessão autenticada. Entre novamente para continuar.");

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException(
                "A sessão autenticada não contém um identificador de usuário válido.");
        }

        return user;
    }

    private void EnsureCurrentUserHasNotChanged(string expectedUserId)
    {
        if (!string.Equals(
                _authService.CurrentUser?.Id,
                expectedUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A sessão autenticada foi alterada durante a operação. Tente novamente.");
        }
    }

    private static string GetRequiredRouteId(
        string? routeId,
        string parameterName)
    {
        var normalizedRouteId = routeId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedRouteId))
        {
            throw new ArgumentException(
                "Informe um identificador de rota válido.",
                parameterName);
        }

        return normalizedRouteId;
    }

    private static string GetRequiredRequestId(string? requestId)
    {
        var normalizedRequestId = requestId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedRequestId))
        {
            throw new ArgumentException(
                "Informe o identificador da solicitação.",
                nameof(requestId));
        }

        return normalizedRequestId;
    }

    private static string GetRequiredDocumentName(FirestoreDocumentDto document)
    {
        return !string.IsNullOrWhiteSpace(document.Name)
            ? document.Name
            : throw new InvalidOperationException(
                "O Firebase não retornou o caminho esperado para o documento.");
    }

    private static string GetRequiredUpdateTime(FirestoreDocumentDto document)
    {
        return !string.IsNullOrWhiteSpace(document.UpdateTime)
            ? document.UpdateTime
            : throw new InvalidOperationException(
                "O Firebase não retornou a versão esperada para o documento.");
    }

    private static string GetDocumentId(string? documentName)
    {
        if (!string.IsNullOrWhiteSpace(documentName))
        {
            var separatorIndex = documentName.LastIndexOf('/');

            if (separatorIndex >= 0 && separatorIndex < documentName.Length - 1)
            {
                return Uri.UnescapeDataString(documentName[(separatorIndex + 1)..]);
            }
        }

        throw new InvalidOperationException(
            "O Firebase não retornou o identificador esperado para a solicitação.");
    }

    private static InvalidOperationException CreateInvalidDocumentException(
        string documentId,
        string reason)
    {
        return new InvalidOperationException(
            $"O documento de solicitação '{documentId}' é inválido: {reason}.");
    }

    private async Task<FirestoreDocumentDto> GetDocumentAsync(
        string url,
        string idToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);

        return DeserializeResponse<FirestoreDocumentDto>(response.Content);
    }

    private async Task<FirebaseHttpResponse> CommitAsync(
        IEnumerable<object> writes,
        string idToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            BuildCommitUrl(),
            new { writes = writes.ToArray() });
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", idToken);

        return await SendAsync(request, cancellationToken);
    }

    private static bool IsConcurrencyConflict(FirebaseHttpResponse response)
    {
        if (response.IsSuccessStatusCode)
        {
            return false;
        }

        var code = ExtractFirebaseErrorCode(response.Content);
        return code is "ABORTED" or "FAILED_PRECONDITION";
    }

    private static Task DelayBeforeRetryAsync(
        int completedAttempt,
        CancellationToken cancellationToken)
    {
        return Task.Delay(
            TimeSpan.FromMilliseconds(100 * completedAttempt),
            cancellationToken);
    }

    private static InvalidOperationException CreateConcurrencyException()
    {
        return new InvalidOperationException(
            "A solicitação foi alterada por outra operação. Recarregue a lista e tente novamente.");
    }

    private async Task<FirebaseHttpResponse> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new FirebaseHttpResponse(response.IsSuccessStatusCode, content);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Não foi possível conectar aos serviços do Firebase. Verifique sua conexão.",
                exception);
        }
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string url,
        object requestBody)
    {
        return new HttpRequestMessage(method, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
    }

    private static void EnsureSuccess(FirebaseHttpResponse response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var firebaseCode = ExtractFirebaseErrorCode(response.Content);
        var message = firebaseCode switch
        {
            "PERMISSION_DENIED" =>
                "O Firebase negou acesso às solicitações de carona.",
            "UNAUTHENTICATED" =>
                "Sua sessão não é válida. Entre novamente para continuar.",
            "FAILED_PRECONDITION" =>
                "A consulta de solicitações requer configuração adicional no Firebase.",
            "ABORTED" =>
                "A solicitação foi alterada por outra operação. Recarregue e tente novamente.",
            "NOT_FOUND" =>
                "A solicitação ou rota relacionada não foi encontrada.",
            _ => "Não foi possível concluir a operação de solicitação no Firebase."
        };

        var exception = new InvalidOperationException(message);
        exception.Data["FirebaseCode"] = firebaseCode;
        exception.Data["FirebaseResponse"] = response.Content;
        throw exception;
    }

    private static string ExtractFirebaseErrorCode(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);

            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("status", out var status)
                && !string.IsNullOrWhiteSpace(status.GetString()))
            {
                return status.GetString()!;
            }

            if (document.RootElement.TryGetProperty("error", out error)
                && error.TryGetProperty("message", out var message)
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!.Split(':', 2)[0].Trim();
            }
        }
        catch (JsonException)
        {
        }

        return "UNKNOWN_ERROR";
    }

    private static T DeserializeResponse<T>(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new JsonException("A resposta estava vazia.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "O Firebase retornou uma resposta em formato inesperado.",
                exception);
        }
    }

    private string BuildCollectionUrl()
    {
        return BuildDocumentsBaseUrl() + $"/{CollectionName}";
    }

    private string BuildRunQueryUrl()
    {
        return BuildDocumentsBaseUrl() + ":runQuery";
    }

    private string BuildCommitUrl()
    {
        return BuildDocumentsBaseUrl() + ":commit";
    }

    private string BuildRideRequestDocumentUrl(string requestId)
    {
        return BuildCollectionUrl() + $"/{Escape(requestId)}";
    }

    private string BuildWeeklyRouteDocumentUrl(string routeId)
    {
        return BuildDocumentsBaseUrl() + $"/weeklyRoutes/{Escape(routeId)}";
    }

    private string BuildRideRequestDocumentName(string requestId)
    {
        return BuildDatabaseName()
               + $"/documents/{CollectionName}/{Escape(requestId)}";
    }

    private string BuildDocumentsBaseUrl()
    {
        return $"{FirestoreBaseUrl}/{BuildDatabaseName()}/documents";
    }

    private string BuildDatabaseName()
    {
        return $"projects/{Escape(_options.ProjectId)}/databases/(default)";
    }

    private void EnsureFirebaseIsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ProjectId))
        {
            throw new InvalidOperationException(
                "Configure FirebaseOptions.ProjectId em MauiProgram.cs.");
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record FirebaseHttpResponse(
        bool IsSuccessStatusCode,
        string Content);

    private sealed class RunQueryResultDto
    {
        [JsonPropertyName("document")]
        public FirestoreDocumentDto? Document { get; init; }
    }

    private sealed class FirestoreDocumentDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("fields")]
        public Dictionary<string, FirestoreValueDto> Fields { get; init; } = [];

        [JsonPropertyName("updateTime")]
        public string? UpdateTime { get; init; }
    }

    private sealed class FirestoreValueDto
    {
        [JsonPropertyName("stringValue")]
        public string? StringValue { get; init; }

        [JsonPropertyName("integerValue")]
        public JsonElement IntegerValue { get; init; }

        [JsonPropertyName("doubleValue")]
        public JsonElement DoubleValue { get; init; }

        [JsonPropertyName("timestampValue")]
        public DateTimeOffset? TimestampValue { get; init; }

        [JsonPropertyName("arrayValue")]
        public FirestoreArrayValueDto? ArrayValue { get; init; }
    }

    private sealed class FirestoreArrayValueDto
    {
        [JsonPropertyName("values")]
        public List<FirestoreValueDto> Values { get; init; } = [];
    }

    private sealed record DriverRouteSnapshot(
        string Id,
        string UserId,
        RouteRole Role,
        int AvailableSeats,
        long RequestRevision,
        string DocumentName,
        string UpdateTime);
}
