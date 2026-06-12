// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// The equivalence property promised in <see cref="RepoHostPropertyTests"/>:
/// the real <see cref="RepoHostEngine"/> (git observed via
/// <see cref="LocalRepo"/>, pull requests via <see cref="FakePullRequestApi"/>)
/// behaves identically to the <see cref="ModelRepoHost"/> executable
/// specification on generated scenarios. This transfers every property proven
/// against the model to the real engine — and therefore to
/// <see cref="GitHubRepoHost"/>, which is the engine plus a thin Octokit
/// adapter.
///
/// Worlds are compared via <see cref="ModelRepo.Snapshot"/> with commit SHAs
/// masked: the model's synthetic SHAs can never match real git SHAs, and they
/// only appear inside stop comments.
/// </summary>
[TestClass]
public class RepoHostEquivalenceTests
{
    // Real git commands dominate the runtime, so run fewer (but deeper)
    // scenarios than the in-memory properties do.
    private const int Iterations = 50;

    /// <summary>
    /// Replays a generated history of automation runs and human activity on
    /// both the model and a real git repository, checking after every command
    /// that the two worlds are still identical and that ensure results agree.
    /// </summary>
    [TestMethod]
    public void EngineMatchesModelOnGeneratedScenarios() =>
        AutomationGen
            .Setup(maxCommands: 5)
            .Sample(
                iter: Iterations,
                assert: setup =>
                {
                    using var real = RealWorld.Create(setup.MainTree);
                    World model = World.Create(new Setup(setup.MainTree, []));

                    foreach (WorldCommand command in setup.Commands)
                    {
                        EnsureResult? modelResult = model.Execute(command);
                        EnsureResult? realResult = real.Execute(command);

                        if (modelResult is not null)
                        {
                            realResult.ShouldNotBeNull($"executing '{command}'");
                            realResult.Outcome.ShouldBe(modelResult.Outcome, $"executing '{command}'");
                            (realResult.CommitSha is null).ShouldBe(modelResult.CommitSha is null);
                            realResult.Url.ShouldBe(modelResult.Url);
                            MaskShas(realResult.Detail).ShouldBe(MaskShas(modelResult.Detail));
                        }

                        MaskShas(real.ToModelRepo().Snapshot())
                            .ShouldBe(MaskShas(model.Repo.Snapshot()), $"after executing '{command}'");
                    }
                });

    /// <summary>
    /// Dry-run equivalence: after replaying a shared history on both worlds, a
    /// dry-run ensure leaves the real repository untouched and reports the
    /// same outcome (and explanation) as the model's dry run.
    /// </summary>
    [TestMethod]
    public void DryRunEngineMatchesModel() =>
        Gen.Select(AutomationGen.Setup(maxCommands: 3), AutomationGen.EnsureCommand())
            .Sample(
                iter: Iterations,
                assert: (setup, command) =>
                {
                    using var real = RealWorld.Create(setup.MainTree);
                    World model = World.Create(new Setup(setup.MainTree, []));
                    foreach (WorldCommand replayed in setup.Commands)
                    {
                        model.Execute(replayed);
                        real.Execute(replayed);
                    }

                    string preSnapshot = MaskShas(real.ToModelRepo().Snapshot());

                    EnsureResult modelResult = model.Execute(command, World.HostFor(model.Repo, isDryRun: true))!;
                    EnsureResult realResult = real.Execute(command, real.HostFor(isDryRun: true)).ShouldNotBeNull();

                    realResult.Outcome.ShouldBe(modelResult.Outcome);
                    realResult.Url.ShouldBe(modelResult.Url);
                    MaskShas(realResult.Detail).ShouldBe(MaskShas(modelResult.Detail));
                    MaskShas(real.ToModelRepo().Snapshot()).ShouldBe(preSnapshot);
                });

    [return: NotNullIfNotNull(nameof(text))]
    private static string? MaskShas(string? text) =>
        text is null ? null : Regex.Replace(text, "[0-9a-f]{40}", "<sha>");
}
