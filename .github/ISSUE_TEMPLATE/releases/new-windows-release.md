# New Windows Release

Windows version: &lt;version&gt;

## Tasks

1. - [ ] Well before the Windows release date, contact DDFUN to schedule the provisioning of an Azure scale set for the new Windows version.
      - [ ] If the new Windows version supports ARM architecture, it will also need to be installed on new or existing ARM devices.
1. - [ ] If this is an LTS release of Windows, update [ImageBuilder](https://github.com/dotnet/docker-tools/blob/master/src/Microsoft.DotNet.ImageBuilder/src/McrTagsMetadataGenerator.cs) code to generate the correct README display name from the version specified in the manifest.
1. - [ ] Include additional build and test jobs in the [common pipeline](https://github.com/dotnet/docker-tools/blob/master/eng/common/templates/stages/build-test-publish-repo.yml) to support the new Windows version.
