﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    public class PackageDependencyComparer : IEqualityComparer<PackageDependency>
    {
        private readonly IVersionRangeComparer _versionRangeComparer;

        public PackageDependencyComparer()
            :this (VersionRangeComparer.Default)
        {

        }

        public PackageDependencyComparer(IVersionRangeComparer versionRangeComparer)
        {
            if (versionRangeComparer == null)
            {
                throw new ArgumentNullException("versionRangeComparer");
            }

            _versionRangeComparer = versionRangeComparer;
        }

        /// <summary>
        /// Default comparer
        /// Null ranges and the All range are treated as equal.
        /// </summary>
        public static readonly PackageDependencyComparer Default = new PackageDependencyComparer();

        public bool Equals(PackageDependency x, PackageDependency y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
            {
                return false;
            }

            bool result = StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id);

            if (result)
            {
                result = _versionRangeComparer.Equals(x.VersionRange ?? VersionRange.All, y.VersionRange ?? VersionRange.All);
            }

            return result;
        }

        public int GetHashCode(PackageDependency obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 0;
            }

            var combiner = new HashCodeCombiner();

            combiner.AddObject(obj.Id.ToUpperInvariant());

            // Treat null ranges and the All range as the same thing here
            if (obj.VersionRange != null && !obj.VersionRange.Equals(VersionRange.All))
            {
                combiner.AddObject(_versionRangeComparer.GetHashCode(obj.VersionRange));
            }

            return combiner.CombinedHash;
        }
    }
}
