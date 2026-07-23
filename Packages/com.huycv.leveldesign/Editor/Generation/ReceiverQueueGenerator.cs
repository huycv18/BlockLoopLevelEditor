using System.Collections.Generic;

namespace Huycv.LevelDesign
{
    /// <summary>
    /// Pure-logic generator for receiver queues (passengersQueuesData).
    /// Collects all cube colors (grid + garage), replicates by clearRatio,
    /// shuffles, then distributes evenly across the requested queue count.
    /// </summary>
    internal static class ReceiverQueueGenerator
    {
        // Reusable buffer — cleared each call, avoids per-generate allocation.
        static readonly List<int> s_receiverBuffer = new List<int>();

        /// <summary>
        /// Validates inputs and generates receiver queues.
        /// Returns null and sets <paramref name="errorMessage"/> if validation fails.
        /// </summary>
        public static ReceiverQueueResult[] Generate(
            LevelEditorContext ctx,
            int queueCount,
            int clearRatio,
            out string errorMessage)
        {
            errorMessage = null;

            if (queueCount <= 0)
            {
                errorMessage = "Queues Amount must be greater than 0.";
                return null;
            }

            if (clearRatio <= 0)
            {
                errorMessage = "Clear Ratio must be greater than 0.";
                return null;
            }

            // Collect all cube colorIds (grid cubes + garage cars)
            s_receiverBuffer.Clear();
            CollectCubeColors(ctx, s_receiverBuffer);
            int cubeCount = s_receiverBuffer.Count;

            if (cubeCount == 0)
            {
                errorMessage = "Grid must have at least 1 cube.";
                return null;
            }

            int totalReceivers = clearRatio * cubeCount;
            if (queueCount > totalReceivers)
            {
                errorMessage = string.Concat(
                    "Not enough receivers for ", queueCount.ToString(),
                    " queues. Maximum queues: ", totalReceivers.ToString(), ".");
                return null;
            }

            // Replicate each cube color by clearRatio
            // s_receiverBuffer currently holds cubeCount colors (one per cube).
            // Expand in-place: for each original color, add (clearRatio - 1) copies.
            int originalCount = s_receiverBuffer.Count;
            for (int i = 0; i < originalCount; i++)
            {
                int colorId = s_receiverBuffer[i];
                for (int r = 1; r < clearRatio; r++)
                    s_receiverBuffer.Add(colorId);
            }

            // Fisher-Yates shuffle
            var rng = new System.Random();
            Shuffle(s_receiverBuffer, rng);

            // Distribute evenly: base + remainder
            return DistributeIntoQueues(s_receiverBuffer, queueCount);
        }

        static void CollectCubeColors(LevelEditorContext ctx, List<int> output)
        {
            int total = ctx.GridWidth * ctx.GridHeight;
            for (int i = 0; i < total; i++)
            {
                if (ctx.CellHasCube(i))
                    output.Add(ctx.Cells[i].colorId);
            }

            foreach (var kv in ctx.GarageMap)
            {
                var cars = kv.Value.carColors;
                for (int i = 0; i < cars.Count; i++)
                    output.Add(cars[i]);
            }
        }

        static ReceiverQueueResult[] DistributeIntoQueues(List<int> receivers, int queueCount)
        {
            int totalReceivers = receivers.Count;
            int basePerQueue = totalReceivers / queueCount;
            int remainder = totalReceivers % queueCount;

            var results = new ReceiverQueueResult[queueCount];
            int offset = 0;

            for (int q = 0; q < queueCount; q++)
            {
                int count = basePerQueue + (q < remainder ? 1 : 0);
                var colorQueue = new int[count];
                for (int i = 0; i < count; i++)
                    colorQueue[i] = receivers[offset + i];
                offset += count;

                results[q] = new ReceiverQueueResult
                {
                    queueIndex = q,
                    colorTypesQueue = colorQueue,
                };
            }

            return results;
        }

        static void Shuffle(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
