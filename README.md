# DocFX for Unity

> DocFX usage example for Unity projects (Unity API xref map included)

[![Build status](https://ci.appveyor.com/api/projects/status/00mejohk0tfxqy7x?svg=true)](https://ci.appveyor.com/project/NormandErwan/docfxforunity)

[DocFX](https://dotnet.github.io/docfx/) tool generates a clean documentation that looks like the
[Unity documentation](https://docs.unity3d.com/Manual/index.html) with a manual (written in Markdown) and a scripting
API (from the C# scripts of the project).

This repository contains a simple example Unity project. Its documentation is automatically generated and deployed
online with every `git push`: <https://normanderwan.github.io/DocFxForUnity/>.

Every refence to the C# API or to the Unity API will be automatically linked.

| DocFxForUnity documentation manual | DocFxForUnity documentation scripting API |
|------------------------------------|-------------------------------------------|
| [![DocFxForUnity documentation manual](https://normanderwan.github.io/DocFxForUnity/images/ExampleManual.png)](https://normanderwan.github.io/DocFxForUnity/manual/coniunctis.html) | [![DocFxForUnity documentation scripting API](https://normanderwan.github.io/DocFxForUnity/images/ExampleScriptingApi.png)](https://normanderwan.github.io/DocFxForUnity/api/DocFxForUnity.Player.html) |

## Setup a Unity-like documentation on your project

### Install

Copy the `Documentation/` folder from this repository to the root of your Unity project (at the same level than the `Assets/` folder).

### Generate documentation locally

- [Install DocFX](https://dotnet.github.io/docfx/tutorial/docfx_getting_started.html#2-use-docfx-as-a-command-line-tool).
- On a command line opened on your project, run: `docfx Documentation/docfx.json --serve`. The generated website will be
visible at <http://localhost:8080>.

### Generate documentation automatically

It requires some steps but the setup is quick!

- Setup GitHub Pages:
  - [Setup GitHub Pages](https://help.github.com/en/articles/configuring-a-publishing-source-for-github-pages) to use the `gh-branch` of your repository. The documentation will be generated as a static website that will be pushed on this branch.
- Setup AppVeyor:
  - Subscribes to [AppVeyor](https://www.appveyor.com/).
  - Create an [AppVeyor project](https://ci.appveyor.com/projects/new) for you repository.
- Setup `appveyor.yml` build instructions:
  - Copy the [`appveyor.yml`](https://github.com/NormandErwan/DocFxForUnity/blob/master/appveyor.yml) from this repository to the root of your Unity project.
  - As AppVeyor will push on the `gh-pages` branch, it need an authentication token (you don't want to copy/paste your password!):
    - Generate a new [personal access token](https://github.com/settings/tokens) with `public_repo` scope.
    - Encrypt this token with <https://ci.appveyor.com/tools/encrypt> (now you can safely paste it on `appveyor.yml`).
    - If this token is compromised, you can regenerate it at any time on GitHub.
  - Edit all the `environment` variables on your `appveyor.yml`:

  ```yaml
  environment:
    git_repo: <github_user_name/github_repo_name>
    git_user_email: <github_user_email>
    git_user_name: <github_user_name>
    auth_token:
      secure: <your_secure_token> # The secure, encrypted token!
  ```

  - Push `appveyor.yml` it to GitHub.
- Try a build on your Appveyor project!

You can also found a [`.gitlab-ci.yml`](https://github.com/NormandErwan/DocFxForUnity/blob/master/.gitlab-ci.yml)
if you're using GitLab instead of GitHub. Generated website is pushed to a `public/` directory. See the
[GitLab Pages documentation](https://docs.gitlab.com/ee/user/project/pages/getting_started_part_four.html) for more
details.

## Details

### docfx.json

Copy and detail docfx.json

### appveyor.yml

Copy and detail appveyor.yml example

https://github.blog/2012-09-21-easier-builds-and-deployments-using-git-over-https-and-oauth/
https://www.appveyor.com/docs/how-to/git-push/ but errors