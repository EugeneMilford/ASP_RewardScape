namespace SpendWell.Api.Common;

/// <summary>The thing asked for does not exist. Returns 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public static NotFoundException For(string entity, object key)
    {
        return new NotFoundException(string.Format("{0} '{1}' was not found.", entity, key));
    }
}

/// <summary>A business rule was broken, e.g. insufficient funds. Returns 400.</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

/// <summary>The request clashes with something that already exists, e.g. a duplicate slip. Returns 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
