// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Location for different elements tracked by the evaluation profiler.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.Profiler
{
    /// <summary>
    /// Represents a location for different evaluation elements tracked by the EvaluationProfiler.
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public struct EvaluationLocation
    {
        /// <nodoc/>
        public double EvaluationPassOrdinal { get; }

        /// <nodoc/>
        public string EvaluationPass { get; }

        /// <nodoc/>
        public string File { get; }

        /// <nodoc/>
        public int? Line { get; }

        /// <nodoc/>
        public string ElementName { get; }

        /// <nodoc/>
        public string ElementOrCondition { get; }

        /// <summary>
        /// True when <see cref="ElementOrCondition"/> is an element
        /// </summary>
        public bool IsElement { get; }

        private EvaluationLocation(double evaluationPassOrdinal, string evaluationPass, string file, int? line, string condition)
            : this(evaluationPassOrdinal, evaluationPass, file, line, "Condition", condition, isElement: false)
        {}

        private EvaluationLocation(double evaluationPassOrdinal, string evaluationPass, string file, int? line, IProjectElement element)
            : this(evaluationPassOrdinal, evaluationPass, file, line, element?.ElementName, element?.OuterXmlElement, isElement: true)
        {}

        /// <nodoc/>
        public EvaluationLocation(double evaluationPassOrdinal, string evaluationPass, string file, int? line, string elementName, string elementOrCondition, bool isElement)
        {
            EvaluationPassOrdinal = evaluationPassOrdinal;
            EvaluationPass = evaluationPass;
            File = file;
            Line = line;
            ElementName = elementName;
            ElementOrCondition = elementOrCondition;
            IsElement = isElement;
        }

        private static readonly EvaluationLocation Empty = new EvaluationLocation();

        /// <summary>
        /// An empty location, used as the starting instance.
        /// </summary>
        public static EvaluationLocation EmptyLocation { get; } = Empty;
        
        /// <nodoc/>
        public EvaluationLocation WithEvaluationPass(double ordinal, string evaluationPass)
        {
            return new EvaluationLocation(ordinal, evaluationPass, this.File, this.Line, this.ElementName, this.ElementOrCondition, this.IsElement);
        }

        /// <nodoc/>
        public EvaluationLocation WithFile(string file)
        {
            return new EvaluationLocation(this.EvaluationPassOrdinal, this.EvaluationPass, file, null, null, null, this.IsElement);
        }

        /// <nodoc/>
        public EvaluationLocation WithFileLineAndElement(string file, int? line, IProjectElement element)
        {
            return new EvaluationLocation(this.EvaluationPassOrdinal, this.EvaluationPass, file, line, element);
        }

        /// <nodoc/>
        public EvaluationLocation WithFileLineAndCondition(string file, int? line, string condition)
        {
            return new EvaluationLocation(this.EvaluationPassOrdinal, this.EvaluationPass, file, line, condition);
        }

        /// <nodoc/>
        public override bool Equals(object obj)
        {
            if (obj is EvaluationLocation other)
            {
                return
                    Math.Abs(EvaluationPassOrdinal - other.EvaluationPassOrdinal) < .0001 &&
                    EvaluationPass == other.EvaluationPass &&
                    string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase) &&
                    Line == other.Line &&
                    ElementName == other.ElementName;
            }
            return false;
        }

        /// <nodoc/>
        public override int GetHashCode()
        {
            var hashCode = 590978104;
            hashCode = hashCode * -1521134295 + base.GetHashCode();

            hashCode = hashCode * -1521134295 + EqualityComparer<double>.Default.GetHashCode(EvaluationPassOrdinal);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(EvaluationPass);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(File?.ToLower());
            hashCode = hashCode * -1521134295 + EqualityComparer<int?>.Default.GetHashCode(Line);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ElementName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ElementOrCondition);

            return hashCode;
        }

        /// <nodoc/>
        public override string ToString()
        {
            return $"{EvaluationPass ?? string.Empty}\t{File ?? string.Empty}\t{Line?.ToString() ?? string.Empty}\t{ElementName ?? string.Empty}";
        }
    }
}
