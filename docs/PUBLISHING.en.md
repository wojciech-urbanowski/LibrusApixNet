# Publishing to NuGet

[Polski](PUBLISHING.md) | [English](PUBLISHING.en.md) | [README](../README.en.md)

## Version and workflow

[Version.props](../Version.props) defines `UpstreamVersion` and `BetaNumber`. The package version is `$(UpstreamVersion)-beta$(BetaNumber)`, currently `1.6.0-beta1`. Port and verify an upstream release before updating its version and [upstream map](upstream-map.json). The build does not fetch the latest PyPI version automatically: a new release number does not establish that its code has been ported.

[ci.yml](../.github/workflows/ci.yml) validates Windows, Linux and macOS, including local package installation in a separate project. It saves the exact package verified on Linux. The publish job downloads that artifact after the entire matrix succeeds, then pushes it to NuGet.org using Trusted Publishing.

Publishing runs on pushes to `main` and manual CI runs on `main`. Pull requests and other branches run checks without publishing. `--skip-duplicate` skips versions already published; code changes with the same version do not replace a package on NuGet. Increment `BetaNumber` for a new revision of the same upstream version (for example, `2`), or update the upstream version and reset `BetaNumber` to `1` when porting a new release.

## Initial setup

1. Sign in to [NuGet.org](https://www.nuget.org/users/account/LogOn) as `wojciech-urbanowski`.
2. Open the profile menu → **Trusted Publishing**, then create a GitHub policy.
3. Select your account as the policy owner and enter:

| Field | Value |
|---|---|
| Repository Owner | `wojciech-urbanowski` |
| Repository | `LibrusApixNet` |
| Workflow File | `ci.yml`, without `.github/workflows/` |
| Environment | leave empty |
| Package pattern / glob | `LibrusApixNet` |
| Scopes | publishing new packages and new package versions |

4. Save the policy. The workflow uses OIDC; it does not require a long-lived API key.
5. In [GitHub → Settings → Secrets and variables → Actions → Variables](https://github.com/wojciech-urbanowski/LibrusApixNet/settings/variables/actions), set repository variables:

| Variable | Value |
|---|---|
| `NUGET_USER` | `wojciech-urbanowski` (NuGet profile name, not email) |
| `NUGET_PUBLISH_ENABLED` | `true` after saving the policy |

6. Open [Actions → CI](https://github.com/wojciech-urbanowski/LibrusApixNet/actions/workflows/ci.yml), choose **Run workflow**, select `main` and start the run. A subsequent push to `main` also starts publication.
7. After **Publish to NuGet.org** succeeds, check [LibrusApixNet on NuGet](https://www.nuget.org/packages/LibrusApixNet). Indexing is asynchronous; enable prerelease packages in your package client when necessary.

Until both variables are configured, publishing is skipped. To disable publishing, set `NUGET_PUBLISH_ENABLED` to `false`; CI continues to run.

See the [official Trusted Publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

## Source and local verification

The package is licensed under GPL-3.0-only. Corresponding source and build files are available in the [public GitHub repository](https://github.com/wojciech-urbanowski/LibrusApixNet), and package metadata identifies the commit used to build it. Keep the repository public so package users can access the corresponding source.

```powershell
dotnet pack src/LibrusApixNet/LibrusApixNet.csproj -c Release
$version = ./tools/Get-PackageVersion.ps1
./tools/Test-Package.ps1 -PackagePath "./artifacts/packages/LibrusApixNet.$version.nupkg"
```

If OIDC login fails, verify the NuGet profile name, policy scopes, repository owner and workflow file name `ci.yml`. Environment is blank because the workflow does not use a GitHub Environment.
