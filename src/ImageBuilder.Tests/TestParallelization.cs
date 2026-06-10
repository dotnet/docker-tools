// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Match xUnit's default behavior of running test classes in parallel while
// keeping the tests within a single class serialized. Workers = 0 lets MSTest
// use the available processor count.
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.ClassLevel)]
