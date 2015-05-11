﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NuGet.Resolver
{
    public class ResolverComparer : IComparer<ResolverPackage>
    {
        private readonly DependencyBehavior _dependencyBehavior;
        private readonly HashSet<PackageIdentity> _installedPackages;
        private readonly HashSet<string> _newPackageIds;
        private readonly IVersionComparer _versionComparer;
        private readonly PackageIdentityComparer _identityComparer;
        private readonly Dictionary<string, NuGetVersion> _installedVersions;

        public ResolverComparer(DependencyBehavior dependencyBehavior, 
            HashSet<PackageIdentity> installedPackages, 
            HashSet<string> newPackageIds)
        {
            _dependencyBehavior = dependencyBehavior;
            _installedPackages = installedPackages;
            _newPackageIds = newPackageIds;
            _versionComparer = VersionComparer.Default;
            _identityComparer = PackageIdentity.Comparer;

            _installedVersions = new Dictionary<string, NuGetVersion>();

            if (_installedVersions != null)
            {
                foreach (var package in _installedPackages)
                {
                    if (package.Version != null)
                    {
                        _installedVersions.Add(package.Id, package.Version);
                    }
                }
            }
        }

        public int Compare(ResolverPackage x, ResolverPackage y)
        {
            Debug.Assert(string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase));

            // The absent package comes first in the sort order
            bool isXAbsent = x.Absent;
            bool isYAbsent = y.Absent;
            if (isXAbsent && !isYAbsent)
            {
                return -1;
            }
            if (!isXAbsent && isYAbsent)
            {
                return 1;
            }
            if (isXAbsent && isYAbsent)
            {
                return 0;
            }

            if (_installedPackages != null)
            {
                //Already installed packages come next in the sort order.
                bool xInstalled = _installedPackages.Contains(x, _identityComparer);
                bool yInstalled = _installedPackages.Contains(y, _identityComparer);

                if (xInstalled && !yInstalled)
                {
                    return -1;
                }

                if (!xInstalled && yInstalled)
                {
                    return 1;
                }
            }

            NuGetVersion xv = x.Version;
            NuGetVersion yv = y.Version;

            DependencyBehavior packageBehavior = _dependencyBehavior;

            // for new packages use the highest version
            if (_newPackageIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase))
            {
                packageBehavior = DependencyBehavior.Highest;
            }

            // stay as close to the installed version as possible
            // Choose upgrades over downgrades
            // For downgrades choose the highest version
            // 
            // Example:
            // 1.0.0
            // 1.1.0
            // 2.0.0 - installed
            // 2.1.0
            // 3.0.0
            // Order: 2.0.0, 2.1.0, 3.0.0, 1.1.0, 1.0.0
            if (_dependencyBehavior != DependencyBehavior.Highest && _dependencyBehavior != DependencyBehavior.Ignore)
            {
                NuGetVersion installedVersion = null;
                if (_installedVersions.TryGetValue(x.Id, out installedVersion))
                {
                    bool xvDowngrade = _versionComparer.Compare(xv, installedVersion) < 0;
                    bool yvDowngrade = _versionComparer.Compare(yv, installedVersion) < 0;

                    // the upgrade is preferred over the downgrade
                    if (xvDowngrade && !yvDowngrade)
                    {
                        return 1;
                    }
                    else if (!xvDowngrade && yvDowngrade)
                    {
                        return -1;
                    }
                    else if (xvDowngrade && yvDowngrade)
                    {
                        // when both are downgrades prefer the highest
                        return -1 * _versionComparer.Compare(xv, yv);
                    }
                }
            }

            // Normal 
            switch (_dependencyBehavior)
            {
                case DependencyBehavior.Lowest:
                    {
                        return _versionComparer.Compare(xv, yv);
                    }
                case DependencyBehavior.Ignore:
                case DependencyBehavior.Highest:
                    return -1 * _versionComparer.Compare(xv, yv);
                case DependencyBehavior.HighestMinor:
                    {
                        if (_versionComparer.Equals(xv, yv)) return 0;

                        // Take the lowest Major, then the Highest Minor and Patch
                        return new[] { x, y }.OrderBy(p => p.Version.Major)
                                           .ThenByDescending(p => p.Version.Minor)
                                           .ThenByDescending(p => p.Version.Patch).FirstOrDefault() == x ? -1 : 1;

                    }
                case DependencyBehavior.HighestPatch:
                    {
                        if (_versionComparer.Equals(xv, yv)) return 0;

                        // Take the lowest Major and Minor, then the Highest Patch
                        return new[] { x, y }.OrderBy(p => p.Version.Major)
                                             .ThenBy(p => p.Version.Minor)
                                             .ThenByDescending(p => p.Version.Patch).FirstOrDefault() == x ? -1 : 1;
                    }
                default:
                    throw new InvalidOperationException("Unknown DependencyBehavior value.");
            }
        }
    }
}