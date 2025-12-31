using Nuke.Common.Git;

[GitHubActions("pack", GitHubActionsImage.WindowsLatest, 
    InvokedTargets = [nameof(Pack)],
    AutoGenerate = true,
    PublishArtifacts = true,
    On = 
    [
        GitHubActionsTrigger.WorkflowDispatch, 
        GitHubActionsTrigger.PullRequest
    ])]
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
            PackagesDirectory.DeleteDirectory();
        });

    Target Pack => _ => _
        .DependsOn(Test)
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {

            NuGetTasks.NuGetPack(options => options
                .SetProcessWorkingDirectory(NugetDirectory)
                .SetOutputDirectory(PackagesDirectory));
        });

    Target Test => _ => _
        .OnlyWhenStatic(()=> IsWin)
        .Executes(() =>
        {
            DotNetTasks.DotNetPublish(options => options
                .SetProject(Solution.Tests.TestTarget)
                .SetRuntime(DotNetRuntimeIdentifier.win_x64)
                .SetConfiguration(Configuration));
        });

    
    
    // Paths
    AbsolutePath NugetDirectory { get => field / "nuget"; } = RootDirectory / "src";

    AbsolutePath PackagesDirectory => NugetDirectory / "packages";

}
