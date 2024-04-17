namespace Domain.Repositories;

public class Repository<TEntity> where TEntity : class {
    public readonly ModelDbContext Context;
    public readonly DbSet<TEntity> Table;

    public Repository(ModelDbContext context) {
        Context = context;
        Table = Context.Set<TEntity>();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> func, CancellationToken ct,
        bool tracking = false, bool ignoreAutoIncludes = false,
        params (Expression<Func<TEntity, object>>, SortDirection)[] sort) {
        var query = Table.AsQueryable();
        if (ignoreAutoIncludes) query = query.IgnoreAutoIncludes();
        if (!tracking) query = query.AsNoTracking();
        return await query
            .OrderByMultiple(sort)
            .FirstOrDefaultAsync(func, ct);
    }

    public async Task<TEntity?> FirstOrDefaultAsync<TKey>(Expression<Func<TEntity, TKey>> keySelector, TKey key,
        CancellationToken ct,
        bool tracking = false, bool ignoreAutoIncludes = false) {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var body = Expression.Equal(Expression.Invoke(keySelector, parameter), Expression.Constant(key, typeof(TKey)));
        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);

        var query = Table.AsQueryable();
        if (ignoreAutoIncludes) query = query.IgnoreAutoIncludes();
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(lambda, ct);
    }

    public async Task<List<TEntity>> ReadAsync(Expression<Func<TEntity, bool>> filter, CancellationToken ct,
        bool tracking = false,
        params (Expression<Func<TEntity, object>>, SortDirection)[] sort) {
        var query = Table.AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return await query
            .OrderByMultiple(sort)
            .Where(filter)
            .ToListAsync(ct);
    }

    private static Expression<Func<TEntity, bool>> CreateLikeExpression<TProperty>(
        Expression<Func<TEntity, TProperty>> propertySelector, string search) {
        // Get the body of the original lambda expression
        var property = propertySelector.Body;

        // Create the EF.Functions.Like expression
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)]);
        var functionsInstance = Expression.Constant(EF.Functions);
        var searchExpression = Expression.Constant($"%{search}%");
        var likeCall = Expression.Call(likeMethod ?? throw new InvalidOperationException(),
            functionsInstance, property, searchExpression);

        // Create a new lambda expression with the Like method call
        var lambda = Expression.Lambda<Func<TEntity, bool>>(likeCall, propertySelector.Parameters);

        return lambda;
    }


    public async Task<TEntity> CreateAsync(TEntity entity, CancellationToken ct) {
        Table.Add(entity);
        await Context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<TEntity>> CreateAsync(List<TEntity> entities, CancellationToken ct) {
        await Table.AddRangeAsync(entities, ct);
        await Context.SaveChangesAsync(ct);
        return entities;
    }


    public async Task UpdateAsync(TEntity entity, CancellationToken ct) {
        Table.Update(entity);
        await Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(IEnumerable<TEntity> entities, CancellationToken ct) {
        Table.UpdateRange(entities);
        await Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TEntity entity, CancellationToken ct) {
        Table.Remove(entity);
        await Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(IEnumerable<TEntity> entities, CancellationToken ct) {
        Table.RemoveRange(entities);
        await Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Expression<Func<TEntity, bool>> filter, CancellationToken ct) {
        await Table.IgnoreAutoIncludes().Where(filter).ExecuteDeleteAsync(ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter, CancellationToken ct) {
        return await Table.IgnoreAutoIncludes().AnyAsync(filter, ct);
    }

    protected IQueryable<TEntity> SearchFor<TProperty>(Expression<Func<TEntity, TProperty>> propertySelector,
        string? search) {
        ArgumentNullException.ThrowIfNull(propertySelector);
        ArgumentNullException.ThrowIfNull(search);

        return Table.AsNoTracking().Where(CreateLikeExpression(propertySelector, search));
    }

    public async Task<int> CountAsync<TProperty>(Expression<Func<TEntity, TProperty>> propertySelector, string? search,
        CancellationToken ct, Expression<Func<TEntity, bool>>? filter = null) {
        var query = Table.AsQueryable();
        if (!search.IsNullEmptyOrWhiteSpace()) query = SearchFor(propertySelector, search);
        if (filter is not null) query = query.Where(filter);
        return await query
            .IgnoreAutoIncludes()
            .AsNoTracking()
            .CountAsync(ct);
    }
}