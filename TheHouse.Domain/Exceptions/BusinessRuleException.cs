namespace TheHouse.Domain.Exceptions;

// Solicitud bien formada que incumple una regla de negocio (422), por
// ejemplo un horario de visita fuera de la ventana permitida.
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string rule)
        : base("Business rule violated.")
    {
        Rule = rule;
    }

    public string Rule { get; }
}
