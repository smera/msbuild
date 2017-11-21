﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Interface for exposing a ProjectElement to the appropriate loggers.</summary>
//-----------------------------------------------------------------------

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for exposing a ProjectElement to the appropriate loggers
    /// </summary>
    public interface IProjectElement
    {

        /// <summary>
        /// Gets the name of the associated element. 
        /// Useful for display in some circumstances.
        /// </summary>
        string ElementName { get; }


        /// <summary>
        /// The outer xml markup of the xml element associated with this project element/>
        /// </summary>
        string OuterXmlElement { get; }
    }
}
