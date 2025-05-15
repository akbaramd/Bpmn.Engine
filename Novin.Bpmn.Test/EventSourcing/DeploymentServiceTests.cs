using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.EventSourcing;
using Novin.Bpmn.EventSourcing.Core.Deployments;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Xunit;

public class DeploymentServiceTests
{
    private readonly IServiceProvider _serviceProvider;

    public DeploymentServiceTests()
    {
        var services = new ServiceCollection();
        services.AddBpmnEngine();

        // اگر سرویس‌های دیگری نیاز دارید اینجا اضافه کنید

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Deploy_ShouldLoadAndStoreBpmnModel()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Bpmn", "diagram_2.bpmn");
        Assert.True(File.Exists(filePath), $"File not found: {filePath}");

        var bpmnXml = await File.ReadAllTextAsync(filePath);

        var deploymentService = _serviceProvider.GetRequiredService<IDeploymentService>();
        Assert.NotNull(deploymentService);

        var deploymentKey = "TestDeploymentKey";
        var deployment = deploymentService.Deploy(deploymentKey, bpmnXml);

        Assert.NotNull(deployment);
        Assert.Equal(deploymentKey, deployment.DeploymentKey);
        Assert.NotNull(deployment.XmlContent);

        var details = deploymentService.GetDeploymentWithTopology(deployment.DeploymentId);
        Assert.NotNull(details);
        Assert.Equal(deployment.DeploymentId, details.Deployment.DeploymentId);
        Assert.NotEmpty(details.Topologies);
    }
    
    [Fact]
    public async Task Deploy_TwiceWithSameKey_ShouldIncrementVersion()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Bpmn", "diagram_2.bpmn");
        Assert.True(File.Exists(filePath), $"File not found: {filePath}");

        var bpmnXml = await File.ReadAllTextAsync(filePath);

        var deploymentService = _serviceProvider.GetRequiredService<IDeploymentService>();
        Assert.NotNull(deploymentService);

        var deploymentKey = "VersioningTestKey";

        // دیپلوی اول
        var deployment1 = deploymentService.Deploy(deploymentKey, bpmnXml);
        Assert.NotNull(deployment1);
        Assert.Equal(deploymentKey, deployment1.DeploymentKey);
        Assert.Equal("1.0.0", deployment1.Version);

        // دیپلوی دوم با همان کلید
        var deployment2 = deploymentService.Deploy(deploymentKey, bpmnXml);
        Assert.NotNull(deployment2);
        Assert.Equal(deploymentKey, deployment2.DeploymentKey);
        Assert.Equal("1.0.1", deployment2.Version);

        // مطمئن شو که DeploymentId ها متفاوت هستند (نسخه‌های متفاوت)
        Assert.NotEqual(deployment1.DeploymentId, deployment2.DeploymentId);

        // بازیابی همه نسخه‌ها و اطمینان از وجود هر دو نسخه
        var allVersions = _serviceProvider
            .GetRequiredService<IBpmnDeploymentStore>()
            .GetAllVersions(deploymentKey);

        Assert.Contains(allVersions, d => d.Version == "1.0.0");
        Assert.Contains(allVersions, d => d.Version == "1.0.1");
    }
}
