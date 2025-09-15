# New Windows Release

Windows version: &lt;version&gt;

## Tasks

1. - [ ] Well before the Windows release date, contact DDFUN to schedule the provisioning of an Azure scale set for the new Windows version.
2. - [ ] If necessary, update [`PlatformInfo.cs`](https://github.com/dotnet/docker-tools/blob/main/src/ImageBuilder/ViewModel/PlatformInfo.cs) to generate the correct README display name from the version specified in the manifest. This is usually not needed unless Windows changes its naming scheme, since the code is version-independent.
3. - [ ] Add support for new Windows version in common pipeline templates:
      - [ ] Add new default pool variables to [`variables/common.yml`](https://github.com/dotnet/docker-tools/blob/3ba01b2b9abc1c28cd694cbddc11f5fdd8c70e8e/eng/common/templates/variables/common.yml#L48-L59)
      - [ ] Add parameter for new windows default pool in [`stages/build-test-publish-repo.yml`](https://github.com/dotnet/docker-tools/blob/3ba01b2b9abc1c28cd694cbddc11f5fdd8c70e8e/eng/common/templates/stages/build-test-publish-repo.yml#L38-L39).
      - [ ] Add new build and test jobs in [`stages/build-test-publish-repo.yml`](https://github.com/dotnet/docker-tools/blob/3ba01b2b9abc1c28cd694cbddc11f5fdd8c70e8e/eng/common/templates/stages/build-test-publish-repo.yml) to support the new Windows version.
      - [ ] If necessary, add new .NET-specific pool/image variables to [`variables/dotnet/common.yml`](https://github.com/dotnet/docker-tools/blob/3ba01b2b9abc1c28cd694cbddc11f5fdd8c70e8e/eng/common/templates/variables/dotnet/common.yml#L43-L48) and reference them from [`stages/dotnet/build-test-publish-repo.yml`](https://github.com/dotnet/docker-tools/blob/3ba01b2b9abc1c28cd694cbddc11f5fdd8c70e8e/eng/common/templates/stages/dotnet/build-test-publish-repo.yml#L115-L122)
