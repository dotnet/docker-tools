// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;

namespace Microsoft.DotNet.ImageBuilder;

public class HttpPolicyBuilder
{
    private readonly List<AsyncPolicy<HttpResponseMessage>> _policies = [];

    public static HttpPolicyBuilder Create() => new HttpPolicyBuilder();

    public HttpPolicyBuilder WithRefreshAccessTokenPolicy(Func<Task> refreshAccessToken, ILogger logger)
    {
        _policies.Add(Policy
            .HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.Unauthorized)
            .RetryAsync(1, async (result, retryCount, context) =>
            {
                logger.LogInformation(
                    "Unauthorized status code returned for '{RequestUri}'. Refreshing access token and retrying.",
                    result.Result.RequestMessage?.RequestUri);
                await refreshAccessToken();
            }));
        return this;
    }

    public HttpPolicyBuilder WithNotFoundRetryPolicy(TimeSpan timeout, TimeSpan retryFrequency, ILogger logger)
    {
        IEnumerable<TimeSpan> sleepDurations = Enumerable
            .Repeat(retryFrequency.TotalSeconds, (int)(timeout.TotalSeconds / retryFrequency.TotalSeconds))
            .Select(TimeSpan.FromSeconds);
        _policies.Add(Policy.HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.NotFound)
            .WaitAndRetryAsync(sleepDurations, (result, duration) =>
            {
                logger.LogInformation(
                    "NotFound status code returned for '{RequestUri}'. Retrying in {RetrySeconds} seconds.",
                    result.Result.RequestMessage?.RequestUri,
                    retryFrequency.TotalSeconds);
            }));
        return this;
    }

    public AsyncPolicy<HttpResponseMessage>? Build()
    {
        if (_policies.Count == 0)
        {
            return null;
        }

        AsyncPolicy<HttpResponseMessage> policy = _policies[0];
        for (int i = 1; i < _policies.Count; i++)
        {
            policy = policy.WrapAsync(_policies[i]);
        }

        return policy;
    }
}
