namespace Circle.Core.Domain;

/// <summary>
/// Identifies a sector on the circle by key type and index.
/// </summary>
public sealed record KeySelection(KeyType Type, int Index);
