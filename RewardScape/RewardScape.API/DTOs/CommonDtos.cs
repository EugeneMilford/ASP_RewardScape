namespace SpendWell.Api.DTOs;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages
    {
        get { return PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize); }
    }
}
