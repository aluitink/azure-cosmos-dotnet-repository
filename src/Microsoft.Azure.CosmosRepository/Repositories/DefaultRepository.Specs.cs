// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.


// ReSharper disable once CheckNamespace
namespace Microsoft.Azure.CosmosRepository;

internal sealed partial class DefaultRepository<TItem>
{
    /// <inheritdoc/>
    public async ValueTask<TResult> QueryAsync<TResult>(
        ISpecification<TItem, TResult> specification,
        bool returnTotal = false,
        CancellationToken cancellationToken = default)
        where TResult : IQueryResult<TItem>
    {
        Container container = await containerProvider.GetContainerAsync()
            .ConfigureAwait(false);

        QueryRequestOptions options = new();

        if (specification.UseContinuationToken)
        {
            options.MaxItemCount = specification.PageSize;
        }

        IQueryable<TItem> query = container
            .GetItemLinqQueryable<TItem>(
                requestOptions: options,
                continuationToken: specification.ContinuationToken, 
                linqSerializerOptions: optionsMonitor.CurrentValue.SerializationOptions)
            .Where(repositoryExpressionProvider.Default<TItem>());

        query = specificationEvaluator.GetQuery(query, specification);

        logger.LogQueryConstructed(query);

        (List<TItem> items, var charge, var continuationToken) =
            await GetAllItemsAsync(query, specification.PageSize, cancellationToken)
                .ConfigureAwait(false);

        logger.LogQueryExecuted(query, charge);

        Response<int>? countResponse = null;
        if (returnTotal)
        {
            countResponse = await CountAsync(specification, cancellationToken)
                .ConfigureAwait(false);
        }

        return specification.PostProcessingAction(
                items.AsReadOnly(), countResponse?.Resource ?? null,
                charge + countResponse?.RequestCharge ?? 0,
                continuationToken);
    }
}
