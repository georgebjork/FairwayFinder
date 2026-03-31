using FairwayFinder.Features.Enums;

namespace FairwayFinder.Features.Helpers;

/// <summary>
/// Static lookup table for expected strokes to hole out from a given distance and lie.
/// Data based on Mark Broadie's research (Every Shot Counts, 2014).
/// </summary>
public static class StrokesGainedBaseline
{
    /// <summary>
    /// Returns expected strokes to hole out from a given distance and lie.
    /// Sand and Bunker lie types use the same baseline column.
    /// </summary>
    public static double GetExpectedStrokes(int distance, string unit, LieType lie, BaselineLevel level = BaselineLevel.Scratch)
    {
        if (distance <= 0) return 0.0;

        var table = GetTable(level);

        if (lie == LieType.Green || unit == DistanceUnit.Feet)
        {
            return LookupWithInterpolation(distance, table.Green);
        }

        var column = lie switch
        {
            LieType.Tee => table.Tee,
            LieType.Fairway => table.Fairway,
            LieType.Rough => table.Rough,
            LieType.Sand => table.Sand,
            LieType.Recovery => table.Recovery,
            _ => table.Fairway
        };

        return LookupWithInterpolation(distance, column);
    }

    /// <summary>
    /// Returns 0.0 — used when the ball is holed (EndDistance is null).
    /// </summary>
    public static double GetExpectedStrokesHoled() => 0.0;

    private static double LookupWithInterpolation(int distance, SortedList<int, double> data)
    {
        if (data.Count == 0) return 0.0;

        // Exact match
        if (data.TryGetValue(distance, out var exact))
            return exact;

        // Below minimum
        if (distance <= data.Keys[0])
            return data.Values[0];

        // Above maximum
        if (distance >= data.Keys[^1])
            return data.Values[^1];

        // Find surrounding points for interpolation
        int lowerKey = 0, upperKey = 0;
        double lowerVal = 0, upperVal = 0;

        for (int i = 0; i < data.Keys.Count - 1; i++)
        {
            if (data.Keys[i] <= distance && data.Keys[i + 1] >= distance)
            {
                lowerKey = data.Keys[i];
                upperKey = data.Keys[i + 1];
                lowerVal = data.Values[i];
                upperVal = data.Values[i + 1];
                break;
            }
        }

        return Interpolate(distance, lowerKey, lowerVal, upperKey, upperVal);
    }

    /// <summary>
    /// Linear interpolation between two data points.
    /// </summary>
    private static double Interpolate(double x, double x0, double y0, double x1, double y1)
    {
        if (Math.Abs(x1 - x0) < 0.001) return y0;
        return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
    }

    private static BaselineTable GetTable(BaselineLevel level)
    {
        return level switch
        {
            BaselineLevel.Scratch => ScratchBaseline,
            BaselineLevel.Bogey => BogeyBaseline,
            BaselineLevel.HighHandicap => HighHandicapBaseline,
            _ => ScratchBaseline
        };
    }

    private class BaselineTable
    {
        public SortedList<int, double> Tee { get; init; } = new();
        public SortedList<int, double> Fairway { get; init; } = new();
        public SortedList<int, double> Rough { get; init; } = new();
        public SortedList<int, double> Sand { get; init; } = new();
        public SortedList<int, double> Recovery { get; init; } = new();
        /// <summary>
        /// Green distances are in feet.
        /// </summary>
        public SortedList<int, double> Green { get; init; } = new();
    }

