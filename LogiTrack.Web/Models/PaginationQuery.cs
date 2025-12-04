using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Web.Models;

public class PaginationQuery
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
    public int Page { get; init; } = DefaultPage;

    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; init; } = DefaultPageSize;
}
