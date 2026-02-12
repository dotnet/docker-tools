# docker-tools Changelog

This changelog documents breaking changes and notable new features in `eng/docker-tools`.
When consuming repos receive updates, check this file to understand what changed and how to respond.

## Guidelines for Maintainers

- Update this changelog in the same PR that introduces a breaking change or notable feature
- Link to the PR (and optionally the issue) for each change
- For breaking changes, include migration guidance with before/after examples

---

## 2026-02-12: Separate Registry Endpoints from Authentication

**PR:** [#1945](https://github.com/dotnet/docker-tools/pull/1945)
**Issue:** [#1914](https://github.com/dotnet/docker-tools/issues/1914)
**Breaking:** Yes

### What Changed

Authentication details (`serviceConnection`, `resourceGroup`, `subscription`) have been moved from individual registry endpoints into a centralized `RegistryAuthentication` list. This fixes an issue where ACR authentication could fail when multiple service connections existed for the same registry.

**Before:** Each registry endpoint embedded its own authentication:

```yaml
publishConfig:
  BuildRegistry:
    server: $(acr.server)
    repoPrefix: "my-prefix/"
    resourceGroup: $(resourceGroup)
    subscription: $(subscription)
    serviceConnection:
      name: $(serviceConnectionName)
      id: $(serviceConnection.id)
      clientId: $(serviceConnection.clientId)
      tenantId: $(tenant)
  PublishRegistry:
    server: $(acr.server)
    repoPrefix: "publish/"
    resourceGroup: $(resourceGroup)
    subscription: $(subscription)
    serviceConnection:
      name: $(publishServiceConnectionName)
      id: $(publishServiceConnection.id)
      clientId: $(publishServiceConnection.clientId)
      tenantId: $(tenant)
```

**After:** Registry endpoints only contain `server` and `repoPrefix`. Authentication is centralized:

```yaml
publishConfig:
  BuildRegistry:
    server: $(acr.server)
    repoPrefix: "my-prefix/"
  PublishRegistry:
    server: $(acr.server)
    repoPrefix: "publish/"
  RegistryAuthentication:
    - server: $(acr.server)
      resourceGroup: $(resourceGroup)
      subscription: $(subscription)
      serviceConnection:
        name: $(serviceConnectionName)
        id: $(serviceConnection.id)
        clientId: $(serviceConnection.clientId)
        tenantId: $(tenant)
```

### How to Migrate

1. **Update your `publish-config-*.yml` files:**
   - Remove `resourceGroup`, `subscription`, and `serviceConnection` from each registry endpoint
   - Add a `RegistryAuthentication` list with one entry per unique registry server
   - Each entry needs: `server`, `resourceGroup`, `subscription`, and `serviceConnection`

2. **Update service connection setup** (if using `setup-service-connections.yml`):
   - The template now supports looking up service connections from `publishConfig.RegistryAuthentication`
   - Use the new `usesRegistries` parameter to specify which registries need auth setup:
     ```yaml
     - template: eng/docker-tools/templates/stages/setup-service-connections.yml
       parameters:
         publishConfig: ${{ variables.publishConfig }}
         usesRegistries:
           - $(buildRegistry.server)
           - $(publishRegistry.server)
     ```

3. **Multiple registries can share authentication:**
   - If `BuildRegistry` and `InternalMirrorRegistry` use the same ACR server, you only need one entry in `RegistryAuthentication`
   - ImageBuilder looks up auth by server name using `FindRegistryAuthentication()`
