// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageInfoHelper
    {
        public static void MergeRepos(RepoData[] srcRepos, List<RepoData> targetRepos)
        {
            foreach (RepoData srcRepo in srcRepos)
            {
                RepoData targetRepo = targetRepos.FirstOrDefault(r => r.Repo == srcRepo.Repo);
                if (targetRepo == null)
                {
                    targetRepos.Add(srcRepo);
                }
                else
                {
                    MergeData(srcRepo, targetRepo);
                }
            }
        }

        private static void MergeData(object srcObj, object targetObj)
        {
            if (srcObj.GetType() != targetObj.GetType())
            {
                throw new ArgumentException("Object types don't match.", nameof(targetObj));
            }

            foreach (PropertyInfo property in srcObj.GetType().GetProperties())
            {
                if (property.PropertyType == typeof(string))
                {
                    property.SetValue(targetObj, property.GetValue(srcObj));
                }
                else if (typeof(IDictionary).IsAssignableFrom(property.PropertyType))
                {
                    MergeDictionaries(property, srcObj, targetObj);
                }
                else if (typeof(IList<string>).IsAssignableFrom(property.PropertyType))
                {
                    MergeLists(property, srcObj, targetObj);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported model property type: '{property.PropertyType.FullName}'");
                }
            }
        }

        private static void MergeLists(PropertyInfo property, object srcObj, object targetObj)
        {
            IList<string> srcList = (IList<string>)property.GetValue(srcObj);
            if (srcList == null)
            {
                return;
            }

            IList<string> targetList = (IList<string>)property.GetValue(targetObj);

            if (srcList.Any())
            {
                if (targetList != null)
                {
                    targetList = targetList
                        .Union(srcList)
                        .OrderBy(element => element)
                        .ToList();
                }
                else
                {
                    targetList = srcList;
                }

                property.SetValue(targetObj, targetList);
            }
        }

        private static void MergeDictionaries(PropertyInfo property, object srcObj, object targetObj)
        {
            IDictionary srcDict = (IDictionary)property.GetValue(srcObj);
            if (srcDict == null)
            {
                return;
            }

            IDictionary targetDict = (IDictionary)property.GetValue(targetObj);

            if (srcDict.Cast<object>().Any())
            {
                if (targetDict != null)
                {
                    foreach (dynamic kvp in srcDict)
                    {
                        if (targetDict.Contains(kvp.Key))
                        {
                            MergeData(kvp.Value, targetDict[kvp.Key]);
                        }
                        else
                        {
                            targetDict[kvp.Key] = kvp.Value;
                        }
                    }
                }
                else
                {
                    property.SetValue(targetObj, srcDict);
                }
            }
        }
    }
}
