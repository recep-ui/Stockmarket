using BistQuant.Application.Common.Exceptions;
using BistQuant.Application.Common.Models;

namespace BistQuant.Application.Tests;

public class ApiResponseTests
{
    [Fact]
    public void ApiResponse_Ok_ShouldReturnSuccessPayload()
    {
        // Arrange & Act
        var response = ApiResponse<string>.Ok("Calculated", "Success message");

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Calculated", response.Data);
        Assert.Equal("Success message", response.Message);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void ApiResponse_Fail_ShouldReturnErrorDetails()
    {
        // Arrange & Act
        var response = ApiResponse<object>.Fail("Symbol not found", new List<string> { "Symbol XYZ is not listed." });

        // Assert
        Assert.False(response.Success);
        Assert.Equal("Symbol not found", response.Message);
        Assert.NotNull(response.Errors);
        Assert.Single(response.Errors);
    }

    [Fact]
    public void PagedResult_PaginationCalculations_ShouldBeAccurate()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3, 4, 5 };
        var paged = new PagedResult<int>(items, count: 25, pageNumber: 2, pageSize: 5);

        // Assert
        Assert.Equal(5, paged.TotalPages);
        Assert.Equal(2, paged.PageNumber);
        Assert.True(paged.HasPreviousPage);
        Assert.True(paged.HasNextPage);
    }

    [Fact]
    public void ValidationException_ShouldStoreFieldErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Symbol", new[] { "Symbol is required." } },
            { "Timeframe", new[] { "Timeframe is invalid." } }
        };

        // Act
        var ex = new ValidationException(errors);

        // Assert
        Assert.Equal("VALIDATION_ERROR", ex.ErrorCode);
        Assert.Equal(2, ex.Errors.Count);
        Assert.Contains("Symbol", ex.Errors.Keys);
    }
}
