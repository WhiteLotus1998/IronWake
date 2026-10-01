namespace Ironwake.Core;

/// <summary>A cast member's pronoun (issue 615), from the cast file's optional <c>pronoun</c>.</summary>
public enum Pronoun
{
    He,
    She,
    They,
}

/// <summary>
/// The words a line uses to refer back to one unit it has already named (issue 615): the
/// unit's pronoun when the cast file gives one, else <paramref name="Name"/> again, so no line
/// calls a person "it". <see cref="Plural"/> is true for "they", which takes the plural verb.
/// </summary>
public sealed record Referent(string Subject, string Object, string Possessive, bool Plural, string Name)
{
    /// <summary>
    /// How a line refers to <paramref name="unitId"/> after naming it as <paramref name="name"/>:
    /// its pronoun from <see cref="GameContent.Pronouns"/>, or the name for a unit without one.
    /// </summary>
    public static Referent For(GameContent content, string unitId, string name) =>
        content.Pronouns.TryGetValue(unitId, out var pronoun) ? For(pronoun, name) : new Referent(name, name, name + "'s", false, name);

    /// <summary>
    /// How a line refers to <paramref name="unit"/> after naming it by its id: the pronoun chosen
    /// for it (<see cref="Unit.Pronoun"/>, issue 681), else the cast file's, else its id.
    /// </summary>
    public static Referent For(GameContent content, Unit unit) =>
        unit.Pronoun is { } chosen ? For(chosen, unit.Id) : For(content, unit.Id, unit.Id);

    /// <summary>The words for <paramref name="pronoun"/>.</summary>
    public static Referent For(Pronoun pronoun, string name) => pronoun switch
    {
        Pronoun.He => new Referent("he", "him", "his", false, name),
        Pronoun.She => new Referent("she", "her", "her", false, name),
        _ => new Referent("they", "them", "their", true, name),
    };

    /// <summary><paramref name="singular"/> after a singular subject, <paramref name="plural"/> after "they".</summary>
    public string Verb(string singular, string plural) => Plural ? plural : singular;
}
