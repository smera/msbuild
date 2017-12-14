﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Profiler log listener.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Listens to build evaluation finished events and collects profiling information when available
    /// </summary>
    public sealed class ProfilerLogger : ILogger
    {
        /// <summary>
        /// Accumulates the result of profiling each project. Computing the aggregated result is deferred till the end of the build
        /// to interfere as less as possible with evaluation times
        /// </summary>
        private readonly ConcurrentQueue<ProfilerResult> _profiledResults = new ConcurrentQueue<ProfilerResult>();

        /// <summary>
        /// Aggregation of all profiled locations. Computed the first time <see cref="GetAggregatedResult"/> is called.
        /// </summary>
        private Dictionary<EvaluationLocation, ProfiledLocation> _aggregatedLocations = null;

        /// <summary>
        /// If null, no file is saved to disk
        /// </summary>
        public string FileToLog { get; }

        /// <nodoc/>
        public ProfilerLogger(string fileToLog)
        {
            FileToLog = fileToLog;
        }

        /// <summary>
        /// Creates a logger for testing purposes that gathers profiling information but doesn't save a file to disk with the report
        /// </summary>
        internal static ProfilerLogger CreateForTesting()
        {
            return new ProfilerLogger(fileToLog: null);
        }

        /// <summary>
        /// Verbosity is ignored by this logger
        /// </summary>
        public LoggerVerbosity Verbosity { get; set; }

        /// <summary>
        /// No specific parameters are used by this logger
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Subscribes to status events, which is the category for the evaluation finished event.
        /// </summary>
        public void Initialize(IEventSource eventSource)
        {
            eventSource.StatusEventRaised += ProjectEvaluationFinishedRaised;
        }

        /// <summary>
        /// On shutdown, the profiler report is written to disk
        /// </summary>
        public void Shutdown()
        {
            // Tests may pass null so no file is saved to disk
            if (FileToLog != null)
            {
                GenerateProfilerReport();
            }
        }

        private void ProjectEvaluationFinishedRaised(object sender, BuildEventArgs e)
        {
            // We are only interested in project evaluation finished events, and only
            // in the case there is actually a profiler result in it
            var projectFinishedEvent = e as ProjectEvaluationFinishedEventArgs;
            if (projectFinishedEvent?.ProfilerResult == null)
            {
                return;
            }

            // The aggregation is delayed until the whole aggregated result is needed for generating the final content
            // There is a memory/speed trade off here, but we are prioritizing not interfeering with regular evaluation times
            Debug.Assert(_aggregatedLocations == null, "GetAggregatedResult was called, but a new ProjectEvaluationFinishedEventArgs arrived after that.");
            _profiledResults.Enqueue(projectFinishedEvent.ProfilerResult.Value);
        }

        /// <summary>
        /// Returns the result of aggregating all profiled projects across a build
        /// </summary>
        /// <remarks>
        /// Not thread safe. After this method is called, the assumption is that no new ProjectEvaluationFinishedEventArgs will arrive.
        /// In the regular code path, this method is called only once per build. But some test cases may call it multiple times to validate 
        /// the aggregated data
        /// </remarks>
        internal ProfilerResult GetAggregatedResult()
        {
            if (_aggregatedLocations != null)
            {
                return new ProfilerResult(_aggregatedLocations);
            }

            _aggregatedLocations = new Dictionary<EvaluationLocation, ProfiledLocation>();

            while (!_profiledResults.IsEmpty)
            {
                ProfilerResult profiledResult;
                var result = _profiledResults.TryDequeue(out profiledResult);
                Debug.Assert(result,
                    "Expected a non empty queue, this method is not supposed to be called in a multithreaded way");

                foreach (var pair in profiledResult.ProfiledLocations)
                {
                    //  Add elapsed times to evaluation counter dictionaries
                    ProfiledLocation previousTimeSpent;
                    if (!_aggregatedLocations.TryGetValue(pair.Key, out previousTimeSpent))
                    {
                        previousTimeSpent = new ProfiledLocation(TimeSpan.Zero, TimeSpan.Zero, 0);
                    }

                    var updatedTimeSpent = AggregateProfiledLocation(previousTimeSpent, pair.Value);

                    _aggregatedLocations[pair.Key] = updatedTimeSpent;
                }
            }

            // After aggregating all items, prune the ones that are too small to report
            _aggregatedLocations = PruneSmallItems(_aggregatedLocations);

            // Add one single top-level item representing the total aggregated evaluation time for globs.
            var aggregatedGlobs = _aggregatedLocations.Keys
                .Where(key => key.Kind == EvaluationLocationKind.Glob)
                .Aggregate(new ProfiledLocation(),
                    (profiledLocation, evaluationLocation) =>
                        AggregateProfiledLocation(profiledLocation, _aggregatedLocations[evaluationLocation]));

            _aggregatedLocations[EvaluationLocation.CreateLocationForAggregatedGlob()] =
                aggregatedGlobs;

            return new ProfilerResult(_aggregatedLocations);
        }

        private static Dictionary<EvaluationLocation, ProfiledLocation> PruneSmallItems(IDictionary<EvaluationLocation, ProfiledLocation> aggregatedLocations)
        {
            var result = new Dictionary<EvaluationLocation, ProfiledLocation>();

            // Let's build an index of profiled locations by id, to speed up subsequent queries
            var idTable = aggregatedLocations.ToDictionary(pair => pair.Key.Id, pair => new Pair<EvaluationLocation, ProfiledLocation>(pair.Key, pair.Value));

            // We want to keep all evaluation pass entries plus the big enough regular entries
            foreach (var prunedPair in aggregatedLocations.Where(pair => pair.Key.IsEvaluationPass || !IsTooSmall(pair.Value)))
            {
                var key = prunedPair.Key;
                // We already know this pruned pair is something we want to keep. But the parent may be broken since we may remove it
                var parentId = FindBigEnoughParentId(idTable, key.ParentId);
                result[key.WithParentId(parentId)] = prunedPair.Value;
            }
            
            return result;
        }

        /// <summary>
        /// Finds the first ancestor of parentId (which could be itself) that is either an evaluation pass location or a big enough profiled data
        /// </summary>
        private static int? FindBigEnoughParentId(IDictionary<int, Pair<EvaluationLocation, ProfiledLocation>> idTable, int? parentId)
        {
            // The parent id is null, which means the item was pointing to an evaluation pass item. So we keep it as is.
            if (!parentId.HasValue)
            {
                return null;
            }

            var pair = idTable[parentId.Value];

            // We go up the parent relationship until we find an item that is either an evaluation pass or a big enough regular item
            while (!pair.Key.IsEvaluationPass || IsTooSmall(pair.Value))
            {
                Debug.Assert(pair.Key.ParentId.HasValue, "A location that is not an evaluation pass should always have a parent");
                pair = idTable[pair.Key.ParentId.Value];
            }

            return pair.Key.Id;
        }

        private static bool IsTooSmall(ProfiledLocation profiledData)
        {
            return profiledData.InclusiveTime.TotalMilliseconds < 1 ||
                   profiledData.ExclusiveTime.TotalMilliseconds < 1;
        }

        private static ProfiledLocation AggregateProfiledLocation(ProfiledLocation location, ProfiledLocation otherLocation)
        {
            return new ProfiledLocation(
                location.InclusiveTime + otherLocation.InclusiveTime,
                location.ExclusiveTime + otherLocation.ExclusiveTime,
                location.NumberOfHits + 1
            );
        }

        /// <summary>
        /// Gets the markdown content of the aggregated results and saves it to disk
        /// </summary>
        private void GenerateProfilerReport()
        {
            try
            {
                var profilerFile = FileToLog;
                Console.WriteLine(ResourceUtilities.FormatResourceString("WritingProfilerReport", profilerFile));

                var content = ProfilerResultPrettyPrinter.GetMarkdownContent(GetAggregatedResult());
                File.WriteAllText(profilerFile, content);

                Console.WriteLine(ResourceUtilities.GetResourceString("WritingProfilerReportDone"));
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
            catch (IOException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
            catch (SecurityException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
        }
    }
}
