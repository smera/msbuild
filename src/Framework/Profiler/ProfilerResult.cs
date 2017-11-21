// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>The result profiling an evaluation.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.Profiler
{
    /// <summary>
    /// Result of profiling an evaluation
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public struct ProfilerResult
    {
        /// <nodoc/>
        public IReadOnlyDictionary<EvaluationLocation, ProfiledLocation> ProfiledLocations { get; }

        /// <nodoc/>
        public ProfilerResult(IReadOnlyDictionary<EvaluationLocation, ProfiledLocation> profiledLocations)
        {
            ProfiledLocations = profiledLocations;
        }
    }

    /// <summary>
    /// Result of timing the evaluation of a given element at a given location
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public struct ProfiledLocation
    {
        /// <nodoc/>
        public TimeSpan InclusiveTime { get; }

        /// <nodoc/>
        public TimeSpan ExclusiveTime { get; }

        /// <nodoc/>
        public int NumberOfHits { get; }

        /// <nodoc/>
        public ProfiledLocation(TimeSpan inclusiveTime, TimeSpan exclusiveTime, int numberOfHits)
        {
            InclusiveTime = inclusiveTime;
            ExclusiveTime = exclusiveTime;
            NumberOfHits = numberOfHits;
        }
    }
}
