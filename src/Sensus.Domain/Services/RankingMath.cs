namespace Sensus.Domain.Services;

/// <summary>
/// Utilidades matemáticas deterministas compartidas entre funciones de ranking de dominio (Specs 041 y 042).
/// </summary>
internal static class RankingMath
{
    public static Dictionary<Guid, double> ComputePopularityPercentiles<T>(
        IReadOnlyList<T> candidates,
        Func<T, Guid> idSelector,
        Func<T, int> engagementSelector)
    {
        var result = new Dictionary<Guid, double>(candidates.Count);
        if (candidates.Count == 0)
        {
            return result;
        }

        if (candidates.Count == 1)
        {
            result[idSelector(candidates[0])] = engagementSelector(candidates[0]) > 0 ? 1d : 0d;
            return result;
        }

        int[] sorted = candidates.Select(c => Math.Max(0, engagementSelector(c))).OrderBy(x => x).ToArray();
        if (sorted[^1] == 0)
        {
            foreach (T candidate in candidates)
            {
                result[idSelector(candidate)] = 0d;
            }

            return result;
        }

        if (sorted[0] == sorted[^1])
        {
            foreach (T candidate in candidates)
            {
                result[idSelector(candidate)] = 0.5d;
            }

            return result;
        }

        double denom = candidates.Count - 1d;
        foreach (T candidate in candidates)
        {
            int val = Math.Max(0, engagementSelector(candidate));
            int belowCount = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                if (sorted[i] < val)
                {
                    belowCount++;
                }
                else
                {
                    break;
                }
            }

            result[idSelector(candidate)] = Math.Clamp(belowCount / denom, 0d, 1d);
        }

        return result;
    }
}
