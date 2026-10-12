using UniRota.Services.Firebase;

namespace UniRota.Tests;

public sealed class FirebaseOptionsTests
{
    [Fact]
    public void Production_UsesFirebaseEndpoints()
    {
        var options = new FirebaseOptions
        {
            ProjectId = "unirota-f0a63",
            FunctionsRegion = "southamerica-east1"
        };

        Assert.Equal("https://identitytoolkit.googleapis.com/v1", options.IdentityToolkitBaseUrl);
        Assert.Equal("https://securetoken.googleapis.com/v1", options.SecureTokenBaseUrl);
        Assert.Equal("https://firestore.googleapis.com/v1", options.FirestoreBaseUrl);
        Assert.Equal(
            "https://southamerica-east1-unirota-f0a63.cloudfunctions.net/computeRoute",
            options.BuildFunctionUrl("computeRoute"));
    }

    [Fact]
    public void Emulator_UsesLocalFirebaseEndpoints()
    {
        var options = new FirebaseOptions
        {
            UseEmulators = true,
            EmulatorHost = "10.0.2.2",
            ProjectId = "unirota-f0a63",
            FunctionsRegion = "southamerica-east1"
        };

        Assert.Equal("http://10.0.2.2:9099/identitytoolkit.googleapis.com/v1", options.IdentityToolkitBaseUrl);
        Assert.Equal("http://10.0.2.2:9099/securetoken.googleapis.com/v1", options.SecureTokenBaseUrl);
        Assert.Equal("http://10.0.2.2:8080/v1", options.FirestoreBaseUrl);
        Assert.Equal(
            "http://10.0.2.2:5001/unirota-f0a63/southamerica-east1/computeRoute",
            options.BuildFunctionUrl("computeRoute"));
    }
}
