using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tools.GitHub;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;

[
    ShutdownDotNetAfterServerBuild,
    GitHubActions("pack", GitHubActionsImage.WindowsLatest, 
    InvokedTargets = [nameof(Pack)],
    AutoGenerate = true,
    PublishArtifacts = true,
    On = 
    [
        GitHubActionsTrigger.WorkflowDispatch, 
        GitHubActionsTrigger.PullRequest
    ]),
    GitHubActions("manual_deploy", GitHubActionsImage.WindowsLatest,
    InvokedTargets = [nameof(Publish)],
    AutoGenerate = true,
    PublishArtifacts = true,
    ReadPermissions = [GitHubActionsPermissions.Contents],
    WritePermissions = [GitHubActionsPermissions.Packages],
    EnableGitHubToken = true,
    OnWorkflowDispatchRequiredInputs = ["version"]),
    GitHubActions("deploy", GitHubActionsImage.WindowsLatest,
        InvokedTargets = [nameof(Publish)],
        AutoGenerate = true,
        PublishArtifacts = true,
        ReadPermissions = [GitHubActionsPermissions.Contents],
        WritePermissions = [GitHubActionsPermissions.Packages],
        EnableGitHubToken = true,
        OnPushTags = ["v*"])
]
class Build : NukeBuild
{
    [GitRepository]
    readonly GitRepository GitRepository;
    
    [Solution(GenerateProjects = true)] 
    readonly Solution Solution;
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    public static int Main () => Execute<Build>(x => x.Pack);

    Target Clean => _ => _
        .Executes(() =>
        {
            PackagesDirectory.CreateOrCleanDirectory();
        });

    [Parameter] 
    string Version;
    
    Target Pack => _ => _
        .DependsOn(Test)
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {
            var version = Version
                          ?? GitRepository.Tags?.FirstOrDefault(t => t != null && t.StartsWith('v'))?.TrimStart('v');
            
            if (version == null)
                Assert.Fail("Could not find a version specified for this release");
            
            return NuGetPack(options => options
                .SetVersion(version)
                .SetProcessWorkingDirectory(NugetDirectory)
                .SetOutputDirectory(PackagesDirectory));
        });

    Target Test => _ => _
        .OnlyWhenStatic(()=> IsWin)
        .Executes(() => 
            DotNetPublish(options => options
            .SetProject(Solution.Tests.TestTarget)
            .SetRuntime(DotNetRuntimeIdentifier.win_x64)
            .SetConfiguration(Configuration)));

    [Secret, Parameter("Private Access Token for publishing Nuget packages to GitHub")]
    string GithubNugetPAT;
    
    Target Publish => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(GithubNugetPAT))
                GithubNugetPAT = GitHubActions.Instance.Token;
            
            GithubNugetPAT.NotNullOrWhiteSpace();
            var source = $"https://nuget.pkg.github.com/{GitRepository.GetGitHubOwner()}/index.json";

            var preExisting = true;
            if (!NuGetSourcesList().Any(x => x.Text.Contains("github")))
            {
                preExisting = false;
                NuGetSourcesAdd(options => options
                    .SetName("github")
                    .SetUserName(GitRepository.GetGitHubOwner())
                    .SetPassword(GithubNugetPAT)
                    .SetSource(source));
            }

            NuGetPush(options => options
                .SetApiKey(GithubNugetPAT)
                .SetSource(source)
                .SetTargetPath((PackagesDirectory / "*.nupkg").GlobFiles().First()));

            if (!preExisting)
                NuGetSourcesRemove(options => options
                    .SetName("github"));
        });
    
    
    // Paths
    AbsolutePath NugetDirectory { get => field / "nuget"; } = RootDirectory / "src";

    AbsolutePath PackagesDirectory => NugetDirectory / "packages";

}
