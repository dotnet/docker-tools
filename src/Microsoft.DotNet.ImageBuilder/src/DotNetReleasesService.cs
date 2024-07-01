// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IDotNetReleasesService))]
    public class DotNetReleasesService : IDotNetReleasesService
    {
        public async Task<Dictionary<string, DateOnly?>> GetProductEolDatesFromReleasesJson()
        {
            Dictionary<string, DateOnly?> productEolDates = [];

            ProductCollection col = await ProductCollection.GetAsync();

            foreach (Product product in col)
            {
                if (product.EndOfLifeDate != null &&
                    product.EndOfLifeDate < DateTime.Today)
                {
                    productEolDates.Add(product.ProductVersion, DateOnly.FromDateTime((DateTime)product.EndOfLifeDate));
                }
            }

            return productEolDates;
        }
    }
}
