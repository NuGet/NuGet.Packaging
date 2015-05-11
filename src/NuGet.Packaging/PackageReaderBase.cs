﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// Abstract class that both the zip and folder package readers extend
    /// This class contains the path convetions for both zip and folder readers
    /// </summary>
    public abstract class PackageReaderBase : PackageReaderCoreBase
    {
        private NuspecReader _nuspec;

        public PackageReaderBase()
            : base()
        {

        }

        /// <summary>
        /// Frameworks mentioned in the package.
        /// </summary>
        public IEnumerable<NuGetFramework> GetSupportedFrameworks()
        {
            HashSet<NuGetFramework> frameworks = new HashSet<NuGetFramework>(new NuGetFrameworkFullComparer());

            frameworks.UnionWith(GetLibItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetBuildItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetContentItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetToolItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetFrameworkItems().Select(g => g.TargetFramework));

            return frameworks.Where(f => !f.IsUnsupported).OrderBy(f => f, new NuGetFrameworkSorter());
        }

        public IEnumerable<FrameworkSpecificGroup> GetFrameworkItems()
        {
            return Nuspec.GetFrameworkReferenceGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetBuildItems()
        {
            string id = GetIdentity().Id;

            List<FrameworkSpecificGroup> results = new List<FrameworkSpecificGroup>();

            foreach (FrameworkSpecificGroup group in GetFileGroups("build"))
            {
                FrameworkSpecificGroup filteredGroup = group;

                if (group.Items.Any(e => !IsAllowedBuildFile(id, e)))
                {
                    // create a new group with only valid files
                    filteredGroup = new FrameworkSpecificGroup(group.TargetFramework, group.Items.Where(e => IsAllowedBuildFile(id, e)));

                    if (!filteredGroup.Items.Any())
                    {
                        // nothing was useful in the folder, skip this group completely
                        filteredGroup = null;
                    }
                }

                if (filteredGroup != null)
                {
                    results.Add(filteredGroup);
                }
            }

            return results;
        }

        /// <summary>
        /// only packageId.targets and packageId.props should be used from the build folder
        /// </summary>
        private static bool IsAllowedBuildFile(string packageId, string path)
        {
            string file = Path.GetFileName(path);

            return StringComparer.OrdinalIgnoreCase.Equals(file, String.Format(CultureInfo.InvariantCulture, "{0}.targets", packageId)) 
                || StringComparer.OrdinalIgnoreCase.Equals(file, String.Format(CultureInfo.InvariantCulture, "{0}.props", packageId));
        }

        public IEnumerable<FrameworkSpecificGroup> GetToolItems()
        {
            return GetFileGroups("tools");
        }

        public IEnumerable<FrameworkSpecificGroup> GetContentItems()
        {
            return GetFileGroups("content");
        }

        public IEnumerable<PackageDependencyGroup> GetPackageDependencies()
        {
            return Nuspec.GetDependencyGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetLibItems()
        {
            return GetFileGroups("lib");
        }

        /// <summary>
        /// True only for assemblies that should be added as references to msbuild projects
        /// </summary>
        private static bool IsReferenceAssembly(string path)
        {
            bool result = false;

            string extension = Path.GetExtension(path);

            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".dll"))
            {
                if (!path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".winmd"))
            {
                result = true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".exe"))
            {
                result = true;
            }

            return result;
        }

        public IEnumerable<FrameworkSpecificGroup> GetReferenceItems()
        {
            IEnumerable<FrameworkSpecificGroup> referenceGroups = Nuspec.GetReferenceGroups();
            List<FrameworkSpecificGroup> fileGroups = new List<FrameworkSpecificGroup>();

            // filter out non reference assemblies
            foreach (var group in GetLibItems())
            {
                fileGroups.Add(new FrameworkSpecificGroup(group.TargetFramework, group.Items.Where(e => IsReferenceAssembly(e))));
            }

            // results
            List<FrameworkSpecificGroup> libItems = new List<FrameworkSpecificGroup>();

            if (referenceGroups.Any())
            {
                // the 'any' group from references, for pre2.5 nuspecs this will be the only group
                var fallbackGroup = referenceGroups.Where(g => g.TargetFramework.Equals(NuGetFramework.AnyFramework)).FirstOrDefault();

                foreach (FrameworkSpecificGroup fileGroup in fileGroups)
                {
                    // check for a matching reference group to use for filtering
                    var referenceGroup = referenceGroups.Where(g => g.TargetFramework.Equals(fileGroup.TargetFramework)).FirstOrDefault();

                    if (referenceGroup == null)
                    {
                        referenceGroup = fallbackGroup;
                    }

                    if (referenceGroup == null)
                    {
                        // add the lib items without any filtering
                        libItems.Add(fileGroup);
                    }
                    else
                    {
                        List<string> filteredItems = new List<string>();

                        foreach (string path in fileGroup.Items)
                        {
                            // reference groups only have the file name, not the path
                            string file = Path.GetFileName(path);

                            if (referenceGroup.Items.Any(s => StringComparer.OrdinalIgnoreCase.Equals(s, file)))
                            {
                                filteredItems.Add(path);
                            }
                        }

                        if (filteredItems.Any())
                        {
                            libItems.Add(new FrameworkSpecificGroup(fileGroup.TargetFramework, filteredItems));
                        }
                    }
                }
            }
            else
            {
                libItems.AddRange(fileGroups);
            }

            return libItems;
        }

        protected sealed override NuspecCoreReaderBase NuspecCore
        {
            get
            {
                return Nuspec;
            }
        }

        protected virtual NuspecReader Nuspec
        {
            get
            {
                if (_nuspec == null)
                {
                    _nuspec = new NuspecReader(GetNuspec());
                }

                return _nuspec;
            }
        }

        protected IEnumerable<FrameworkSpecificGroup> GetFileGroups(string folder)
        {
            Dictionary<NuGetFramework, List<string>> groups = new Dictionary<NuGetFramework, List<string>>(new NuGetFrameworkFullComparer());

            bool isContentFolder = StringComparer.OrdinalIgnoreCase.Equals(folder, PackagingConstants.ContentFolder);
            bool allowSubFolders = true;

            foreach (string path in GetFiles(folder))
            {
                // Use the known framework or if the folder did not parse, use the Any framework and consider it a sub folder
                NuGetFramework framework = GetFrameworkFromPath(path, allowSubFolders);

                List<string> items = null;
                if (!groups.TryGetValue(framework, out items))
                {
                    items = new List<string>();
                    groups.Add(framework, items);
                }

                items.Add(path);
            }

            // Sort the groups by framework, and the items by ordinal string compare to keep things deterministic
            foreach (NuGetFramework framework in groups.Keys.OrderBy(e => e, new NuGetFrameworkSorter()))
            {
                yield return new FrameworkSpecificGroup(framework, groups[framework].OrderBy(e => e, StringComparer.OrdinalIgnoreCase));
            }

            yield break;
        }

        /// <summary>
        /// Return property values for the given key. Case-sensitive.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private static IEnumerable<string> GetPropertyValues(IEnumerable<KeyValuePair<string, string>> properties, string key)
        {
            if (properties == null)
            {
                return Enumerable.Empty<string>();
            }

            if (!String.IsNullOrEmpty(key))
            {
                return properties.Select(p => p.Value);
            }

            return properties.Where(p => StringComparer.Ordinal.Equals(p.Key, key)).Select(p => p.Value);
        }

        private static string GetFileName(string path)
        {
            return path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        private static NuGetFramework GetFrameworkFromPath(string path, bool allowSubFolders = false)
        {
            NuGetFramework framework = NuGetFramework.AnyFramework;

            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // ignore paths that are too short, and ones that have additional sub directories
            if (parts.Length == 3 || (parts.Length > 3 && allowSubFolders))
            {
                string folderName = parts[1];

                var parsedFramework = NuGetFramework.ParseFolder(folderName);

                if (parsedFramework.IsSpecificFramework)
                {
                    // the folder name is a known target framework
                    framework = parsedFramework;
                }
            }

            return framework;
        }

        protected abstract IEnumerable<string> GetFiles(string folder);
    }
}
