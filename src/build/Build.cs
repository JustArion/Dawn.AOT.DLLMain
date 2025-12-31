[GitHubActions("pack", GitHubActionsImage.UbuntuLatest, 
    InvokedTargets = [nameof(Test), nameof(Pack)],
    AutoGenerate = true,
    On = 
    [
        GitHubActionsTrigger.WorkflowDispatch, 
        GitHubActionsTrigger.PullRequest
    ])]
class Build : NukeBuild
{
    [Solution(GenerateProjects = true)] 
    readonly Solution Solution;
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    public static int Main () => Execute<Build>(x => x.Test, x => x.Pack);

    Target Clean => _ => _
        .Executes(() =>
        {
            PackagesDirectory.DeleteDirectory();
        });

    Target Pack => _ => _
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {

            NuGetTasks.NuGetPack(options => options
                .SetProcessWorkingDirectory(NugetDirectory)
                .SetOutputDirectory(PackagesDirectory));
        });

    Target Test => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetPublish(options => options
                .SetProject(Solution.Tests.TestTarget)
                .SetRuntime(DotNetRuntimeIdentifier.win_x64)
                .SetConfiguration(Configuration));
        });

    
    
    // Paths
    readonly AbsolutePath NugetDirectory = RootDirectory / "nuget";
    AbsolutePath PackagesDirectory => NugetDirectory / "packages";

}