    // ================================================================
    // Scratch Baseline (0 handicap) — Primary reference data
    // Distance in yards for non-green, feet for green
    // ================================================================
    private static readonly BaselineTable ScratchBaseline = new()
    {
        Tee = new SortedList<int, double>
        {
            { 100, 2.82 }, { 125, 2.97 }, { 150, 3.09 }, { 175, 3.22 },
            { 200, 3.35 }, { 225, 3.45 }, { 250, 3.54 }, { 275, 3.63 },
            { 300, 3.71 }, { 325, 3.79 }, { 350, 3.86 }, { 375, 3.93 },
            { 400, 3.99 }, { 425, 4.09 }, { 450, 4.18 }, { 475, 4.29 },
            { 500, 4.40 }, { 525, 4.53 }, { 550, 4.63 }, { 575, 4.68 },
            { 600, 4.73 }
        },
        Fairway = new SortedList<int, double>
        {
            { 10, 2.18 }, { 20, 2.33 }, { 30, 2.40 }, { 40, 2.47 },
            { 50, 2.54 }, { 60, 2.58 }, { 75, 2.63 }, { 100, 2.75 },
            { 125, 2.87 }, { 150, 2.99 }, { 175, 3.08 }, { 200, 3.18 },
            { 225, 3.31 }, { 250, 3.45 }, { 275, 3.55 }, { 300, 3.64 },
            { 350, 3.84 }, { 400, 4.08 }, { 450, 4.35 }, { 500, 4.57 }
        },
        Rough = new SortedList<int, double>
        {
            { 10, 2.28 }, { 20, 2.42 }, { 30, 2.49 }, { 40, 2.56 },
            { 50, 2.63 }, { 60, 2.68 }, { 75, 2.74 }, { 100, 2.86 },
            { 125, 2.99 }, { 150, 3.12 }, { 175, 3.23 }, { 200, 3.35 },
            { 225, 3.49 }, { 250, 3.62 }, { 275, 3.73 }, { 300, 3.84 },
            { 350, 4.08 }, { 400, 4.34 }, { 450, 4.61 }, { 500, 4.73 }
        },
        Sand = new SortedList<int, double>
        {
            { 10, 2.38 }, { 20, 2.47 }, { 30, 2.53 }, { 40, 2.61 },
            { 50, 2.68 }, { 60, 2.74 }, { 75, 2.82 }, { 100, 2.94 },
            { 125, 3.06 }, { 150, 3.15 }, { 175, 3.26 }, { 200, 3.36 },
            { 250, 3.57 }, { 300, 3.80 }
        },
        Recovery = new SortedList<int, double>
        {
            { 10, 2.50 }, { 20, 2.63 }, { 30, 2.70 }, { 40, 2.78 },
            { 50, 2.85 }, { 60, 2.92 }, { 75, 3.00 }, { 100, 3.15 },
            { 125, 3.30 }, { 150, 3.45 }, { 175, 3.58 }, { 200, 3.70 },
            { 225, 3.83 }, { 250, 3.95 }, { 275, 4.08 }, { 300, 4.20 }
        },
        Green = new SortedList<int, double>
        {
            { 1, 1.00 }, { 2, 1.01 }, { 3, 1.04 }, { 4, 1.08 },
            { 5, 1.15 }, { 6, 1.22 }, { 7, 1.32 }, { 8, 1.50 },
            { 9, 1.54 }, { 10, 1.61 }, { 12, 1.70 }, { 15, 1.78 },
            { 18, 1.84 }, { 20, 1.87 }, { 25, 1.93 }, { 30, 1.96 },
            { 35, 2.00 }, { 40, 2.04 }, { 45, 2.07 }, { 50, 2.09 },
            { 60, 2.11 }, { 70, 2.17 }, { 80, 2.23 }, { 90, 2.29 }
        }
    };

    // ================================================================
    // Bogey Baseline (~18 handicap)
    // Higher expected strokes across the board
    // ================================================================
    private static readonly BaselineTable BogeyBaseline = new()
    {
        Tee = new SortedList<int, double>
        {
            { 100, 3.45 }, { 125, 3.60 }, { 150, 3.75 }, { 175, 3.90 },
            { 200, 4.05 }, { 225, 4.18 }, { 250, 4.30 }, { 275, 4.42 },
            { 300, 4.52 }, { 325, 4.62 }, { 350, 4.71 }, { 375, 4.80 },
            { 400, 4.89 }, { 425, 5.00 }, { 450, 5.12 }, { 475, 5.25 },
            { 500, 5.40 }, { 525, 5.53 }, { 550, 5.63 }, { 575, 5.70 },
            { 600, 5.77 }
        },
        Fairway = new SortedList<int, double>
        {
            { 10, 2.70 }, { 20, 2.90 }, { 30, 3.00 }, { 40, 3.10 },
            { 50, 3.20 }, { 60, 3.28 }, { 75, 3.35 }, { 100, 3.45 },
            { 125, 3.60 }, { 150, 3.75 }, { 175, 3.90 }, { 200, 4.05 },
            { 225, 4.18 }, { 250, 4.30 }, { 275, 4.42 }, { 300, 4.52 },
            { 350, 4.71 }, { 400, 4.93 }, { 450, 5.18 }, { 500, 5.45 }
        },
        Rough = new SortedList<int, double>
        {
            { 10, 2.85 }, { 20, 3.05 }, { 30, 3.15 }, { 40, 3.25 },
            { 50, 3.35 }, { 60, 3.43 }, { 75, 3.50 }, { 100, 3.60 },
            { 125, 3.75 }, { 150, 3.90 }, { 175, 4.08 }, { 200, 4.25 },
            { 225, 4.40 }, { 250, 4.55 }, { 275, 4.68 }, { 300, 4.80 },
            { 350, 5.05 }, { 400, 5.30 }, { 450, 5.55 }, { 500, 5.75 }
        },
        Sand = new SortedList<int, double>
        {
            { 10, 2.95 }, { 20, 3.10 }, { 30, 3.20 }, { 40, 3.30 },
            { 50, 3.40 }, { 60, 3.48 }, { 75, 3.58 }, { 100, 3.70 },
            { 125, 3.85 }, { 150, 3.98 }, { 175, 4.15 }, { 200, 4.30 },
            { 250, 4.55 }, { 300, 4.80 }
        },
        Recovery = new SortedList<int, double>
        {
            { 10, 3.10 }, { 20, 3.30 }, { 30, 3.40 }, { 40, 3.50 },
            { 50, 3.60 }, { 60, 3.68 }, { 75, 3.78 }, { 100, 3.95 },
            { 125, 4.12 }, { 150, 4.30 }, { 175, 4.48 }, { 200, 4.65 },
            { 225, 4.80 }, { 250, 4.95 }, { 275, 5.10 }, { 300, 5.25 }
        },
        Green = new SortedList<int, double>
        {
            { 1, 1.00 }, { 2, 1.05 }, { 3, 1.14 }, { 4, 1.24 },
            { 5, 1.36 }, { 6, 1.48 }, { 7, 1.60 }, { 8, 1.74 },
            { 9, 1.80 }, { 10, 1.87 }, { 12, 1.97 }, { 15, 2.10 },
            { 18, 2.20 }, { 20, 2.25 }, { 25, 2.35 }, { 30, 2.45 },
            { 35, 2.52 }, { 40, 2.58 }, { 45, 2.63 }, { 50, 2.67 },
            { 60, 2.73 }, { 70, 2.80 }, { 80, 2.87 }, { 90, 2.93 }
        }
    };

