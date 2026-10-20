using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseRouteServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task CreateAsync_RoundTripsDriverSeatSchema(int reservationCount)
    {
        OneTimeReservation[] reservations =
        [
            SeatReservation("once-A", new DateOnly(2026, 10, 19)),
            SeatReservation("once-B", new DateOnly(2026, 10, 20)),
            SeatReservation("once-C", new DateOnly(2026, 10, 26))
        ];
        var input = CreateSeatSchemaRoute(
            reservations: reservations.Take(reservationCount).ToArray());

        var (output, fields, _) = await RoundTripSeatRouteAsync(input);

        Assert.Equal(3, output.SeatCapacity);
        Assert.Equal(input.DaysOfWeek, output.DaysOfWeek);
        Assert.Equal(3, output.WeeklyAvailableSeats.Count);
        Assert.Equal("3", fields["seatCapacity"]!["integerValue"]!.GetValue<string>());
        var weekly = fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!.AsObject();
        Assert.Equal(3, weekly.Count);

        foreach (var entry in input.WeeklyAvailableSeats)
        {
            Assert.Equal(entry.Value, output.WeeklyAvailableSeats[entry.Key]);
            Assert.Equal(
                entry.Value.ToString(CultureInfo.InvariantCulture),
                weekly[entry.Key.ToString()]!["integerValue"]!.GetValue<string>());
        }

        var values = fields["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray();
        Assert.Equal(reservationCount, values.Count);
        Assert.Equal(input.OneTimeReservations.ToArray(), output.OneTimeReservations.ToArray());

        for (var index = 0; index < reservationCount; index++)
        {
            var expected = input.OneTimeReservations[index];
            var actual = values[index]!["mapValue"]!["fields"]!.AsObject();
            Assert.Equal(3, actual.Count);
            Assert.Equal(expected.RequestId, actual["requestId"]!["stringValue"]!.GetValue<string>());
            Assert.Equal(
                expected.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                actual["date"]!["stringValue"]!.GetValue<string>());
            Assert.Equal(
                expected.DayOfWeek.ToString(),
                actual["dayOfWeek"]!["stringValue"]!.GetValue<string>());
        }

        Assert.False(fields.ContainsKey("availableSeats"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteAsync_RoundTripsPassengerSchemaWithoutDriverFields(bool update)
    {
        var input = CreateSeatSchemaRoute(role: RouteRole.Passenger, capacity: null);

        var (output, fields, _) = await RoundTripSeatRouteAsync(input, update);

        Assert.Equal(RouteRole.Passenger, output.Role);
        Assert.Null(output.SeatCapacity);
        Assert.Empty(output.WeeklyAvailableSeats);
        Assert.Empty(output.OneTimeReservations);
        foreach (var field in new[] { "seatCapacity", "weeklyAvailableSeats", "oneTimeReservations", "estimatedDistanceKm", "requestRevision" })
            Assert.False(fields.ContainsKey(field));
        Assert.False(fields.ContainsKey("availableSeats"));
    }

    [Fact]
    public async Task UpdateAsync_RoundTripsProvidedSeatSchemaWithNewUpdateMask()
    {
        var input = CreateSeatSchemaRoute(
            reservations: [SeatReservation("once-A", new DateOnly(2026, 10, 20))]);

        var (output, fields, url) = await RoundTripSeatRouteAsync(input, update: true);

        Assert.Equal(input.SeatCapacity, output.SeatCapacity);
        Assert.Equal(input.WeeklyAvailableSeats.ToArray(), output.WeeklyAvailableSeats.ToArray());
        Assert.Equal(input.OneTimeReservations.ToArray(), output.OneTimeReservations.ToArray());
        Assert.Contains("updateMask.fieldPaths=seatCapacity", url);
        Assert.Contains("updateMask.fieldPaths=weeklyAvailableSeats", url);
        Assert.Contains("updateMask.fieldPaths=oneTimeReservations", url);
        Assert.Contains("currentDocument.updateTime=", url);
        Assert.DoesNotContain("updateMask.fieldPaths=availableSeats", url);
        Assert.False(fields.ContainsKey("availableSeats"));
    }

    [Theory]
    [InlineData("capacity-null")]
    [InlineData("capacity-zero")]
    [InlineData("capacity-negative")]
    [InlineData("balance-negative")]
    [InlineData("balance-above-capacity")]
    [InlineData("missing-day")]
    [InlineData("extra-day")]
    [InlineData("invalid-day")]
    [InlineData("request-empty")]
    [InlineData("request-whitespace")]
    [InlineData("request-duplicate")]
    [InlineData("reservation-day-mismatch")]
    [InlineData("reservation-outside-route")]
    [InlineData("passenger-capacity")]
    [InlineData("passenger-weekly-seats")]
    [InlineData("passenger-reservation")]
    public async Task CreateAsync_RejectsInvalidSeatSchemaBeforeWriting(string scenario)
    {
        var sent = false;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            sent = true;
            throw new InvalidOperationException("Schema inválido não deve ser enviado.");
        });
        var route = CreateInvalidSeatRoute(scenario);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateService(handler).CreateAsync(route));

        Assert.False(sent);
    }

    [Theory]
    [InlineData("capacity-zero")]
    [InlineData("capacity-negative")]
    [InlineData("capacity-null")]
    [InlineData("capacity-overflow")]
    [InlineData("balance-negative")]
    [InlineData("balance-above-capacity")]
    [InlineData("balance-overflow")]
    [InlineData("balance-wrong-type")]
    [InlineData("missing-day")]
    [InlineData("extra-day")]
    [InlineData("request-empty")]
    [InlineData("request-duplicate")]
    [InlineData("reservation-day-mismatch")]
    [InlineData("reservation-outside-route")]
    [InlineData("date-invalid")]
    [InlineData("date-format")]
    [InlineData("requestId-missing")]
    [InlineData("date-missing")]
    [InlineData("dayOfWeek-missing")]
    [InlineData("weekly-not-map")]
    [InlineData("weekly-null-fields")]
    [InlineData("reservations-not-array")]
    [InlineData("reservations-null-values")]
    [InlineData("reservation-not-map")]
    [InlineData("passenger-capacity")]
    [InlineData("passenger-weekly-seats")]
    [InlineData("passenger-reservation")]
    public async Task GetMyRoutesAsync_RejectsInvalidPersistedSeatSchema(string scenario)
    {
        var document = CreateInvalidSeatDocument(scenario);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));
    }

    [Theory]
    [InlineData(false, "seatCapacity")]
    [InlineData(false, "weeklyAvailableSeats")]
    [InlineData(false, "oneTimeReservations")]
    [InlineData(true, "weeklyAcceptedDays")]
    [InlineData(true, "oneTimeAcceptedDates")]
    [InlineData(false, "estimatedDistanceKm")]
    [InlineData(false, "requestRevision")]
    public async Task GetMyRoutesAsync_RequiresEverySeatField(bool passenger, string missingField)
    {
        var document = CreateSeatDocument(passenger);
        document["fields"]!.AsObject().Remove(missingField);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReadSeatDocumentAsync(document));

        Assert.Contains(missingField, exception.Message);
    }

    [Theory]
    [InlineData("monday")]
    [InlineData("1")]
    [InlineData("Segunda")]
    [InlineData("Mon")]
    [InlineData("Monday ")]
    [InlineData("Unknown")]
    public async Task GetMyRoutesAsync_RejectsNoncanonicalPersistedDays(string day)
    {
        var document = CreateSeatDocument();
        var fields = document["fields"]!.AsObject();
        var weekly = fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!.AsObject();
        var seats = weekly["Monday"]!.DeepClone();
        weekly.Remove("Monday");
        weekly[day] = seats;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));

        fields["weeklyAvailableSeats"] = CreateSeatDocument()["fields"]!["weeklyAvailableSeats"]!.DeepClone();
        fields["oneTimeReservations"]!["arrayValue"]!["values"] = new JsonArray(
            SeatReservationValue("once-A", "2026-10-19", day));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));

        fields["oneTimeReservations"]!["arrayValue"]!["values"] = new JsonArray();
        fields["daysOfWeek"]!["arrayValue"]!["values"]![0]!["stringValue"] = day;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSeatDocumentAsync(document));
    }

    [Fact]
    public async Task GetMyRoutesAsync_AcceptsAllSevenCanonicalDayNames()
    {
        var document = CreateSeatDocument();
        var fields = document["fields"]!.AsObject();
        var days = new JsonArray();
        var weekly = new JsonObject();

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            days.Add(new JsonObject { ["stringValue"] = day.ToString() });
            weekly[day.ToString()] = new JsonObject { ["integerValue"] = "2" };
        }

        fields["daysOfWeek"]!["arrayValue"]!["values"] = days;
        fields["weeklyAvailableSeats"]!["mapValue"]!["fields"] = weekly;

        var route = Assert.Single(await ReadSeatDocumentAsync(document));

        Assert.Equal(Enum.GetValues<DayOfWeek>(), route.DaysOfWeek.ToArray());
        Assert.Equal(7, route.WeeklyAvailableSeats.Count);
    }

    [Fact]
    public async Task GetMyRoutesAsync_AcceptsOmittedContentsOfEmptyFirestoreContainers()
    {
        var document = CreateSeatDocument(passenger: true);
        var fields = document["fields"]!.AsObject();
        fields["weeklyAcceptedDays"] = JsonNode.Parse("{\"arrayValue\":{}}");
        fields["oneTimeAcceptedDates"] = JsonNode.Parse("{\"arrayValue\":{}}");

        var route = Assert.Single(await ReadSeatDocumentAsync(document));

        Assert.Null(route.SeatCapacity);
        Assert.Empty(route.WeeklyAvailableSeats);
        Assert.Empty(route.OneTimeReservations);
    }

    [Fact]
    public async Task GetMyRoutesAsync_RejectsLegacyOnlyDocumentWithoutInferringSeatFields()
    {
        var document = CreateSeatDocument();
        var fields = document["fields"]!.AsObject();
        fields.Remove("seatCapacity");
        fields.Remove("weeklyAvailableSeats");
        fields.Remove("oneTimeReservations");
        fields["availableSeats"] = new JsonObject { ["integerValue"] = "2" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReadSeatDocumentAsync(document));

        Assert.Contains("seatCapacity", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsDriverWithoutSeatSchema()
    {
        var withoutSchema = new WeeklyRoute
        {
            Role = RouteRole.Driver,
            Origin = "Centro",
            Destination = "Facens",
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 450,
            EstimatedDistanceKm = 8.5m
        };
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("Não deve inferir o novo schema."));

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService(handler).CreateAsync(withoutSchema));
    }

    [Fact]
    public async Task CreateAsync_UsesInvariantDateFormatAndRetainsPastReservations()
    {
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var input = CreateSeatSchemaRoute(
                reservations: [SeatReservation("historical", new DateOnly(2020, 1, 6))]);

            var (output, fields, _) = await RoundTripSeatRouteAsync(input);

            Assert.Equal(new DateOnly(2020, 1, 6), Assert.Single(output.OneTimeReservations).Date);
            Assert.Equal(
                "2020-01-06",
                fields["oneTimeReservations"]!["arrayValue"]!["values"]![0]!
                    ["mapValue"]!["fields"]!["date"]!["stringValue"]!.GetValue<string>());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void WeeklyRoute_JsonHasNoLegacySeatProperty()
    {
        var route = CreateSeatSchemaRoute();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(route));

        Assert.False(json.RootElement.TryGetProperty("AvailableSeats", out _));
    }

    private static WeeklyRoute CreateSeatSchemaRoute(
        RouteRole role = RouteRole.Driver,
        int? capacity = 3,
        IReadOnlyDictionary<DayOfWeek, int>? seats = null,
        IReadOnlyList<OneTimeReservation>? reservations = null)
    {
        return new WeeklyRoute
        {
            Id = "schema-route",
            Role = role,
            Origin = "Centro",
            OriginPlaceId = "origin-place-id",
            Destination = "Facens",
            DestinationPlaceId = "destination-place-id",
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
            DepartureTimeMinutes = 450,
            SeatCapacity = capacity,
            WeeklyAvailableSeats = seats ?? (role == RouteRole.Passenger
                ? new Dictionary<DayOfWeek, int>()
                : DefaultWeeklySeatMap()),
            OneTimeReservations = reservations ?? [],
            EstimatedDistanceKm = role == RouteRole.Driver ? 8.5m : 0m
        };
    }

    private static Dictionary<DayOfWeek, int> DefaultWeeklySeatMap() => new()
    {
        [DayOfWeek.Monday] = 2,
        [DayOfWeek.Tuesday] = 1,
        [DayOfWeek.Wednesday] = 0
    };

    private static OneTimeReservation SeatReservation(string requestId, DateOnly date) => new()
    {
        RequestId = requestId,
        Date = date,
        DayOfWeek = date.DayOfWeek
    };

    private static async Task<(WeeklyRoute Route, JsonObject Fields, string Url)> RoundTripSeatRouteAsync(
        WeeklyRoute input,
        bool update = false)
    {
        var storedDocument = CreateSeatDocument(input.Role == RouteRole.Passenger);

        if (update && input.Role == RouteRole.Driver)
        {
            // A edição preserva a ocupação do documento relido, não a enviada pela tela.
            var storedFields = storedDocument["fields"]!.AsObject();
            storedFields["daysOfWeek"]!["arrayValue"]!["values"] = new JsonArray(
                input.DaysOfWeek.Select(day => (JsonNode)new JsonObject { ["stringValue"] = day.ToString() }).ToArray());
            storedFields["seatCapacity"] = input.SeatCapacity is { } capacity
                ? new JsonObject { ["integerValue"] = capacity.ToString(CultureInfo.InvariantCulture) }
                : new JsonObject { ["nullValue"] = null };
            var weekly = new JsonObject();

            foreach (var entry in input.WeeklyAvailableSeats)
            {
                weekly[entry.Key.ToString()] = new JsonObject { ["integerValue"] = entry.Value.ToString(CultureInfo.InvariantCulture) };
            }

            storedFields["weeklyAvailableSeats"]!["mapValue"]!["fields"] = weekly;
            storedFields["oneTimeReservations"]!["arrayValue"]!["values"] = new JsonArray(
                input.OneTimeReservations.Select(reservation => (JsonNode)SeatReservationValue(
                    reservation.RequestId,
                    reservation.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    reservation.DayOfWeek.ToString())).ToArray());
        }

        JsonObject? writtenFields = null;
        string? writtenUrl = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(storedDocument.ToJsonString());
            }

            Assert.Equal(update ? HttpMethod.Patch : HttpMethod.Post, request.Method);
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            writtenFields = body["fields"]!.AsObject();
            writtenUrl = request.RequestUri!.AbsoluteUri;

            foreach (var entry in writtenFields)
            {
                storedDocument["fields"]![entry.Key] = entry.Value?.DeepClone();
            }

            return JsonResponse(storedDocument.ToJsonString());
        });
        var service = CreateService(handler);
        var output = update ? await service.UpdateAsync(input) : await service.CreateAsync(input);
        Assert.NotNull(writtenFields);
        Assert.NotNull(writtenUrl);
        return (output, writtenFields, writtenUrl);
    }

    private static JsonObject CreateSeatDocument(bool passenger = false)
    {
        var document = JsonNode.Parse(CreateRouteDocumentJson("schema-route"))!.AsObject();

        if (passenger)
        {
            var fields = document["fields"]!.AsObject();
            fields["role"]!["stringValue"] = "passenger";
            foreach (var field in new[] { "seatCapacity", "weeklyAvailableSeats", "oneTimeReservations", "estimatedDistanceKm", "requestRevision" })
                fields.Remove(field);
            fields["weeklyAcceptedDays"] = Ledger([]);
            fields["oneTimeAcceptedDates"] = Ledger([]);
        }

        return document;
    }

    private static Task<IReadOnlyList<WeeklyRoute>> ReadSeatDocumentAsync(JsonObject document)
    {
        var queryResponse = new JsonArray(new JsonObject { ["document"] = document.DeepClone() });
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(queryResponse.ToJsonString())));
        return CreateService(handler).GetMyRoutesAsync();
    }

    private static JsonObject SeatReservationValue(string requestId, string date, string day) => new()
    {
        ["mapValue"] = new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["requestId"] = new JsonObject { ["stringValue"] = requestId },
                ["date"] = new JsonObject { ["stringValue"] = date },
                ["dayOfWeek"] = new JsonObject { ["stringValue"] = day }
            }
        }
    };

    private static WeeklyRoute CreateInvalidSeatRoute(string scenario)
    {
        int? capacity = 3;
        var seats = DefaultWeeklySeatMap();
        var reservation = SeatReservation("once-A", new DateOnly(2026, 10, 19));
        OneTimeReservation[] reservations = [];
        var role = RouteRole.Driver;

        switch (scenario)
        {
            case "capacity-null": capacity = null; break;
            case "capacity-zero": capacity = 0; break;
            case "capacity-negative": capacity = -1; break;
            case "balance-negative": seats[DayOfWeek.Monday] = -1; break;
            case "balance-above-capacity": seats[DayOfWeek.Monday] = 4; break;
            case "missing-day": seats.Remove(DayOfWeek.Monday); break;
            case "extra-day": seats[DayOfWeek.Sunday] = 1; break;
            case "invalid-day": seats[(DayOfWeek)99] = 1; break;
            case "request-empty": reservations = [reservation with { RequestId = "" }]; break;
            case "request-whitespace": reservations = [reservation with { RequestId = "  " }]; break;
            case "request-duplicate":
                reservations = [reservation, SeatReservation("once-A", new DateOnly(2026, 10, 26))];
                break;
            case "reservation-day-mismatch":
                reservations = [reservation with { DayOfWeek = DayOfWeek.Tuesday }];
                break;
            case "reservation-outside-route":
                reservations = [SeatReservation("once-A", new DateOnly(2026, 10, 18))];
                break;
            case "passenger-capacity": role = RouteRole.Passenger; seats.Clear(); break;
            case "passenger-weekly-seats": role = RouteRole.Passenger; capacity = null; break;
            case "passenger-reservation":
                role = RouteRole.Passenger;
                capacity = null;
                seats.Clear();
                reservations = [reservation];
                break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        return CreateSeatSchemaRoute(role, capacity, seats, reservations);
    }

    private static JsonObject CreateInvalidSeatDocument(string scenario)
    {
        var document = CreateSeatDocument(scenario.StartsWith("passenger-", StringComparison.Ordinal));
        var fields = document["fields"]!.AsObject();
        if (scenario.StartsWith("passenger-", StringComparison.Ordinal))
        {
            fields[scenario == "passenger-capacity" ? "seatCapacity"
                : scenario == "passenger-weekly-seats" ? "weeklyAvailableSeats" : "oneTimeReservations"] =
                scenario == "passenger-capacity" ? new JsonObject { ["nullValue"] = null }
                : scenario == "passenger-weekly-seats" ? new JsonObject { ["mapValue"] = new JsonObject() }
                : new JsonObject { ["arrayValue"] = new JsonObject() };
            return document;
        }
        var weekly = fields["weeklyAvailableSeats"]!["mapValue"]!["fields"]!.AsObject();
        var reservation = SeatReservationValue("once-A", "2026-10-19", "Monday");
        var reservationFields = reservation["mapValue"]!["fields"]!.AsObject();
        var reservations = fields["oneTimeReservations"]!["arrayValue"]!["values"]!.AsArray();

        switch (scenario)
        {
            case "capacity-zero": fields["seatCapacity"]!["integerValue"] = "0"; break;
            case "capacity-negative": fields["seatCapacity"]!["integerValue"] = "-1"; break;
            case "capacity-null": fields["seatCapacity"] = new JsonObject { ["nullValue"] = null }; break;
            case "capacity-overflow": fields["seatCapacity"]!["integerValue"] = "2147483648"; break;
            case "balance-negative": weekly["Monday"]!["integerValue"] = "-1"; break;
            case "balance-above-capacity": weekly["Monday"]!["integerValue"] = "3"; break;
            case "balance-overflow": weekly["Monday"]!["integerValue"] = "2147483648"; break;
            case "balance-wrong-type": weekly["Monday"] = new JsonObject { ["stringValue"] = "2" }; break;
            case "missing-day": weekly.Remove("Monday"); break;
            case "extra-day": weekly["Tuesday"] = new JsonObject { ["integerValue"] = "2" }; break;
            case "request-empty":
                reservationFields["requestId"]!["stringValue"] = " ";
                reservations.Add(reservation);
                break;
            case "request-duplicate":
                reservations.Add(reservation);
                reservations.Add(SeatReservationValue("once-A", "2026-10-26", "Monday"));
                break;
            case "reservation-day-mismatch":
                reservationFields["dayOfWeek"]!["stringValue"] = "Tuesday";
                reservations.Add(reservation);
                break;
            case "reservation-outside-route":
                reservations.Add(SeatReservationValue("once-A", "2026-10-20", "Tuesday"));
                break;
            case "date-invalid":
                reservationFields["date"]!["stringValue"] = "2026-02-30";
                reservations.Add(reservation);
                break;
            case "date-format":
                reservationFields["date"]!["stringValue"] = "19/10/2026";
                reservations.Add(reservation);
                break;
            case "requestId-missing":
            case "date-missing":
            case "dayOfWeek-missing":
                reservationFields.Remove(scenario[..scenario.IndexOf('-')]);
                reservations.Add(reservation);
                break;
            case "weekly-not-map": fields["weeklyAvailableSeats"] = new JsonObject { ["arrayValue"] = new JsonObject() }; break;
            case "weekly-null-fields": fields["weeklyAvailableSeats"]!["mapValue"]!["fields"] = null; break;
            case "reservations-not-array": fields["oneTimeReservations"] = new JsonObject { ["mapValue"] = new JsonObject() }; break;
            case "reservations-null-values": fields["oneTimeReservations"]!["arrayValue"]!["values"] = null; break;
            case "reservation-not-map": reservations.Add(new JsonObject { ["stringValue"] = "invalid" }); break;
            case "passenger-capacity": fields["seatCapacity"] = new JsonObject { ["integerValue"] = "0" }; break;
            case "passenger-weekly-seats": weekly["Monday"] = new JsonObject { ["integerValue"] = "1" }; break;
            case "passenger-reservation": reservations.Add(reservation); break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        return document;
    }
}
