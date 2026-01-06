using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.Git;
using Nuke.Common.Tools.GitHub;

[
    GitHubActions("pack", GitHubActionsImage.WindowsLatest, 
    InvokedTargets = [nameof(Pack)],
    AutoGenerate = true,
    PublishArtifacts = true,
    OnWorkflowDispatchRequiredInputs = ["Version"]),
    GitHubActions("manual_deploy", GitHubActionsImage.WindowsLatest,
    InvokedTargets = [nameof(Publish)],
    AutoGenerate = true,
    PublishArtifacts = true,
    ReadPermissions = [GitHubActionsPermissions.Contents],
    WritePermissions = [GitHubActionsPermissions.Packages],
    EnableGitHubToken = true,
    OnWorkflowDispatchRequiredInputs = ["Version"]),
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
    public static int Main () => Execute<Build>(x => x.Pack);
    
    [GitRepository]
    readonly GitRepository GitRepository;

    private string GetLatestTag() => GitRepository.Tags?.FirstOrDefault(t => t != null && t.StartsWith('v'))?.TrimStart('v');
    
    [Solution(GenerateProjects = true)] 
    readonly Solution Solution;
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    Target Clean => _ => _
        .Executes(() =>
        {
            PackagesDirectory.CreateOrCleanDirectory();
        });

    [Parameter] 
    string Version;

    [UsedImplicitly]
    Target PrepareVersion => _ => _
        .Before(Pack)
        .Unlisted()
        .OnlyWhenStatic(() => IsServerBuild)
        .Executes(() => Version = GetLatestTag());
    
    Target Pack => _ => _
        .DependsOn(Test)
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {
            Version.NotNullOrWhiteSpace("Version must be provided (use --version)");
            
            return NuGetPack(options => options
                .SetVersion(Version)
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

    [Secret, Optional, Parameter("Private Access Token for publishing Nuget packages to GitHub")]
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
