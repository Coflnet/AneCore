using Cassandra;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Coflnet.Ane.TrainingListings;

/// <summary>Backoff of <see cref="TrainingStoreRetry"/>: the waits between attempts (attempts = waits + 1), each stretched by up to <see cref="Jitter"/>.</summary>
public sealed record TrainingRetryPolicy(IReadOnlyList<TimeSpan> Delays, double Jitter = 0.5, Func<TimeSpan, CancellationToken, Task>? Delay = null)
{
    /// <summary>0.25 s, 0.5 s, 1 s, 2 s, 4 s plus jitter: a slow node costs seconds, an outage about 8 s before the batch fails.</summary>
    public static readonly TrainingRetryPolicy Default = new(
    [
        TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)
    ]);

    /// <summary>The wait before retry number <paramref name="retry"/> (0 based): the base delay plus up to <see cref="Jitter"/> of it.</summary>
    public TimeSpan WaitFor(int retry, Random? random = null) =>
        Delays[retry] * (1 + Jitter * (random ?? Random.Shared).NextDouble());
}

/// <summary>Bounded retry with backoff and jitter for the transient Cassandra errors of a write: timeouts, unavailable replicas, no host.</summary>
public static class TrainingStoreRetry
{
    public static readonly Counter Retries = Metrics.CreateCounter("ane_training_listing_store_retries_total",
        "Training listing store statements retried after a transient Cassandra error", new CounterConfiguration { LabelNames = new[] { "operation" } });
    public static readonly Counter Exhausted = Metrics.CreateCounter("ane_training_listing_store_retries_exhausted_total",
        "Training listing store statements that still failed after all retries", new CounterConfiguration { LabelNames = new[] { "operation" } });

    public static bool IsTransient(Exception ex) => ex is WriteTimeoutException or ReadTimeoutException or UnavailableException
        or OperationTimedOutException or NoHostAvailableException or WriteFailureException or ReadFailureException or OverloadedException;

    /// <summary>
    /// Runs <paramref name="action"/> and repeats it after a transient error (or after <paramref name="retryable"/> says so). The last error is
    /// rethrown unchanged when the policy is used up. Only the exception type is logged, never a listing.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, string operation, TrainingRetryPolicy policy, ILogger? logger,
        CancellationToken cancellationToken = default, Func<Exception, bool>? retryable = null, Random? random = null)
    {
        retryable ??= IsTransient;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (retryable(ex) && !cancellationToken.IsCancellationRequested)
            {
                if (attempt >= policy.Delays.Count)
                {
                    Exhausted.WithLabels(operation).Inc();
                    logger?.LogWarning("Training listing store {Operation} failed after {Attempts} attempts ({Error})", operation, attempt + 1, ex.GetType().Name);
                    throw;
                }
                Retries.WithLabels(operation).Inc();
                var wait = policy.WaitFor(attempt, random);
                logger?.LogInformation("Training listing store {Operation} retry {Attempt}/{Max} in {Wait} ms ({Error})",
                    operation, attempt + 1, policy.Delays.Count, (int)wait.TotalMilliseconds, ex.GetType().Name);
                await (policy.Delay ?? Task.Delay)(wait, cancellationToken);
            }
        }
    }
}
