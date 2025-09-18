// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Running;
using Microsoft.DotNet.DockerTools.TemplateGenerator.Benchmarks;

var summary = BenchmarkRunner.Run<SimpleTemplateBenchmarks>();
