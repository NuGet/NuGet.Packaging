﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// When we're internalized into other projects, there are multiple HashCodeCombiners defined
// So make sure we select the right one
using HashCodeCombiner = NuGet.Packaging.Core.HashCodeCombiner;

namespace NuGet.Packaging
{
    /// <summary>
    /// Package dependencies grouped to a target framework.
    /// </summary>
    public class PackageDependencyGroup : IEquatable<PackageDependencyGroup>, IFrameworkSpecific
    {
        private readonly NuGetFramework _targetFramework;
        private readonly IEnumerable<PackageDependency> _packages;

        public PackageDependencyGroup(string targetFramework, IEnumerable<PackageDependency> packages)
        {
            if (packages == null)
            {
                throw new ArgumentNullException("packages");
            }

            if (String.IsNullOrEmpty(targetFramework))
            {
                _targetFramework = NuGetFramework.AnyFramework;
            }
            else
            {
                _targetFramework = NuGetFramework.Parse(targetFramework);
            }

            _packages = packages;
        }

        public PackageDependencyGroup(NuGetFramework targetFramework, IEnumerable<PackageDependency> packages)
        {
            if (targetFramework == null)
            {
                throw new ArgumentNullException("targetFramework");
            }

            if (packages == null)
            {
                throw new ArgumentNullException("packages");
            }

            _targetFramework = targetFramework;
            _packages = packages;
        }

        /// <summary>
        /// Dependency group target framework
        /// </summary>
        public NuGetFramework TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        /// <summary>
        /// Package dependencies
        /// </summary>
        public IEnumerable<PackageDependency> Packages
        {
            get
            {
                return _packages;
            }
        }

        public bool Equals(PackageDependencyGroup other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (Object.ReferenceEquals(other, null))
            {
                return false;
            }

            return GetHashCode() == other.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            PackageDependencyGroup other = obj as PackageDependencyGroup;

            if (other != null)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            HashCodeCombiner combiner = new HashCodeCombiner();

            combiner.AddObject(TargetFramework);

            if (Packages != null)
            {
                foreach (int hash in Packages.Select(e => e.GetHashCode()).OrderBy(e => e))
                {
                    combiner.AddObject(hash);
                }
            }

            return combiner.CombinedHash;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "[{0}] ({1})", TargetFramework, String.Join(", ", Packages));
        }
    }
}
