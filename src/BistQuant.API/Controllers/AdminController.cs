using BistQuant.Application.Common.Models;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Providers.MarketData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly ICsvMarketDataService _csvService;

    public AdminController(ICsvMarketDataService csvService)
    {
        _csvService = csvService;
    }

    [HttpPost("import/market-data")]
    public async Task<ActionResult<ApiResponse<CsvImportResult>>> ImportMarketData(
        IFormFile file,
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<CsvImportResult>.Fail("File is empty or not provided."));
        }

        using var stream = file.OpenReadStream();
        var result = await _csvService.ImportCsvAsync(stream, timeframe, cancellationToken);
        return Ok(ApiResponse<CsvImportResult>.Ok(result, $"Processed {result.TotalProcessed} rows."));
    }
}
