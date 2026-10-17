namespace Argus.Personas;

/// <summary>
/// Hypothetical playstyles, NOT psychometric measurements of real human players.
/// All dimensions are normalized to [0,1] and currently determine shadow proposals only.
/// </summary>
public sealed record PersonaProfile(string Id, float RiskAppetite, float Exploration, float Patience, float Attention,
    float MotorPrecision)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 64 ||
            new[] { RiskAppetite, Exploration, Patience, Attention, MotorPrecision }
                .Any(value => !float.IsFinite(value) || value is < 0 or > 1))
            throw new ArgumentException("Persona ID and every normalized trait must be valid.");
    }
}

public static class PersonaPresets
{
    public static IReadOnlyList<PersonaProfile> Default { get; } =
    [
        new("conservative", RiskAppetite: 0.1f, Exploration: 0.2f, Patience: 0.9f, Attention: 0.9f, MotorPrecision: 0.8f),
        new("adventurer", RiskAppetite: 0.5f, Exploration: 0.9f, Patience: 0.4f, Attention: 0.6f, MotorPrecision: 0.5f),
        new("daredevil", RiskAppetite: 0.95f, Exploration: 0.8f, Patience: 0.15f, Attention: 0.35f, MotorPrecision: 0.65f)
    ];
}

public sealed record MovementBindings(string Up, string Left, string Down, string Right)
{
    public void Validate()
    {
        var keys = new[] { Up, Left, Down, Right };
        if (keys.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 32) ||
            keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
            throw new ArgumentException("Movement directions need four distinct ordinary input keys.");
    }

    public string At(int index) => index switch
    {
        0 => Up,
        1 => Left,
        2 => Down,
        3 => Right,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
