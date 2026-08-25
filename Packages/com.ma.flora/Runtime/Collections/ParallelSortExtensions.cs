using System.Threading;
using UnityEngine.Assertions;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace MA.Flora
{
    internal static class ParallelSortExtensions
    {
        private const int MinRadixSortArraySize = 2048;
        private const int MinRadixSortBatchSize = 256;

        public static JobHandle ParallelSort(this NativeArray<int> array, JobHandle inputDeps = default)
        {
            if (array.Length <= 1)
                return new JobHandle();

            var jobHandle = inputDeps;

            if (array.Length >= MinRadixSortArraySize)
            {
                int workersCount = math.max(JobsUtility.JobWorkerCount + 1, 1);
                int batchSize = math.max(MinRadixSortBatchSize, (int)math.ceil((float)array.Length / workersCount));
                int jobsCount = (int)math.ceil((float)array.Length / batchSize);

                Assert.IsTrue(jobsCount * batchSize >= array.Length);

                var supportArray = new NativeArray<int>(array.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var counter = new NativeArray<int>(1, Allocator.TempJob);
                var buckets = new NativeArray<int>(jobsCount * MinRadixSortBatchSize, Allocator.TempJob);
                var indices = new NativeArray<int>(jobsCount * MinRadixSortBatchSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var indicesSum = new NativeArray<int>(16, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                var arraySource = array;
                var arrayDest = supportArray;

                for (int radix = 0; radix < 4; ++radix)
                {
                    var bucketCountJobData = new RadixSortBucketCountJob
                    {
                        Radix = radix,
                        JobsCount = jobsCount,
                        BatchSize = batchSize,
                        Buckets = buckets,
                        Array = arraySource
                    };

                    var batchPrefixSumJobData = new RadixSortBatchPrefixSumJob
                    {
                        Radix = radix,
                        JobsCount = jobsCount,
                        Array = arraySource,
                        Counter = counter,
                        Buckets = buckets,
                        Indices = indices,
                        IndicesSum = indicesSum
                    };

                    var prefixSumJobData = new RadixSortPrefixSumJob { JobsCount = jobsCount, Indices = indices, IndicesSum = indicesSum };

                    var bucketSortJobData = new RadixSortBucketSortJob
                    {
                        Radix = radix,
                        BatchSize = batchSize,
                        Indices = indices,
                        Array = arraySource,
                        ArraySorted = arrayDest
                    };

                    jobHandle = bucketCountJobData.ScheduleParallel(jobsCount, 1, jobHandle);
                    jobHandle = batchPrefixSumJobData.ScheduleParallel(16, 1, jobHandle);
                    jobHandle = prefixSumJobData.ScheduleParallel(16, 1, jobHandle);
                    jobHandle = bucketSortJobData.ScheduleParallel(jobsCount, 1, jobHandle);
                    (arraySource, arrayDest) = (arrayDest, arraySource);// Swap references
                }

                supportArray.Dispose(jobHandle);
                counter.Dispose(jobHandle);
                buckets.Dispose(jobHandle);
                indices.Dispose(jobHandle);
                indicesSum.Dispose(jobHandle);
            }
            else
            {
                jobHandle = array.SortJob().Schedule(inputDeps);
            }

            return jobHandle;
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSortBucketCountJob : IJobFor
        {
            [ReadOnly] public int Radix;
            [ReadOnly] public int JobsCount;
            [ReadOnly] public int BatchSize;

            [ReadOnly] public NativeArray<int> Array;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Buckets;

            public void Execute(int index)
            {
                int start = index * BatchSize;
                int end = math.min(start + BatchSize, Array.Length);

                int jobBuckets = index * 256;

                for (int i = start; i < end; ++i)
                {
                    int value = Array[i];
                    int bucket = (value >> Radix * 8) & 0xFF;
                    Buckets[jobBuckets + bucket] += 1;
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSortBatchPrefixSumJob : IJobFor
        {
            [ReadOnly] public int Radix;
            [ReadOnly] public int JobsCount;

            [ReadOnly] [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Array;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Counter;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> IndicesSum;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Buckets;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Indices;

            private static unsafe int AtomicIncrement(NativeArray<int> counter)
            {
                return Interlocked.Increment(ref UnsafeUtility.AsRef<int>((int*)counter.GetUnsafePtr()));
            }

            private int JobIndexPrefixSum(int sum, int i)
            {
                for (int j = 0; j < JobsCount; ++j)
                {
                    int k = i + j * MinRadixSortBatchSize;

                    Indices[k] = sum;
                    sum += Buckets[k];
                    Buckets[k] = 0;
                }

                return sum;
            }

            public void Execute(int index)
            {
                int start = index * 16;
                int end = start + 16;

                int jobSum = 0;

                for (int i = start; i < end; ++i)
                    jobSum = JobIndexPrefixSum(jobSum, i);

                IndicesSum[index] = jobSum;

                if (AtomicIncrement(Counter) == 16)
                {
                    int sum = 0;

                    if (Radix < 3)
                    {
                        for (int i = 0; i < 16; ++i)
                        {
                            int indexSum = IndicesSum[i];
                            IndicesSum[i] = sum;
                            sum += indexSum;
                        }
                    }
                    else // Negative
                    {
                        for (int i = 8; i < 16; ++i)
                        {
                            int indexSum = IndicesSum[i];
                            IndicesSum[i] = sum;
                            sum += indexSum;
                        }

                        for (int i = 0; i < 8; ++i)
                        {
                            int indexSum = IndicesSum[i];
                            IndicesSum[i] = sum;
                            sum += indexSum;
                        }
                    }

                    Assert.AreEqual(sum, Array.Length);

                    Counter[0] = 0;
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSortPrefixSumJob : IJobFor
        {
            [ReadOnly] public int JobsCount;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> IndicesSum;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Indices;

            public void Execute(int index)
            {
                int start = index * 16;
                int end = start + 16;

                int jobSum = IndicesSum[index];

                for (int j = 0; j < JobsCount; ++j)
                {
                    for (int i = start; i < end; ++i)
                    {
                        int k = j * MinRadixSortBatchSize + i;
                        Indices[k] += jobSum;
                    }
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSortBucketSortJob : IJobFor
        {
            [ReadOnly] public int Radix;
            [ReadOnly] public int BatchSize;

            [ReadOnly] [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Array;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Indices;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> ArraySorted;

            public void Execute(int index)
            {
                int start = index * BatchSize;
                int end = math.min(start + BatchSize, Array.Length);

                int jobIndices = index * MinRadixSortBatchSize;

                for (int i = start; i < end; ++i)
                {
                    int value = Array[i];
                    int bucket = (value >> Radix * 8) & 0xFF;
                    int sortedIndex = Indices[jobIndices + bucket]++;
                    ArraySorted[sortedIndex] = value;
                }
            }
        }
    }

    internal static class ParallelSortLongExtensions
    {
        private const int MinRadixSortArraySize  = 2048;
        private const int MinRadixSortBatchSize  = 256;

        /// <summary>
        /// Schedules an in-place parallel radix sort on a <see cref="NativeArray{Int64}"/>.
        /// </summary>
        public static JobHandle ParallelSort(this NativeArray<long> array, JobHandle inputDeps = default)
        {
            if (array.Length <= 1)
                return default;

            JobHandle jobHandle = inputDeps;

            if (array.Length >= MinRadixSortArraySize)
            {
                int workersCount = math.max(JobsUtility.JobWorkerCount + 1, 1);
                int batchSize = math.max(MinRadixSortBatchSize, (int)math.ceil((float)array.Length / workersCount));
                int jobsCount = (int)math.ceil((float)array.Length / batchSize);

                Assert.IsTrue(jobsCount * batchSize >= array.Length);

                var supportArray = new NativeArray<long>(array.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var counter = new NativeArray<int>(1, Allocator.TempJob);
                var buckets = new NativeArray<int>(jobsCount * MinRadixSortBatchSize, Allocator.TempJob);
                var indices = new NativeArray<int>(jobsCount * MinRadixSortBatchSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var indicesSum = new NativeArray<int>(16, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                var arraySource = array;
                var arrayDest = supportArray;

                for (int radix = 0; radix < 8; ++radix)
                {
                    var bucketCountJob = new RadixSort64BucketCountJob
                    {
                        Radix     = radix,
                        JobsCount = jobsCount,
                        BatchSize = batchSize,
                        Buckets   = buckets,
                        Array     = arraySource
                    };

                    var batchPrefixJob = new RadixSort64BatchPrefixSumJob
                    {
                        Radix     = radix,
                        JobsCount = jobsCount,
                        Array     = arraySource,
                        Counter   = counter,
                        Buckets   = buckets,
                        Indices   = indices,
                        IndicesSum = indicesSum
                    };

                    var prefixJob = new RadixSort64PrefixSumJob
                    {
                        JobsCount = jobsCount,
                        Indices   = indices,
                        IndicesSum = indicesSum
                    };

                    var bucketSortJob = new RadixSort64BucketSortJob
                    {
                        Radix       = radix,
                        BatchSize   = batchSize,
                        Indices     = indices,
                        Array       = arraySource,
                        ArraySorted = arrayDest
                    };

                    jobHandle = bucketCountJob.ScheduleParallel(jobsCount, 1, jobHandle);
                    jobHandle = batchPrefixJob.ScheduleParallel(16, 1, jobHandle);
                    jobHandle = prefixJob.ScheduleParallel(16, 1, jobHandle);
                    jobHandle = bucketSortJob.ScheduleParallel(jobsCount, 1, jobHandle);
                    (arraySource, arrayDest) = (arrayDest, arraySource); // Swap references
                }

                supportArray.Dispose(jobHandle);
                counter.Dispose(jobHandle);
                buckets.Dispose(jobHandle);
                indices.Dispose(jobHandle);
                indicesSum.Dispose(jobHandle);
            }
            else
            {
                // Falls back to Unity’s built-in single-threaded sort when the
                // payload is small.
                jobHandle = array.SortJob().Schedule(inputDeps);
            }

            return jobHandle;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Internal Jobs (64-bit versions)
        // ──────────────────────────────────────────────────────────────────────────

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSort64BucketCountJob : IJobFor
        {
            [ReadOnly] public int Radix;
            [ReadOnly] public int JobsCount;
            [ReadOnly] public int BatchSize;

            [ReadOnly] public NativeArray<long> Array;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Buckets;

            public void Execute(int index)
            {
                int start = index * BatchSize;
                int end   = math.min(start + BatchSize, Array.Length);
                int jobBuckets = index * 256;

                for (int i = start; i < end; ++i)
                {
                    long value  = Array[i];
                    int bucket  = (int)((value >> (Radix * 8)) & 0xFF);
                    Buckets[jobBuckets + bucket] += 1;
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSort64BatchPrefixSumJob : IJobFor
        {
            [ReadOnly] public int Radix;
            [ReadOnly] public int JobsCount;

            [ReadOnly, NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<long> Array;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Counter;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> IndicesSum;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Buckets;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Indices;

            private static unsafe int AtomicIncrement(NativeArray<int> counter)
            {
                return Interlocked.Increment(ref UnsafeUtility.AsRef<int>(
                    (int*)counter.GetUnsafePtr()));
            }

            private int JobIndexPrefixSum(int sum, int i)
            {
                for (int j = 0; j < JobsCount; ++j)
                {
                    int k = i + j * MinRadixSortBatchSize;
                    Indices[k] = sum;
                    sum       += Buckets[k];
                    Buckets[k]  = 0;
                }

                return sum;
            }

            public void Execute(int index)
            {
                int start = index * 16;
                int end   = start + 16;

                int jobSum = 0;
                for (int i = start; i < end; ++i)
                    jobSum = JobIndexPrefixSum(jobSum, i);

                IndicesSum[index] = jobSum;

                // Last worker normalises prefix sums once all 16 batches are done
                if (AtomicIncrement(Counter) == 16)
                {
                    int sum = 0;

                    if (Radix < 7)          // passes 0-6: normal order
                    {
                        for (int i = 0; i < 16; ++i)
                        {
                            int s = IndicesSum[i];
                            IndicesSum[i] = sum;
                            sum += s;
                        }
                    }
                    else                    // pass 7: negatives first
                    {
                        for (int i = 8; i < 16; ++i)   // buckets 128-255
                        {
                            int s = IndicesSum[i];
                            IndicesSum[i] = sum;
                            sum += s;
                        }
                        for (int i = 0; i < 8; ++i)    // buckets 0-127
                        {
                            int s = IndicesSum[i];
                            IndicesSum[i] = sum;
                            sum += s;
                        }
                    }

                    Assert.AreEqual(sum, Array.Length);
                    Counter[0] = 0;        // reset for next radix pass
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSort64PrefixSumJob : IJobFor
        {
            [ReadOnly] public int JobsCount;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> IndicesSum;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Indices;

            public void Execute(int index)
            {
                int start = index * 16;
                int end   = start + 16;

                int jobSum = IndicesSum[index];
                for (int j = 0; j < JobsCount; ++j)
                {
                    for (int i = start; i < end; ++i)
                    {
                        int k = j * MinRadixSortBatchSize + i;
                        Indices[k] += jobSum;
                    }
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        internal struct RadixSort64BucketSortJob : IJobFor
        {
            [ReadOnly] public int Radix;
            [ReadOnly] public int BatchSize;

            [ReadOnly, NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<long> Array;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<int> Indices;

            [NativeDisableContainerSafetyRestriction, NoAlias]
            public NativeArray<long> ArraySorted;

            public void Execute(int index)
            {
                int start = index * BatchSize;
                int end   = math.min(start + BatchSize, Array.Length);
                int jobIndices = index * MinRadixSortBatchSize;

                for (int i = start; i < end; ++i)
                {
                    long value = Array[i];
                    int bucket = (int)((value >> (Radix * 8)) & 0xFF);
                    int sortedIndex = Indices[jobIndices + bucket]++;
                    ArraySorted[sortedIndex] = value;
                }
            }
        }
    }
}
