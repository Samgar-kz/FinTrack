using FinTrack.Application.Abstractions;
using FinTrack.Application.Common;
using FinTrack.Domain.Categories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Categories;

public sealed record CategoryDto(Guid Id, string Name, CategoryKind Kind, string Color);

public sealed record CreateCategoryRequest(string Name, CategoryKind Kind, string Color);

public sealed record UpdateCategoryRequest(string Name, string Color);

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Color).Must(Category.IsValidColor).WithMessage("Colour must be a hex value such as #22C55E.");
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(x => x.Color).Must(Category.IsValidColor).WithMessage("Colour must be a hex value such as #22C55E.");
    }
}

public sealed class CategoryService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator)
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CategoryKind? kind, CancellationToken ct)
    {
        var query = db.Categories.AsNoTracking().Where(c => c.UserId == currentUser.Id);
        if (kind is not null)
        {
            query = query.Where(c => c.Kind == kind);
        }

        return await query
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Kind, c.Color))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await EnsureNameIsFreeAsync(request.Name, request.Kind, excludeId: null, ct);

        var category = new Category(currentUser.Id, request.Name, request.Kind, request.Color);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var category = await FindAsync(id, ct);
        await EnsureNameIsFreeAsync(request.Name, category.Kind, category.Id, ct);

        category.Update(request.Name, request.Color);
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    /// <summary>
    /// Deletes a category. Transactions keep their history but become uncategorised;
    /// budgets for the category are removed (configured as database cascades).
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var category = await FindAsync(id, ct);
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private static CategoryDto ToDto(Category c) => new(c.Id, c.Name, c.Kind, c.Color);

    private async Task<Category> FindAsync(Guid id, CancellationToken ct) =>
        await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.Id, ct)
        ?? throw new NotFoundException("Category", id);

    private async Task EnsureNameIsFreeAsync(string name, CategoryKind kind, Guid? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        var taken = await db.Categories.AnyAsync(
            c => c.UserId == currentUser.Id && c.Kind == kind && c.Id != excludeId && c.Name.ToLower() == normalized,
            ct);

        if (taken)
        {
            throw new ConflictException($"A {kind.ToString().ToLowerInvariant()} category named '{name.Trim()}' already exists.");
        }
    }
}
