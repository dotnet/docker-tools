// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public static class NotificationLabels
    {
        private const string NotifyPrefix = "notify-";
        public const string AutoBuilder = NotifyPrefix + "autobuilder";
        public const string Failure = NotifyPrefix + "failure";
    }
}
