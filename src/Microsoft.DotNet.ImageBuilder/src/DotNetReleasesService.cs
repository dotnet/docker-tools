// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IDotNetReleasesService))]
    public class DotNetReleasesService : IDotNetReleasesService
    {
        public async Task<Dictionary<string, DateOnly>> GetProductEolDatesFromReleasesJson()
        {
            Dictionary<string, DateOnly> productEolDates = [];

            ProductCollection dotnetProducts = await ProductCollection.GetAsync();

            foreach (Product product in dotnetProducts)
            {
                if (product.EndOfLifeDate != null &&
                    product.EndOfLifeDate <= DateTime.Today)
                {
                    productEolDates.Add(product.ProductVersion, DateOnly.FromDateTime((DateTime)product.EndOfLifeDate));
                }
            }

            return productEolDates;
        }
    }
}
