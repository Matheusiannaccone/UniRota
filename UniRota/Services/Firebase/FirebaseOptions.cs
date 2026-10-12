namespace UniRota.Services.Firebase;

public sealed class FirebaseOptions
{
    public string ApiKey { get; init; } = string.Empty;

    public string ProjectId { get; init; } = string.Empty;

    public string FunctionsRegion { get; init; } = string.Empty;

    public bool UseEmulators { get; init; }

    public string EmulatorHost { get; init; } = "10.0.2.2";

    public int AuthEmulatorPort { get; init; } = 9099;

    public int FirestoreEmulatorPort { get; init; } = 8080;

    public int FunctionsEmulatorPort { get; init; } = 5001;

    public string IdentityToolkitBaseUrl =>
        UseEmulators
            ? $"http://{EmulatorHost}:{AuthEmulatorPort}/identitytoolkit.googleapis.com/v1"
            : "https://identitytoolkit.googleapis.com/v1";

    public string SecureTokenBaseUrl =>
        UseEmulators
            ? $"http://{EmulatorHost}:{AuthEmulatorPort}/securetoken.googleapis.com/v1"
            : "https://securetoken.googleapis.com/v1";

    public string FirestoreBaseUrl =>
        UseEmulators
            ? $"http://{EmulatorHost}:{FirestoreEmulatorPort}/v1"
            : "https://firestore.googleapis.com/v1";

    public string BuildFunctionUrl(string functionName)
    {
        if (UseEmulators)
        {
            return $"http://{EmulatorHost}:{FunctionsEmulatorPort}/" +
                   $"{ProjectId}/{FunctionsRegion}/{functionName}";
        }

        return $"https://{FunctionsRegion}-{ProjectId}.cloudfunctions.net/" +
               functionName;
    }
}