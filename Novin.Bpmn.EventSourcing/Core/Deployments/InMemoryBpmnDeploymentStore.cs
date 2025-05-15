using Novin.Bpmn.EventSourcing.Core.Deployments;

public class InMemoryBpmnDeploymentStore : IBpmnDeploymentStore
{
    // store[deploymentKey][versionNumber] = BpmnDeployment
    private readonly Dictionary<string, SortedDictionary<VersionNumber, BpmnDeployment>> _store = new();

    public BpmnDeployment Store(string deploymentKey, string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(deploymentKey))
            throw new ArgumentException("Deployment key is required.");

        if (!_store.TryGetValue(deploymentKey, out var versions))
        {
            versions = new SortedDictionary<VersionNumber, BpmnDeployment>();
            _store[deploymentKey] = versions;
        }

        VersionNumber nextVersion;

        if (versions.Any())
        {
            var lastVersion = versions.Keys.Max();
            nextVersion = lastVersion.Next();
        }
        else
        {
            nextVersion = new VersionNumber(1, 0, 0);
        }

        var deployment = new BpmnDeployment
        {
            DeploymentId = Guid.NewGuid(),
            DeploymentKey = deploymentKey,
            Version = nextVersion.ToString(), // نگهداری رشته
            XmlContent = bpmnXml,
            DeployedAt = DateTime.UtcNow
        };

        versions[nextVersion] = deployment;
        return deployment;
    }

    // تغییر پارامتر ورژن به string و تبدیل آن به VersionNumber
    public BpmnDeployment? Get(string deploymentKey, string version)
    {
        if (!_store.TryGetValue(deploymentKey, out var versions))
            return null;

        if (!VersionNumber.TryParse(version, out var verNum))
            return null;

        return versions.TryGetValue(verNum, out var deployment) ? deployment : null;
    }

    public BpmnDeployment? GetLatest(string deploymentKey)
    {
        if (_store.TryGetValue(deploymentKey, out var versions) && versions.Any())
        {
            return versions.Values.Last();
        }
        return null;
    }

    public IReadOnlyList<BpmnDeployment> GetAllVersions(string deploymentKey)
    {
        return _store.TryGetValue(deploymentKey, out var versions)
            ? versions.Values.ToList()
            : new List<BpmnDeployment>();
    }

    public BpmnDeployment? GetById(Guid deploymentId)
    {
        return _store
            .SelectMany(kv => kv.Value.Values)
            .FirstOrDefault(d => d.DeploymentId == deploymentId);
    }

    public bool Exists(Guid deploymentId)
    {
        return _store
            .SelectMany(kv => kv.Value.Values)
            .Any(d => d.DeploymentId == deploymentId);
    }

  
}
