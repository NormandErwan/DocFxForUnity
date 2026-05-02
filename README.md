# DocFX for Unity

> [DocFX](https://dotnet.github.io/docfx/index.html) usage example for Unity projects

[DocFX](https://dotnet.github.io/docfx/) tool generates a clean documentation that looks like the
[Unity documentation](https://docs.unity3d.com/Manual/index.html) with a manual (written in Markdown) and a scripting
API (from the C# scripts of the project).

This repository contains a simple Unity example project which documentation is automatically generated and deployed
online: <https://normanderwan.github.io/DocFxForUnity/>. It references both C# API and Unity API.

| DocFxForUnity documentation manual |
|:----------------------------------:|
| [![DocFxForUnity documentation manual](https://normanderwan.github.io/DocFxForUnity/resources/ExampleManual.png)](https://normanderwan.github.io/DocFxForUnity/manual/coniunctis.html) |

| DocFxForUnity documentation scripting API |
|:----------------------------------:|
| [![DocFxForUnity documentation scripting API](https://normanderwan.github.io/DocFxForUnity/resources/ExampleScriptingApi.png)](https://normanderwan.github.io/DocFxForUnity/api/DocFxForUnity.Player.html) |

## Setup your documentation

1. [Install DocFX](https://dotnet.github.io/docfx/tutorial/docfx_getting_started.html#2-use-docfx-as-a-command-line-tool).
2. Copy the `Documentation/` folder and `DocFxForUnity.csproj` to your Unity project:

    ```diff
      .
      ├── Assets
    + ├── Documentation
    + ├── DocFxForUnity.csproj
      ├── Package
      ├── ProjectSettings
      └── README.md
    ```

    > **Tip:** You may rename `DocFxForUnity.csproj` to match your project name (*e.g.* `YourProject.csproj`).
    > If you do, also update the `"files": ["DocFxForUnity.csproj"]` entry inside `Documentation/docfx.json`.

3. Edit the following properties in `Documentation/docfx.json`, keep the others as it is:

    ```javascript
      {
        "build": {
          "globalMetadata": // Edit your documentation website info, see: https://dotnet.github.io/docfx/tutorial/docfx.exe_user_manual.html#322-reserved-metadata
          {
            "_appTitle": "Example Unity documentation",
            "_appFooter": "Example Unity documentation",
            "_enableSearch": true
          },
          "sitemap":
          {
            "baseUrl": "https://normanderwan.github.io/DocFxForUnity" // The URL of your documentation website
          }
      }
    ```

    It's the configuration file of your documentation.
    See <https://dotnet.github.io/docfx/tutorial/docfx.exe_user_manual.html#3-docfxjson-format> for more details.

4. Edit `Documentation/filterConfig.yml`:

    ```yaml
    apiRules:
    - include: # The namespaces to generate
        uidRegex: ^Your\.Namespace1
        type: Namespace
    - include:
        uidRegex: ^Your\.Namespace2
        type: Namespace
    - exclude:
        uidRegex: .* # Every other namespaces are ignored
        type: Namespace
    ```

    It tells DocFX which namespaces you want to generate the documentation.
    See <https://dotnet.github.io/docfx/tutorial/howto_filter_out_unwanted_apis_attributes.html> for more details.

5. Document your classes and methods. See <https://docs.microsoft.com/en-us/dotnet/csharp/codedoc> for more details.

6. (Optional) Add your manual pages:
    - Write a Markdown file for each page in `Documentation/manual/`.
    - Keep a list of these pages on `Documentation/manual/toc.yml`.

7. (Optional) Add resources such as images:
    - Copy them to `Documentation/resources/`.
    - Reference them on your docs or manual pages.
    - See <https://dotnet.github.io/docfx/tutorial/links_and_cross_references.html#link-to-a-file-using-relative-path> for more details.

8. (Optional) Document your namespaces:
    - For each namespace, add a `Assets/Scripts/Your/Namespace1/Your.Namespace1.md` file:

        ```yaml
        ---
        uid: Your.Namespace1
        summary: Description of the Your.Namespace1 namespace.
        ---
        ```
    - See <https://dotnet.github.io/docfx/tutorial/intro_overwrite_files.html> to know how it works.

9. Generate your documentation:
    - `DocFxForUnity.csproj` automatically resolves Unity DLLs — no extra setup is needed if Unity Hub is
      installed at its default location (`C:\Program Files\Unity\Hub` on Windows,
      `/Applications/Unity/Hub` on macOS, `~/Unity/Hub` on Linux).
    - On a command line opened on your project, run:

        ```bash
        cp README.md Documentation/index.md
        docfx Documentation/docfx.json --serve
        ```

    - The generated website will be visible at <http://localhost:8080>.

If you want to have a more similar look to the Unity documentation, see this UnityFX template for DocFX:
<https://github.com/code-beans/UnityFX>.

## Generate automatically your documentation

If you're using GitHub:

1. Copy the `.github/workflows/documentation.yml` workflow to your Unity project:

    ```diff
      .
    + ├── .github
    + |   └── workflows
    + |       └── documentation.yml
      ├── Assets
      ├── Documentation
    + ├── DocFxForUnity.csproj
      ├── Package
      ├── ProjectSettings
      └── README.md
    ```

2. [Configure the Pages of your repository to use GitHub Actions.](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site#publishing-with-a-custom-github-actions-workflow)
3. Commit and push on the `main` branch: your documentation will be built and deployed to `https://<username>.github.io/<repository>`.

If you're using GitLab, use the provided
[`.gitlab-ci.yml`](https://github.com/NormandErwan/DocFxForUnity/blob/main/.gitlab-ci.yml).
Generated website is pushed to a `public/` directory. See the
[GitLab Pages documentation](https://docs.gitlab.com/ee/user/project/pages/getting_started_part_four.html) for more
details.

## Advanced: custom Unity path (`UNITY_MANAGED_PATH`)

`DocFxForUnity.csproj` resolves Unity DLLs using the following priority order:

| Priority | Source | When used |
|:--------:|--------|-----------|
| 1 | `UNITY_MANAGED_PATH` environment variable | Explicit override — use when Unity is **not** in the default Hub path |
| 2 | `lib/UnityEngine/` in the project root | CI path, automatically populated by the GitHub Actions workflow |
| 3 | Default Unity Hub installation directory | Auto-detected at build time — no configuration required |

If none of these paths yields any DLL, a build warning is shown with a clear, actionable message.

To use `UNITY_MANAGED_PATH`, point it to the `Managed/UnityEngine` directory of your Unity installation
before running DocFX:

| OS | Command |
|----|---------|
| Windows | `set UNITY_MANAGED_PATH=C:\Program Files\Unity\Hub\Editor\6000.0.0f1\Editor\Data\Managed\UnityEngine` |
| macOS | `export UNITY_MANAGED_PATH=/Applications/Unity/Hub/Editor/6000.0.0f1/Unity.app/Contents/Managed/UnityEngine` |
| Linux | `export UNITY_MANAGED_PATH=$HOME/Unity/Hub/Editor/6000.0.0f1/Editor/Data/Managed/UnityEngine` |

Replace `6000.0.0f1` with your installed Unity version, then run DocFX as normal.

## Troubleshooting / FAQ

- DocFX outputs: `Warning:[ExtractMetadata]No metadata is generated for Assembly-CSharp,Assembly-CSharp-Editor.`

    Solution: Make sure your included your namespace in `Documentation/filterConfig.yml`:

    ```yaml
    - include:
      uidRegex: ^Your\.Namespace1
      type: Namespace
    ```

- DocFX outputs a warning that Unity managed DLLs were not found.

    Solution: Set `UNITY_MANAGED_PATH` to your Unity installation's `Managed/UnityEngine` directory
    (see [Advanced: custom Unity path](#advanced-custom-unity-path-unity_managed_path) above),
    or install Unity via Unity Hub at its default location.

- If you want to reference a specific version of Unity, change this line on your `docfx.json`:

  ```json
  "xref": [ "https://normanderwan.github.io/UnityXrefMaps/<version>/xrefmap.yml" ],
  ```

  where `<version>` is a Unity version in the form of `YYYY.x` (*e.g.* 2017.4, 2018.4, 2019.3).

## Disclaimer

This repository is not sponsored by or affiliated with Unity Technologies or its affiliates.
"Unity" is a trademark or registered trademark of Unity Technologies or its affiliates in the U.S. and elsewhere.