    // ================================================================
    // High Handicap Baseline (~30 handicap)
    // ================================================================
    private static readonly BaselineTable HighHandicapBaseline = new()
    {
        Tee = new SortedList<int, double>
        {
            { 100, 4.10 }, { 125, 4.25 }, { 150, 4.42 }, { 175, 4.58 },
            { 200, 4.75 }, { 225, 4.90 }, { 250, 5.05 }, { 275, 5.18 },
            { 300, 5.30 }, { 325, 5.42 }, { 350, 5.53 }, { 375, 5.64 },
            { 400, 5.75 }, { 425, 5.88 }, { 450, 6.02 }, { 475, 6.18 },
            { 500, 6.35 }, { 525, 6.50 }, { 550, 6.62 }, { 575, 6.71 },
            { 600, 6.80 }
        },
        Fairway = new SortedList<int, double>
        {
            { 10, 3.20 }, { 20, 3.45 }, { 30, 3.55 }, { 40, 3.67 },
            { 50, 3.80 }, { 60, 3.90 }, { 75, 4.00 }, { 100, 4.10 },
            { 125, 4.25 }, { 150, 4.42 }, { 175, 4.60 }, { 200, 4.78 },
            { 225, 4.95 }, { 250, 5.10 }, { 275, 5.25 }, { 300, 5.38 },
            { 350, 5.60 }, { 400, 5.85 }, { 450, 6.10 }, { 500, 6.35 }
        },
        Rough = new SortedList<int, double>
        {
            { 10, 3.38 }, { 20, 3.60 }, { 30, 3.72 }, { 40, 3.85 },
            { 50, 3.98 }, { 60, 4.08 }, { 75, 4.18 }, { 100, 4.30 },
            { 125, 4.48 }, { 150, 4.65 }, { 175, 4.85 }, { 200, 5.05 },
            { 225, 5.22 }, { 250, 5.40 }, { 275, 5.55 }, { 300, 5.70 },
            { 350, 5.98 }, { 400, 6.25 }, { 450, 6.52 }, { 500, 6.78 }
        },
        Sand = new SortedList<int, double>
        {
            { 10, 3.48 }, { 20, 3.68 }, { 30, 3.80 }, { 40, 3.93 },
            { 50, 4.05 }, { 60, 4.15 }, { 75, 4.28 }, { 100, 4.42 },
            { 125, 4.58 }, { 150, 4.75 }, { 175, 4.95 }, { 200, 5.15 },
            { 250, 5.45 }, { 300, 5.75 }
        },
        Recovery = new SortedList<int, double>
        {
            { 10, 3.65 }, { 20, 3.90 }, { 30, 4.02 }, { 40, 4.15 },
            { 50, 4.28 }, { 60, 4.38 }, { 75, 4.50 }, { 100, 4.70 },
            { 125, 4.90 }, { 150, 5.10 }, { 175, 5.30 }, { 200, 5.50 },
            { 225, 5.68 }, { 250, 5.85 }, { 275, 6.02 }, { 300, 6.20 }
        },
        Green = new SortedList<int, double>
        {
            { 1, 1.00 }, { 2, 1.10 }, { 3, 1.26 }, { 4, 1.40 },
            { 5, 1.56 }, { 6, 1.72 }, { 7, 1.86 }, { 8, 2.00 },
            { 9, 2.08 }, { 10, 2.14 }, { 12, 2.28 }, { 15, 2.42 },
            { 18, 2.54 }, { 20, 2.60 }, { 25, 2.73 }, { 30, 2.85 },
            { 35, 2.94 }, { 40, 3.02 }, { 45, 3.08 }, { 50, 3.14 },
            { 60, 3.23 }, { 70, 3.32 }, { 80, 3.40 }, { 90, 3.48 }
        }
    };
}
