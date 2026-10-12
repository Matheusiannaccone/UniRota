namespace UniRota.Models;

// Estado temporário do fluxo, separado do documento persistido de usuário. Nunca contém senha.
public sealed record PendingEmailChange(
    string UserId,
    string OriginalEmail,
    string NewEmail,
    int ResendCount,
    DateTimeOffset ActionsAvailableAtUtc);

public enum EmailConfirmationAction { Automatic, Manual, Cancel }
public enum EmailConfirmationResult { Waiting, Confirmed, Cancelled }
