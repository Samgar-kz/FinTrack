using FinTrack.Application.Categories;
using FinTrack.Domain.Categories;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> List([FromQuery] CategoryKind? kind, CancellationToken ct) =>
        categories.ListAsync(kind, ct);

    [HttpPost]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await categories.CreateAsync(request, ct);
        return Created($"/api/categories/{category.Id}", category);
    }

    [HttpPut("{id:guid}")]
    public Task<CategoryDto> Update(Guid id, UpdateCategoryRequest request, CancellationToken ct) =>
        categories.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
